using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using ParadeGuard.Api.Models;
using ParadeGuard.Api.Services;
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
        /// Check weather probability for a specific date and location
        /// </summary>
        /// <param name="query">Weather query parameters</param>
        /// <returns>Weather prediction result with historical matching days</returns>
        [HttpPost("check")]
        [ProducesResponseType(typeof(WeatherResult), 200)]
        [ProducesResponseType(typeof(ApiError), 400)]
        [ProducesResponseType(typeof(ApiError), 429)]
        [ProducesResponseType(typeof(ApiError), 500)]
        [ProducesResponseType(typeof(ApiError), 503)]
        public async Task<IActionResult> CheckWeather([FromBody] UserQuery query)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = HttpContext.TraceIdentifier;

            _logger.LogInformation("Weather check request started. RequestId: {RequestId}", requestId);

            if (query == null)
            {
                _logger.LogWarning("Null query received. RequestId: {RequestId}", requestId);
                return BadRequest(new ApiError
                {
                    Message = "Request body is required",
                    RequestId = requestId
                });
            }

            // Enhanced validation
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

            if (!query.IsValid())
            {
                _logger.LogWarning("Invalid query structure for RequestId: {RequestId}", requestId);
                return BadRequest(new ApiError
                {
                    Message = "Provide either LocationName or both Latitude and Longitude",
                    RequestId = requestId
                });
            }

            // Validate target date
            if (query.TargetDate < DateTime.Today)
            {
                _logger.LogWarning("Past date provided for RequestId: {RequestId}. Date: {Date}",
                    requestId, query.TargetDate);
                return BadRequest(new ApiError
                {
                    Message = "Target date cannot be in the past",
                    RequestId = requestId
                });
            }

            if (query.TargetDate > DateTime.Today.AddYears(1))
            {
                _logger.LogWarning("Date too far in future for RequestId: {RequestId}. Date: {Date}",
                    requestId, query.TargetDate);
                return BadRequest(new ApiError
                {
                    Message = "Target date cannot be more than 1 year in the future",
                    RequestId = requestId
                });
            }

            try
            {
                // Get coordinates
                double lat, lon;
                if (query.Latitude.HasValue && query.Longitude.HasValue)
                {
                    lat = query.Latitude.Value;
                    lon = query.Longitude.Value;
                    _logger.LogDebug("Using provided coordinates: ({Lat}, {Lon}). RequestId: {RequestId}",
                        lat, lon, requestId);
                }
                else
                {
                    _logger.LogDebug("Geocoding location: {Location}. RequestId: {RequestId}",
                        query.LocationName, requestId);
                    (lat, lon) = await _geocodingService.GetCoordinatesAsync(query.LocationName!);
                    _logger.LogDebug("Geocoding result: ({Lat}, {Lon}). RequestId: {RequestId}",
                        lat, lon, requestId);
                }

                // Get historical weather data
                _logger.LogDebug("Fetching historical weather data for {Years} years. RequestId: {RequestId}",
                    query.Years, requestId);
                var historicalData = await _nasaWeatherService.GetHistoricalDataAsync(lat, lon, query.Years);

                if (historicalData.Count == 0)
                {
                    _logger.LogWarning("No historical weather data available. RequestId: {RequestId}", requestId);
                    return BadRequest(new ApiError
                    {
                        Message = "No historical weather data available for the specified location",
                        RequestId = requestId
                    });
                }

                // Calculate probability and get matching days
                _logger.LogDebug("Calculating probability for {WeatherType}. RequestId: {RequestId}",
                    query.WeatherType, requestId);
                var (label, probability, observations, description, stats, matchingDays) = _probabilityCalculator.Calculate(
                    historicalData, query.TargetDate, query.WeatherType, query.Threshold);

                stopwatch.Stop();

                var result = new WeatherResult
                {
                    Location = query.LocationName ?? $"{lat:F4}, {lon:F4}",
                    Date = query.TargetDate,
                    Prediction = label,
                    Probability = probability,
                    Observations = observations,
                    Description = description,
                    Stats = stats,
                    MatchingDays = matchingDays,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    RequestId = requestId
                };

                _logger.LogInformation("Weather check completed successfully. RequestId: {RequestId}, " +
                    "Location: {Location}, Date: {Date}, Probability: {Probability}%, ProcessingTime: {ProcessingTime}ms, " +
                    "MatchingDays: {MatchingDaysCount}",
                    requestId, result.Location, query.TargetDate.ToString("yyyy-MM-dd"),
                    probability, stopwatch.ElapsedMilliseconds, matchingDays.Count);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "Invalid argument in weather check request. RequestId: {RequestId}", requestId);
                return BadRequest(new ApiError
                {
                    Message = ex.Message,
                    RequestId = requestId
                });
            }
            catch (InvalidOperationException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Operation error during weather check. RequestId: {RequestId}", requestId);
                return StatusCode(503, new ApiError
                {
                    Message = ex.Message,
                    RequestId = requestId
                });
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HTTP error during weather check. RequestId: {RequestId}", requestId);
                return StatusCode(503, new ApiError
                {
                    Message = "External service temporarily unavailable",
                    RequestId = requestId
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error during weather check. RequestId: {RequestId}", requestId);
                return StatusCode(500, new ApiError
                {
                    Message = "An unexpected error occurred",
                    RequestId = requestId
                });
            }
        }

        /// <summary>
        /// Get available weather types for prediction
        /// </summary>
        /// <returns>List of available weather types</returns>
        [HttpGet("weather-types")]
        [ProducesResponseType(typeof(IEnumerable<object>), 200)]
        public IActionResult GetWeatherTypes()
        {
            var weatherTypes = Enum.GetValues<WeatherType>()
                .Select(wt => new
                {
                    Value = (int)wt,
                    Name = wt.ToString(),
                    Description = wt switch
                    {
                        WeatherType.VeryHot => "Temperature exceeding threshold (default: 35°C)",
                        WeatherType.VeryCold => "Temperature below threshold (default: 5°C)",
                        WeatherType.VeryWet => "Precipitation exceeding threshold (default: 10mm)",
                        WeatherType.VeryWindy => "Wind speed exceeding threshold (default: 10 m/s)",
                        _ => "Unknown weather type"
                    }
                });

            return Ok(weatherTypes);
        }

        /// <summary>
        /// Health check endpoint for the weather service
        /// </summary>
        /// <returns>Service health status</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(object), 200)]
        public IActionResult Health()
        {
            var requestId = HttpContext.TraceIdentifier;

            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "ParadeGuard Weather API",
                version = "1.0",
                requestId = requestId,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            });
        }
    }
}