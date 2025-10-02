using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using ParadeGuard.Api.Models;
using ParadeGuard.Api.Services.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ParadeGuard.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("ApiPolicy")]
    public class WeatherController : ControllerBase
    {
        private readonly IGeocodingService _geocodingService;
        private readonly INasaWeatherService _nasaWeatherService;
        private readonly IProbabilityCalculator _probabilityCalculator;
        private readonly ILogger<WeatherController> _logger;

        public WeatherController(
            IGeocodingService geocodingService,
            INasaWeatherService nasaWeatherService,
            IProbabilityCalculator probabilityCalculator,
            ILogger<WeatherController> logger)
        {
            _geocodingService = geocodingService ?? throw new ArgumentNullException(nameof(geocodingService));
            _nasaWeatherService = nasaWeatherService ?? throw new ArgumentNullException(nameof(nasaWeatherService));
            _probabilityCalculator = probabilityCalculator ?? throw new ArgumentNullException(nameof(probabilityCalculator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Check weather prediction for a specific date and coordinates
        /// Automatically fetches 40 years of historical data and classifies weather conditions
        /// </summary>
        /// <param name="query">Weather query with coordinates and optional target date</param>
        /// <returns>Weather prediction with full 40-year historical data</returns>
        // Only showing the modified PredictWeather method - keep all other methods unchanged

        [HttpPost("predict")]
        [ProducesResponseType(typeof(WeatherResult), 200)]
        [ProducesResponseType(typeof(ApiError), 400)]
        [ProducesResponseType(typeof(ApiError), 429)]
        [ProducesResponseType(typeof(ApiError), 500)]
        [ProducesResponseType(typeof(ApiError), 503)]
        public async Task<IActionResult> PredictWeather([FromBody] UserQuery query)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = HttpContext.TraceIdentifier;

            _logger.LogInformation("Weather prediction request started. RequestId: {RequestId}", requestId);

            if (query == null)
            {
                _logger.LogWarning("Null query received. RequestId: {RequestId}", requestId);
                return BadRequest(new ApiError
                {
                    Message = "Request body is required",
                    RequestId = requestId
                });
            }

            // Validate input
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(query);

            if (!Validator.TryValidateObject(query, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(v => v.ErrorMessage).ToList();
                _logger.LogWarning("Validation failed for RequestId: {RequestId}. Errors: {Errors}",
                    requestId, string.Join("; ", errors));

                return BadRequest(new ApiError
                {
                    Message = "Invalid input parameters",
                    Details = string.Join("; ", errors),
                    RequestId = requestId
                });
            }

            // Check if either LocationName or coordinates are provided
            if (!query.IsValid())
            {
                _logger.LogWarning("Invalid query: neither location name nor coordinates provided. RequestId: {RequestId}", requestId);
                return BadRequest(new ApiError
                {
                    Message = "Please provide either a location name OR both latitude and longitude",
                    RequestId = requestId
                });
            }

            var targetDate = query.GetEffectiveTargetDate();
            double lat, lon;

            // Validate target date (cannot be in past, max 1 year in future)
            if (targetDate < DateTime.Today)
            {
                _logger.LogWarning("Past date provided for RequestId: {RequestId}. Date: {Date}",
                    requestId, targetDate);
                return BadRequest(new ApiError
                {
                    Message = "Target date cannot be in the past",
                    RequestId = requestId
                });
            }

            if (targetDate > DateTime.Today.AddYears(1))
            {
                _logger.LogWarning("Date too far in future for RequestId: {RequestId}. Date: {Date}",
                    requestId, targetDate);
                return BadRequest(new ApiError
                {
                    Message = "Target date cannot be more than 1 year in the future",
                    RequestId = requestId
                });
            }

            try
            {
                // Get coordinates - either from input or by geocoding
                if (query.Latitude.HasValue && query.Longitude.HasValue)
                {
                    lat = query.Latitude.Value;
                    lon = query.Longitude.Value;
                    _logger.LogDebug("Using provided coordinates: ({Lat}, {Lon}). RequestId: {RequestId}",
                        lat, lon, requestId);
                }
                else
                {
                    // Geocode the location name
                    _logger.LogDebug("Geocoding location: {Location}. RequestId: {RequestId}",
                        query.LocationName, requestId);
                    (lat, lon) = await _geocodingService.GetCoordinatesAsync(query.LocationName!);
                    _logger.LogDebug("Geocoding result: ({Lat}, {Lon}). RequestId: {RequestId}",
                        lat, lon, requestId);
                }

                // Always fetch 40 years of historical data
                _logger.LogDebug("Fetching 40 years of historical weather data. RequestId: {RequestId}", requestId);
                var historicalData = await _nasaWeatherService.GetHistoricalDataAsync(lat, lon, 40);

                if (historicalData.Count == 0)
                {
                    _logger.LogWarning("No historical weather data available. RequestId: {RequestId}", requestId);
                    return BadRequest(new ApiError
                    {
                        Message = "No historical weather data available for the specified location",
                        RequestId = requestId
                    });
                }

                // Automatic classification and probability calculation (now includes all probabilities)
                _logger.LogDebug("Performing automatic weather classification. RequestId: {RequestId}", requestId);
                var (label, probability, observations, description, stats, allDays, extremeCount, allProbabilities) =
                    _probabilityCalculator.CalculateAutomatic(historicalData, targetDate);

                stopwatch.Stop();

                var result = new WeatherResult
                {
                    Location = query.LocationName ?? $"{lat:F4}, {lon:F4}",
                    Date = targetDate,
                    Prediction = label,
                    Probability = probability,
                    Probabilities = allProbabilities, // NEW: Add comprehensive probability breakdown
                    Observations = observations,
                    Description = description,
                    Stats = stats,
                    AllDays = allDays,
                    ExtremeWeatherDaysCount = extremeCount,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    RequestId = requestId
                };

                _logger.LogInformation("Weather prediction completed successfully. RequestId: {RequestId}, " +
                    "Location: {Location}, Date: {Date}, Prediction: {Prediction}, Probability: {Probability}%, " +
                    "ProcessingTime: {ProcessingTime}ms, TotalDays: {TotalDays}, ExtremeDays: {ExtremeDays}, " +
                    "AllProbabilities: {AllProbabilities}",
                    requestId, result.Location, targetDate.ToString("yyyy-MM-dd"),
                    label, probability, stopwatch.ElapsedMilliseconds, allDays.Count, extremeCount,
                    string.Join(", ", allProbabilities.Select(kvp => $"{kvp.Key}={kvp.Value}%")));

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "Invalid argument in weather prediction request. RequestId: {RequestId}", requestId);
                return BadRequest(new ApiError
                {
                    Message = ex.Message,
                    RequestId = requestId
                });
            }
            catch (InvalidOperationException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Operation error during weather prediction. RequestId: {RequestId}", requestId);
                return StatusCode(503, new ApiError
                {
                    Message = ex.Message,
                    RequestId = requestId
                });
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HTTP error during weather prediction. RequestId: {RequestId}", requestId);
                return StatusCode(503, new ApiError
                {
                    Message = "External service temporarily unavailable",
                    RequestId = requestId
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error during weather prediction. RequestId: {RequestId}", requestId);
                return StatusCode(500, new ApiError
                {
                    Message = "An unexpected error occurred",
                    RequestId = requestId
                });
            }
        }

        /// <summary>
        /// Get classification thresholds used by the system
        /// </summary>
        [HttpGet("thresholds")]
        [ProducesResponseType(typeof(object), 200)]
        public IActionResult GetThresholds()
        {
            return Ok(new
            {
                temperature = new
                {
                    veryHot = new { value = 35.0, unit = "°C", description = "Temperature exceeding 35°C" },
                    veryCold = new { value = 5.0, unit = "°C", description = "Temperature below 5°C" }
                },
                precipitation = new
                {
                    veryWet = new { value = 10.0, unit = "mm", description = "Precipitation exceeding 10mm" }
                },
                wind = new
                {
                    veryWindy = new { value = 10.0, unit = "m/s", description = "Wind speed exceeding 10 m/s" }
                }
            });
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(object), 200)]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "Weather Prediction API",
                version = "2.0",
                requestId = HttpContext.TraceIdentifier
            });
        }
    }
}