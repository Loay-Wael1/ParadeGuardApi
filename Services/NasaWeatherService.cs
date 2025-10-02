using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParadeGuard.Api.Config;
using ParadeGuard.Api.Models;
using ParadeGuard.Api.Services.Interfaces;

namespace ParadeGuard.Api.Services
{
    public class NasaWeatherService : INasaWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ApiKeysConfig _apiKeys;
        private readonly ApiConfig _apiConfig;
        private readonly CacheConfig _cacheConfig;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NasaWeatherService> _logger;

        public NasaWeatherService(
            HttpClient httpClient,
            IOptions<ApiKeysConfig> apiKeys,
            IOptions<ApiConfig> apiConfig,
            IOptions<CacheConfig> cacheConfig,
            IMemoryCache cache,
            ILogger<NasaWeatherService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKeys = apiKeys?.Value ?? throw new ArgumentNullException(nameof(apiKeys));
            _apiConfig = apiConfig?.Value ?? throw new ArgumentNullException(nameof(apiConfig));
            _cacheConfig = cacheConfig?.Value ?? throw new ArgumentNullException(nameof(cacheConfig));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _httpClient.Timeout = TimeSpan.FromSeconds(_apiConfig.RequestTimeoutSeconds);
        }

        public async Task<List<WeatherData>> GetHistoricalDataAsync(double lat, double lon, int years)
        {
            if (years < 1 || years > 40)
                throw new ArgumentOutOfRangeException(nameof(years), "Years must be between 1 and 40");

            if (lat < -90 || lat > 90)
                throw new ArgumentOutOfRangeException(nameof(lat), "Latitude must be between -90 and 90");

            if (lon < -180 || lon > 180)
                throw new ArgumentOutOfRangeException(nameof(lon), "Longitude must be between -180 and 180");

            var endYear = DateTime.Now.Year - 1;
            var startYear = endYear - (years - 1);
            var cacheKey = $"nasa_weather:{lat:F2}:{lon:F2}:{startYear}:{endYear}";

            if (_cache.TryGetValue(cacheKey, out List<WeatherData>? cached) && cached != null)
            {
                _logger.LogDebug("Cache hit for NASA weather data: {Lat}, {Lon}, records: {Count}",
                    lat, lon, cached.Count);
                return cached;
            }

            var start = $"{startYear}0101";
            var end = $"{endYear}1231";
            var url = BuildNasaUrl(lat, lon, start, end);

            _logger.LogInformation("NASA POWER request for coordinates: ({Lat}, {Lon}), " +
                "years: {Years} ({StartYear}-{EndYear})", lat, lon, years, startYear, endYear);

            try
            {
                using var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("NASA POWER API returned {StatusCode}: {ReasonPhrase}. Content: {Content}",
                        (int)response.StatusCode, response.ReasonPhrase, errorContent);

                    throw new InvalidOperationException(
                        $"NASA weather service returned {response.StatusCode}: {response.ReasonPhrase}");
                }

                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("Empty response from NASA weather service");
                }

                var results = ParseNasaResponse(content, lat, lon);

                if (results.Count == 0)
                {
                    _logger.LogWarning("No weather data returned from NASA POWER for ({Lat}, {Lon})", lat, lon);
                    throw new InvalidOperationException("No weather data available for the specified location and time period");
                }

