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
        /// Complete list of all 40 historical days with full weather data
        /// </summary>
        [JsonPropertyName("allDays")]
        public List<HistoricalWeatherDay> AllDays { get; set; } = new();

        /// <summary>
        /// Count of days that matched extreme weather conditions
        /// </summary>
        public int ExtremeWeatherDaysCount { get; set; }
    }

    public class HistoricalWeatherDay
    {
        public DateTime Date { get; set; }
        public int Year { get; set; }

        [JsonPropertyName("temp")]
        public double? Temperature { get; set; }

        [JsonPropertyName("precip")]
        public double? Precipitation { get; set; }

        [JsonPropertyName("windSpeed")]
        public double? WindSpeed { get; set; }

        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }

        /// <summary>
        /// Auto-classified weather condition for this day
        /// </summary>
        public string Classification { get; set; } = "";

        /// <summary>
        /// Indicates if this day had extreme weather
        /// </summary>
        public bool IsExtremeWeather { get; set; }
    }
}
