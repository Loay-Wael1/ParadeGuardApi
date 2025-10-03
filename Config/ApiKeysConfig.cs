using System.ComponentModel.DataAnnotations;

namespace ParadeGuard.Api.Config
{
    public class ApiKeysConfig
    {
        private string? _nasaApiKey;
        private string? _geocodingApiKey;
        private string? _groqApiKey;

        [Required(ErrorMessage = "NASA API key is required")]
        public string NasaApiKey
        {
            get => _nasaApiKey ??
                   Environment.GetEnvironmentVariable("PARADE_GUARD_NASA_API_KEY") ??
                   Environment.GetEnvironmentVariable("NasaApiKey") ?? "";
            set => _nasaApiKey = value;
        }

        [Required(ErrorMessage = "Geocoding API key is required")]
        public string GeocodingApiKey
        {
            get => _geocodingApiKey ??
                   Environment.GetEnvironmentVariable("PARADE_GUARD_GEOCODING_API_KEY") ??
                   Environment.GetEnvironmentVariable("GeocodingApiKey") ?? "";
            set => _geocodingApiKey = value;
        }

        [Required(ErrorMessage = "Groq API key is required")]
        public string GroqApiKey
        {
            get => _groqApiKey ??
                   Environment.GetEnvironmentVariable("PARADE_GUARD_GROQ_API_KEY") ??
                   Environment.GetEnvironmentVariable("GroqApiKey") ?? "";
            set => _groqApiKey = value;
        }

        /// <summary>
        /// Validates that all required API keys are present
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(GeocodingApiKey) &&
                   !string.IsNullOrWhiteSpace(NasaApiKey) &&
                   !string.IsNullOrWhiteSpace(GroqApiKey);
        }
    }

    public class CacheConfig
    {
        [Range(1, 10080, ErrorMessage = "Cache expiration must be between 1 minute and 1 week")]
        public int DefaultExpirationMinutes { get; set; } = 720; // 12 hours

        [Range(1, 10080, ErrorMessage = "Geocoding cache expiration must be between 1 minute and 1 week")]
        public int GeocodingExpirationMinutes { get; set; } = 1440; // 24 hours

        [Range(1, 10080, ErrorMessage = "Weather cache expiration must be between 1 minute and 1 week")]
        public int WeatherExpirationMinutes { get; set; } = 720; // 12 hours

        [Range(1, 1000, ErrorMessage = "Max cache items must be between 1 and 1000")]
        public int MaxCacheItems { get; set; } = 500;
    }

    public class ApiConfig
    {
        [Range(5, 300, ErrorMessage = "Request timeout must be between 5 and 300 seconds")]
        public int RequestTimeoutSeconds { get; set; } = 30;

        [Range(1, 10, ErrorMessage = "Max retry attempts must be between 1 and 10")]
        public int MaxRetryAttempts { get; set; } = 3;

        [Required]
        [Url(ErrorMessage = "NASA Base URL must be a valid URL")]
        public string NasaBaseUrl { get; set; } = "https://power.larc.nasa.gov/api/temporal/daily/point";

        [Required]
        [Url(ErrorMessage = "OpenCage Base URL must be a valid URL")]
        public string OpenCageBaseUrl { get; set; } = "https://api.opencagedata.com/geocode/v1/json";

        [Range(1, 100, ErrorMessage = "Rate limit must be between 1 and 100")]
        public int RateLimitPerMinute { get; set; } = 60;

        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }

    public class MonitoringConfig
    {
        public bool EnableDetailedLogging { get; set; } = false;
        public bool EnableMetrics { get; set; } = true;
        public string ApplicationInsightsKey { get; set; } = "";
    }
}