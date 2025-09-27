
namespace ParadeGuard.Api.Models
{
    public class ApiError
    {
        public string Message { get; set; } = "";
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string RequestId { get; set; } = "";
        public string? ErrorCode { get; set; }
    }
}