                var validResults = results.Where(r => r.IsValid()).ToList();
                if (validResults.Count < results.Count * 0.5)
                {
                    _logger.LogWarning("Poor data quality from NASA POWER: {ValidCount}/{TotalCount} valid records",
                        validResults.Count, results.Count);
                }

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheConfig.WeatherExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_cacheConfig.WeatherExpirationMinutes / 2),
                    Priority = CacheItemPriority.Normal,
                    Size = Math.Max(1, validResults.Count / 100) // Size based on data volume
                };

                _cache.Set(cacheKey, validResults, cacheOptions);

                _logger.LogInformation("NASA POWER request successful: {RecordCount} valid records out of {TotalCount}",
                    validResults.Count, results.Count);

                return validResults;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during NASA POWER request for ({Lat}, {Lon})", lat, lon);
                throw new InvalidOperationException("NASA weather service is temporarily unavailable", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Timeout during NASA POWER request for ({Lat}, {Lon})", lat, lon);
                throw new InvalidOperationException("NASA weather service request timed out", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON response from NASA POWER service for ({Lat}, {Lon})", lat, lon);
                throw new InvalidOperationException("Invalid response from NASA weather service", ex);
            }
        }

        private string BuildNasaUrl(double lat, double lon, string start, string end)
        {
            var url = $"{_apiConfig.NasaBaseUrl}?start={start}&end={end}&latitude={lat:F4}&longitude={lon:F4}" +
                      $"&parameters=T2M,PRECTOTCORR,WS2M,RH2M&community=RE&format=JSON";

            if (!string.IsNullOrWhiteSpace(_apiKeys.NasaApiKey))
            {
                url += $"&api_key={_apiKeys.NasaApiKey}";
            }

            return url;
        }

        private List<WeatherData> ParseNasaResponse(string jsonContent, double lat, double lon)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                var results = new List<WeatherData>();

                if (!document.RootElement.TryGetProperty("properties", out var properties))
                {
                    _logger.LogError("NASA POWER response missing 'properties' field");
                    throw new InvalidOperationException("Invalid NASA POWER response format: missing properties");
                }

                if (!properties.TryGetProperty("parameter", out var parameters))
                {
                    _logger.LogError("NASA POWER response missing 'properties.parameter' field");
                    throw new InvalidOperationException("Invalid NASA POWER response format: missing parameters");
                }

                parameters.TryGetProperty("T2M", out var temperature);
                parameters.TryGetProperty("PRECTOTCORR", out var precipitation);
                parameters.TryGetProperty("WS2M", out var windSpeed);
                parameters.TryGetProperty("RH2M", out var humidity);

                var dateSource = GetDateSource(temperature, precipitation, windSpeed, humidity);
                if (dateSource.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogError("No usable time-series data found in NASA POWER response for ({Lat}, {Lon})", lat, lon);
                    throw new InvalidOperationException("No time-series data available in NASA response");
                }

                foreach (var property in dateSource.EnumerateObject())
                {
                    if (!DateTime.TryParseExact(property.Name, "yyyyMMdd", null,
                        System.Globalization.DateTimeStyles.None, out var date))
                    {
                        _logger.LogDebug("Skipping invalid date format: {DateString}", property.Name);
                        continue;
                    }

                    var weatherData = new WeatherData
                    {
                        Date = date,
                        Temperature = GetParameterValue(temperature, property.Name),
                        Precipitation = GetParameterValue(precipitation, property.Name),
                        WindSpeed = GetParameterValue(windSpeed, property.Name),
                        Humidity = GetParameterValue(humidity, property.Name)
                    };

                    // Only include data with at least some valid values
                    if (weatherData.Temperature.HasValue || weatherData.Precipitation.HasValue ||
                        weatherData.WindSpeed.HasValue || weatherData.Humidity.HasValue)
                    {
                        results.Add(weatherData);
                    }
                }

                return results.OrderBy(x => x.Date).ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse NASA POWER JSON response. Content length: {Length}",
                    jsonContent.Length);
                throw;
            }
        }

        private JsonElement GetDateSource(params JsonElement[] parameters)
        {
            return parameters.FirstOrDefault(p => p.ValueKind == JsonValueKind.Object);
        }

        private double? GetParameterValue(JsonElement parameter, string date)
        {
            if (parameter.ValueKind == JsonValueKind.Object &&
                parameter.TryGetProperty(date, out var value) &&
                value.ValueKind == JsonValueKind.Number)
            {
                var doubleValue = value.GetDouble();

                // Filter out NASA's fill values and obviously invalid data
                if (doubleValue < -900 || double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    return null;
                }

                return doubleValue;
            }
            return null;
        }
    }
}