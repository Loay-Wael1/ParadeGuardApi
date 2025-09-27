using System.Text.Json.Serialization;
namespace ParadeGuard.Api.Models
{
    public class WeatherResult
    {
        public string Location { get; set; } = "";
        public DateTime Date { get; set; }
        public string Prediction { get; set; } = "";

        [JsonPropertyName("probabilityPercent")]
        public double Probability { get; set; }

        public int Observations { get; set; }
        public string Description { get; set; } = "";
        public WeatherStats? Stats { get; set; }

        [JsonPropertyName("processingTimeMs")]
        public long ProcessingTimeMs { get; set; }

        public string RequestId { get; set; } = "";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// List of historical days that matched the requested weather condition
        /// </summary>
        public List<MatchingWeatherDay> MatchingDays { get; set; } = new();
    }

    public class MatchingWeatherDay
    {
        public DateTime Date { get; set; }
        public int Year { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public string WeatherType { get; set; } = "";
        public string Unit { get; set; } = "";
        public WeatherDayContext? Context { get; set; }
    }

    public class WeatherDayContext
    {
        public double? Temperature { get; set; }
        public double? Precipitation { get; set; }
        public double? WindSpeed { get; set; }
        public double? Humidity { get; set; }
    }
}