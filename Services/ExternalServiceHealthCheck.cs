using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using ParadeGuard.Api.Config;
using System.Diagnostics;

namespace ParadeGuard.Api.Services
{
    public class ExternalServiceHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private readonly ApiKeysConfig _apiKeys;
        private readonly ApiConfig _apiConfig;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExternalServiceHealthCheck> _logger;

        public ExternalServiceHealthCheck(
            HttpClient httpClient,
            IOptions<ApiKeysConfig> apiKeys,
            IOptions<ApiConfig> apiConfig,
            IMemoryCache cache,
            ILogger<ExternalServiceHealthCheck> logger)
        {
            _httpClient = httpClient;
            _apiKeys = apiKeys.Value;
            _apiConfig = apiConfig.Value;
            _cache = cache;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var healthData = new Dictionary<string, object>();

            try
            {
                var healthTasks = new List<Task<(string service, bool isHealthy, string? error, double responseTime)>>();

                // Check OpenCage API if API key is available
                if (!string.IsNullOrWhiteSpace(_apiKeys.GeocodingApiKey))
                {
                    healthTasks.Add(CheckOpenCageApiAsync(cancellationToken));
                }

                // Check NASA POWER API
                healthTasks.Add(CheckNasaApiAsync(cancellationToken));

                // Check memory cache health
                healthTasks.Add(CheckMemoryCacheAsync(cancellationToken));

                var results = await Task.WhenAll(healthTasks);

                var healthyServices = 0;
                var totalServices = results.Length;

                foreach (var (service, isHealthy, error, responseTime) in results)
                {
                    healthData[service] = new
                    {
                        Status = isHealthy ? "Healthy" : "Unhealthy",
                        ResponseTime = $"{responseTime:F2}ms",
                        Error = error
                    };

                    if (isHealthy) healthyServices++;
                }

                stopwatch.Stop();
                healthData["TotalCheckTime"] = $"{stopwatch.ElapsedMilliseconds}ms";
                healthData["HealthyServices"] = $"{healthyServices}/{totalServices}";

                var overallHealth = (double)healthyServices / totalServices;

                if (overallHealth == 1.0)
                {
                    return HealthCheckResult.Healthy("All external services are available", healthData);
                }
                else if (overallHealth >= 0.5)
                {
                    return HealthCheckResult.Degraded($"{healthyServices}/{totalServices} external services are available", null, healthData);
                }
                else
                {
                    return HealthCheckResult.Unhealthy($"Only {healthyServices}/{totalServices} external services are available", null, healthData);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Health check failed with exception");

                healthData["Error"] = ex.Message;
                healthData["TotalCheckTime"] = $"{stopwatch.ElapsedMilliseconds}ms";

                return HealthCheckResult.Unhealthy("Health check failed with exception", ex, healthData);
            }
        }

        private async Task<(string service, bool isHealthy, string? error, double responseTime)> CheckOpenCageApiAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var url = $"{_apiConfig.OpenCageBaseUrl}?q=London&key={_apiKeys.GeocodingApiKey}&limit=1&no_annotations=1";

                using var response = await _httpClient.GetAsync(url, cancellationToken);
                stopwatch.Stop();

                var isHealthy = response.IsSuccessStatusCode;
                var error = isHealthy ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";

                return ("OpenCage Geocoding", isHealthy, error, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ("OpenCage Geocoding", false, ex.Message, stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private async Task<(string service, bool isHealthy, string? error, double responseTime)> CheckNasaApiAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
                var url = $"{_apiConfig.NasaBaseUrl}?start={yesterday}&end={yesterday}&latitude=0&longitude=0&parameters=T2M&community=RE&format=JSON";

                if (!string.IsNullOrWhiteSpace(_apiKeys.NasaApiKey))
                {
                    url += $"&api_key={_apiKeys.NasaApiKey}";
                }

                using var response = await _httpClient.GetAsync(url, cancellationToken);
                stopwatch.Stop();

                var isHealthy = response.IsSuccessStatusCode;
                var error = isHealthy ? null : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";

                return ("NASA POWER", isHealthy, error, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ("NASA POWER", false, ex.Message, stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private async Task<(string service, bool isHealthy, string? error, double responseTime)> CheckMemoryCacheAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Test cache operations
                var testKey = "health_check_test";
                var testValue = "test_value";

                _cache.Set(testKey, testValue, TimeSpan.FromSeconds(1));
                var retrieved = _cache.Get<string>(testKey);

                stopwatch.Stop();

                var isHealthy = testValue.Equals(retrieved);
                var error = isHealthy ? null : "Cache read/write test failed";

                return ("Memory Cache", isHealthy, error, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ("Memory Cache", false, ex.Message, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}