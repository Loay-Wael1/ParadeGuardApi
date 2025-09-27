namespace ParadeGuard.Api.Models
{
    public class HealthCheckResponse
    {
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Service { get; set; } = "";
        public string Version { get; set; } = "";
        public Dictionary<string, object>? Details { get; set; }
        public long? ResponseTimeMs { get; set; }
    }
}
