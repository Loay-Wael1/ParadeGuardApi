using System.Text.Json.Serialization;
namespace ParadeGuard.Api.Models
{
    public class WeatherStats
    {
        [JsonPropertyName("avgTemperature")]
        public double? AverageTemperature { get; set; }

        [JsonPropertyName("avgPrecipitation")]
        public double? AveragePrecipitation { get; set; }

        [JsonPropertyName("avgWindSpeed")]
        public double? AverageWindSpeed { get; set; }

        [JsonPropertyName("avgHumidity")]
        public double? AverageHumidity { get; set; }

        public double? MinTemperature { get; set; }
        public double? MaxTemperature { get; set; }
        public double? MaxPrecipitation { get; set; }
        public double? MaxWindSpeed { get; set; }
    }
}