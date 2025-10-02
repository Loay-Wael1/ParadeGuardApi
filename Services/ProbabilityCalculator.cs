using ParadeGuard.Api.Models;
using ParadeGuard.Api.Services.Interfaces;

namespace ParadeGuard.Api.Services
{
    public class ProbabilityCalculator : IProbabilityCalculator
    {
        private const double HOT_THRESHOLD = 35.0;
        private const double COLD_THRESHOLD = 5.0;
        private const double WET_THRESHOLD = 10.0;
        private const double WINDY_THRESHOLD = 10.0;

        /// <summary>
        /// Calculates weather probabilities with comprehensive classification breakdown
        /// Returns dominant classification along with probabilities for all weather types
        /// </summary>
        public (string label, double probability, int observations, string description,
                WeatherStats stats, List<HistoricalWeatherDay> allDays, int extremeCount,
                Dictionary<string, double> allProbabilities)
            CalculateAutomatic(List<WeatherData> historical, DateTime targetDate)
        {
            var targetMonth = targetDate.Month;
            var targetDay = targetDate.Day;

            // Pre-allocate with expected size (40 years max)
            var allDays = new List<HistoricalWeatherDay>(40);
            var temperatures = new List<double>(40);
            var precipitations = new List<double>(40);
            var windSpeeds = new List<double>(40);
            var humidities = new List<double>(40);

            foreach (var data in historical)
            {
                // Filter inline: only process matching dates
                if (data.Date.Month != targetMonth || data.Date.Day != targetDay)
                    continue;

                // Classify weather
                var classification = ClassifyWeather(data);

                // Build historical day record
                allDays.Add(new HistoricalWeatherDay
                {
                    Date = data.Date,
                    Year = data.Date.Year,
                    Temperature = data.Temperature,
                    Precipitation = data.Precipitation,
                    WindSpeed = data.WindSpeed,
                    Humidity = data.Humidity,
                    Classification = classification,
                    IsExtremeWeather = classification != "Normal"
                });

                // Collect stats data (avoiding multiple passes)
                if (data.Temperature.HasValue) temperatures.Add(data.Temperature.Value);
                if (data.Precipitation.HasValue) precipitations.Add(data.Precipitation.Value);
                if (data.WindSpeed.HasValue) windSpeeds.Add(data.WindSpeed.Value);
                if (data.Humidity.HasValue) humidities.Add(data.Humidity.Value);
            }

            var totalObservations = allDays.Count;

            if (totalObservations == 0)
            {
                return ("NoData", 0.0, 0, "No historical data available for this date.",
                       null, new List<HistoricalWeatherDay>(), 0,
                       new Dictionary<string, double>());
            }

            // Count all classifications in a single pass
            var weatherCounts = new Dictionary<string, int>
            {
                { "VeryHot", 0 },
                { "VeryCold", 0 },
                { "VeryWet", 0 },
                { "VeryWindy", 0 },
                { "Normal", 0 }
            };

            var extremeCount = 0;
            foreach (var day in allDays)
            {
                weatherCounts[day.Classification]++;
                if (day.IsExtremeWeather)
                    extremeCount++;
            }

            // Calculate probabilities for all classifications
            var allProbabilities = new Dictionary<string, double>();
            foreach (var kvp in weatherCounts)
            {
                allProbabilities[kvp.Key] = Math.Round((double)kvp.Value / totalObservations * 100.0, 2);
            }

            // Determine dominant weather pattern
            var sortedCounts = weatherCounts
                .OrderByDescending(kvp => kvp.Value)
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
                var mostCommon = sortedCounts.FirstOrDefault(kvp => kvp.Key != "Normal");
                if (mostCommon.Value > 0)
                {
                    dominantLabel = mostCommon.Key;
                    probability = allProbabilities[dominantLabel];
                    description = GenerateDescription(dominantLabel, mostCommon.Value, totalObservations);
                }
                else
                {
                    dominantLabel = "Normal";
                    probability = allProbabilities["Normal"];
                    description = "Historical data shows primarily normal weather conditions for this date.";
                }
            }

            var stats = CalculateStatsOptimized(temperatures, precipitations, windSpeeds, humidities);

            return (dominantLabel, probability, totalObservations, description, stats,
                    allDays, extremeCount, allProbabilities);
        }

        /// <summary>
        /// Priority-based classification (avoids list allocations)
        /// Classifies weather based on predefined thresholds
        /// </summary>
        private string ClassifyWeather(WeatherData data)
        {
            // Check in priority order (most severe first)
            if (data.Temperature.HasValue && data.Temperature.Value > HOT_THRESHOLD)
                return "VeryHot";

            if (data.Temperature.HasValue && data.Temperature.Value < COLD_THRESHOLD)
                return "VeryCold";

            if (data.Precipitation.HasValue && data.Precipitation.Value > WET_THRESHOLD)
                return "VeryWet";

            if (data.WindSpeed.HasValue && data.WindSpeed.Value > WINDY_THRESHOLD)
                return "VeryWindy";

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

        /// <summary>
        /// Direct calculation from collected lists (no LINQ overhead)
        /// </summary>
        private WeatherStats CalculateStatsOptimized(
            List<double> temperatures,
            List<double> precipitations,
            List<double> windSpeeds,
            List<double> humidities)
        {
            return new WeatherStats
            {
                AverageTemperature = CalculateAverage(temperatures),
                AveragePrecipitation = CalculateAverage(precipitations),
                AverageWindSpeed = CalculateAverage(windSpeeds),
                AverageHumidity = CalculateAverage(humidities),
                MinTemperature = CalculateMin(temperatures),
                MaxTemperature = CalculateMax(temperatures),
                MaxPrecipitation = CalculateMax(precipitations),
                MaxWindSpeed = CalculateMax(windSpeeds)
            };
        }

        private double? CalculateAverage(List<double> values)
        {
            if (values.Count == 0) return null;

            double sum = 0;
            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }
            return Math.Round(sum / values.Count, 2);
        }

        private double? CalculateMin(List<double> values)
        {
            if (values.Count == 0) return null;

            double min = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] < min) min = values[i];
            }
            return Math.Round(min, 2);
        }

        private double? CalculateMax(List<double> values)
        {
            if (values.Count == 0) return null;

            double max = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] > max) max = values[i];
            }
            return Math.Round(max, 2);
        }
    }
}