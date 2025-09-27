using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParadeGuard.Api.Config;
using ParadeGuard.Api.Services.Interfaces;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadeGuard.Api.Services
{
    public class GeocodingService : IGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly ApiKeysConfig _apiKeys;
        private readonly ApiConfig _apiConfig;
        private readonly CacheConfig _cacheConfig;
        private readonly IMemoryCache _cache;
        private readonly ILogger<GeocodingService> _logger;

        // Regex for input sanitization
        private static readonly Regex LocationValidationRegex = new(@"^[a-zA-Z0-9\s\-\.,'\(\)]+$", RegexOptions.Compiled);

        public GeocodingService(
            HttpClient httpClient,
            IOptions<ApiKeysConfig> apiKeys,
            IOptions<ApiConfig> apiConfig,
            IOptions<CacheConfig> cacheConfig,
            IMemoryCache cache,
            ILogger<GeocodingService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKeys = apiKeys?.Value ?? throw new ArgumentNullException(nameof(apiKeys));
            _apiConfig = apiConfig?.Value ?? throw new ArgumentNullException(nameof(apiConfig));
            _cacheConfig = cacheConfig?.Value ?? throw new ArgumentNullException(nameof(cacheConfig));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _httpClient.Timeout = TimeSpan.FromSeconds(_apiConfig.RequestTimeoutSeconds);
        }

        public async Task<(double lat, double lon)> GetCoordinatesAsync(string place)
        {
            if (string.IsNullOrWhiteSpace(place))
                throw new ArgumentException("Location name is required", nameof(place));

            if (place.Length > 200)
                throw new ArgumentException("Location name is too long (max 200 characters)", nameof(place));

            // Input sanitization
            if (!LocationValidationRegex.IsMatch(place))
                throw new ArgumentException("Location name contains invalid characters", nameof(place));

            var normalizedPlace = place.Trim().ToLowerInvariant();
            var cacheKey = $"geocoding:{normalizedPlace}";

            if (_cache.TryGetValue(cacheKey, out (double lat, double lon) cached))
            {
                _logger.LogDebug("Cache hit for geocoding: {Place}", place);
                return cached;
            }

            if (string.IsNullOrWhiteSpace(_apiKeys.GeocodingApiKey))
            {
                throw new InvalidOperationException("Geocoding API key is not configured");
            }

            var url = $"{_apiConfig.OpenCageBaseUrl}?q={Uri.EscapeDataString(place)}&key={_apiKeys.GeocodingApiKey}&limit=1&no_annotations=1";

            _logger.LogInformation("Geocoding request for: {Place}", place);

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);

                var root = document.RootElement;

                if (!root.TryGetProperty("results", out var results))
                {
                    _logger.LogWarning("Invalid response format from geocoding service for: {Place}", place);
                    throw new InvalidOperationException("Invalid response from geocoding service");
                }

                if (results.GetArrayLength() == 0)
                {
                    _logger.LogWarning("Location not found: {Place}", place);
                    throw new InvalidOperationException($"Location '{place}' not found");
                }

                var firstResult = results[0];
                if (!firstResult.TryGetProperty("geometry", out var geometry))
                {
                    throw new InvalidOperationException("Invalid geometry data in response");
                }

                var lat = geometry.GetProperty("lat").GetDouble();
                var lon = geometry.GetProperty("lng").GetDouble();

                if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                {
                    throw new InvalidOperationException("Invalid coordinates received from geocoding service");
                }

                var coordinates = (lat, lon);

                // Cache with size tracking
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheConfig.GeocodingExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_cacheConfig.GeocodingExpirationMinutes / 2),
                    Priority = CacheItemPriority.High,
                    Size = 1
                };

                _cache.Set(cacheKey, coordinates, cacheOptions);

                _logger.LogInformation("Geocoding successful: {Place} -> ({Lat}, {Lon})", place, lat, lon);

                return coordinates;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during geocoding request for: {Place}", place);
                throw new InvalidOperationException("Geocoding service is temporarily unavailable", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid response from geocoding service for: {Place}", place);
                throw new InvalidOperationException("Invalid response from geocoding service", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Timeout during geocoding request for: {Place}", place);
                throw new InvalidOperationException("Geocoding request timed out", ex);
            }
        }
    }
}