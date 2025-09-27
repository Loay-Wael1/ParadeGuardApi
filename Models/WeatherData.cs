using System.Text.Json.Serialization;
namespace ParadeGuard.Api.Models
{
    public class WeatherData
    {
        public DateTime Date { get; set; }

        [JsonPropertyName("temp")]
        public double? Temperature { get; set; }   // °C (T2M)

        [JsonPropertyName("precip")]
        public double? Precipitation { get; set; } // mm (PRECTOT)

        [JsonPropertyName("windSpeed")]
        public double? WindSpeed { get; set; }     // m/s (WS2M)

        [JsonPropertyName("humidity")]
        public double? Humidity { get; set; }      // % (RH2M)

        public bool IsComplete => Temperature.HasValue && Precipitation.HasValue &&
                                 WindSpeed.HasValue && Humidity.HasValue;

        public bool IsValid()
        {
            return (!Temperature.HasValue || (Temperature >= -100 && Temperature <= 70)) &&
                   (!Precipitation.HasValue || (Precipitation >= 0 && Precipitation <= 1000)) &&
                   (!WindSpeed.HasValue || (WindSpeed >= 0 && WindSpeed <= 200)) &&
                   (!Humidity.HasValue || (Humidity >= 0 && Humidity <= 100));
        }
    }

}