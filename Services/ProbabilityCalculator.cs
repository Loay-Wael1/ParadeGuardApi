using ParadeGuard.Api.Models;
using ParadeGuard.Api.Services.Interfaces;

namespace ParadeGuard.Api.Services
{
    public class ProbabilityCalculator : IProbabilityCalculator
    {
        // Fixed thresholds for automatic classification
        private const double HOT_THRESHOLD = 35.0;   // °C
        private const double COLD_THRESHOLD = 5.0;   // °C
        private const double WET_THRESHOLD = 10.0;   // mm
        private const double WINDY_THRESHOLD = 10.0; // m/s

        public (string label, double probability, int observations, string description,
                WeatherStats stats, List<HistoricalWeatherDay> allDays, int extremeCount)
            CalculateAutomatic(List<WeatherData> historical, DateTime targetDate)
        {
            // Filter data for the same month/day across all years
            var relevantData = historical
                .Where(d => d.Date.Month == targetDate.Month && d.Date.Day == targetDate.Day)
                .OrderByDescending(d => d.Date.Year)
                .ToList();

            var totalObservations = relevantData.Count;

            if (totalObservations == 0)
            {
                return ("NoData", 0.0, 0, "No historical data available for this date.",
                       null, new List<HistoricalWeatherDay>(), 0);
            }

            // Convert all days to HistoricalWeatherDay with automatic classification
            var allDays = relevantData.Select(d => {
                var classification = ClassifyWeather(d);
                return new HistoricalWeatherDay
                {
                    Date = d.Date,
                    Year = d.Date.Year,
                    Temperature = d.Temperature,
                    Precipitation = d.Precipitation,
                    WindSpeed = d.WindSpeed,
                    Humidity = d.Humidity,
                    Classification = classification,
                    IsExtremeWeather = classification != "Normal"
                };
            }).ToList();

            // Count extreme weather occurrences
            var extremeWeatherDays = allDays.Where(d => d.IsExtremeWeather).ToList();
            var extremeCount = extremeWeatherDays.Count;

            // Determine dominant weather pattern
            var weatherCounts = allDays
                .GroupBy(d => d.Classification)
                .Select(g => new { Classification = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            string dominantLabel;
            double probability;
            string description;

            if (extremeCount == 0)
            {
                dominantLabel = "Normal";
                probability = 100.0;
                description = "Historical data shows consistently normal weather conditions for this date.";
            }
            else
            {
                var mostCommon = weatherCounts.FirstOrDefault(w => w.Classification != "Normal");
                if (mostCommon != null && mostCommon.Count > 0)
                {
                    dominantLabel = mostCommon.Classification;
                    probability = Math.Round((double)mostCommon.Count / totalObservations * 100.0, 2);
                    description = GenerateDescription(dominantLabel, mostCommon.Count, totalObservations);
                }
                else
                {
                    dominantLabel = "Normal";
                    probability = Math.Round((double)(totalObservations - extremeCount) / totalObservations * 100.0, 2);
                    description = "Historical data shows primarily normal weather conditions for this date.";
                }
            }

            var stats = CalculateStats(relevantData);

            return (dominantLabel, probability, totalObservations, description, stats, allDays, extremeCount);
        }

        private string ClassifyWeather(WeatherData data)
        {
            var conditions = new List<string>();

            if (data.Temperature.HasValue)
            {
                if (data.Temperature.Value > HOT_THRESHOLD)
                    conditions.Add("VeryHot");
                else if (data.Temperature.Value < COLD_THRESHOLD)
                    conditions.Add("VeryCold");
            }

            if (data.Precipitation.HasValue && data.Precipitation.Value > WET_THRESHOLD)
                conditions.Add("VeryWet");

            if (data.WindSpeed.HasValue && data.WindSpeed.Value > WINDY_THRESHOLD)
                conditions.Add("VeryWindy");

            if (conditions.Count == 0)
                return "Normal";

            // If multiple conditions, return the most significant
            if (conditions.Contains("VeryHot")) return "VeryHot";
            if (conditions.Contains("VeryCold")) return "VeryCold";
            if (conditions.Contains("VeryWet")) return "VeryWet";
            if (conditions.Contains("VeryWindy")) return "VeryWindy";

            return "Normal";
        }

        private string GenerateDescription(string classification, int count, int total)
        {
            var percentage = Math.Round((double)count / total * 100.0, 1);

            return classification switch
            {
                "VeryHot" => $"Historical data shows {percentage}% probability of very hot conditions (>35°C) on this date across {total} years.",
                "VeryCold" => $"Historical data shows {percentage}% probability of very cold conditions (<5°C) on this date across {total} years.",
                "VeryWet" => $"Historical data shows {percentage}% probability of heavy rainfall (>10mm) on this date across {total} years.",
                "VeryWindy" => $"Historical data shows {percentage}% probability of strong winds (>10 m/s) on this date across {total} years.",
                _ => $"Historical data analysis based on {total} years of observations."
            };
        }

        private WeatherStats CalculateStats(List<WeatherData> data)
        {
            var temperatures = data.Where(d => d.Temperature.HasValue).Select(d => d.Temperature!.Value);
            var precipitation = data.Where(d => d.Precipitation.HasValue).Select(d => d.Precipitation!.Value);
            var windSpeeds = data.Where(d => d.WindSpeed.HasValue).Select(d => d.WindSpeed!.Value);
            var humidity = data.Where(d => d.Humidity.HasValue).Select(d => d.Humidity!.Value);

            return new WeatherStats
            {
                AverageTemperature = temperatures.Any() ? Math.Round(temperatures.Average(), 2) : null,
                AveragePrecipitation = precipitation.Any() ? Math.Round(precipitation.Average(), 2) : null,
                AverageWindSpeed = windSpeeds.Any() ? Math.Round(windSpeeds.Average(), 2) : null,
                AverageHumidity = humidity.Any() ? Math.Round(humidity.Average(), 2) : null,
                MinTemperature = temperatures.Any() ? Math.Round(temperatures.Min(), 2) : null,
                MaxTemperature = temperatures.Any() ? Math.Round(temperatures.Max(), 2) : null,
                MaxPrecipitation = precipitation.Any() ? Math.Round(precipitation.Max(), 2) : null,
                MaxWindSpeed = windSpeeds.Any() ? Math.Round(windSpeeds.Max(), 2) : null
            };
        }
    }
}
