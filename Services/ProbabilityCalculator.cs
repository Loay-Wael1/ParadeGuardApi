using ParadeGuard.Api.Models;
using ParadeGuard.Api.Services.Interfaces;

namespace ParadeGuard.Api.Services
{
    public class ProbabilityCalculator : IProbabilityCalculator
    {
        public (string label, double probability, int observations, string description, WeatherStats stats, List<MatchingWeatherDay> matchingDays) Calculate(
            List<WeatherData> historical,
            DateTime targetDate,
            WeatherType weatherType,
            double? threshold)
        {
            var relevantData = historical
                .Where(d => d.Date.Month == targetDate.Month && d.Date.Day == targetDate.Day)
                .ToList();

            var totalObservations = relevantData.Count;

            if (totalObservations == 0)
            {
                return ("NoData", 0.0, 0, "No historical data available for this date.", null, new List<MatchingWeatherDay>());
            }

            var (conditionCount, actualThreshold, description, matchingDays) = CountConditionMatches(relevantData, weatherType, threshold);
            var probability = Math.Round((double)conditionCount / totalObservations * 100.0, 2);
            var stats = CalculateStats(relevantData);

            return (weatherType.ToString(), probability, totalObservations, description, stats, matchingDays);
        }

        private (int count, double threshold, string description, List<MatchingWeatherDay> matchingDays) CountConditionMatches(
            List<WeatherData> data, WeatherType weatherType, double? userThreshold)
        {
            return weatherType switch
            {
                WeatherType.VeryHot => CountHotDays(data, userThreshold ?? 35.0),
                WeatherType.VeryCold => CountColdDays(data, userThreshold ?? 5.0),
                WeatherType.VeryWet => CountWetDays(data, userThreshold ?? 10.0),
                WeatherType.VeryWindy => CountWindyDays(data, userThreshold ?? 10.0),
                _ => (0, 0.0, "Unknown weather type", new List<MatchingWeatherDay>())
            };
        }

        private (int count, double threshold, string description, List<MatchingWeatherDay> matchingDays) CountHotDays(List<WeatherData> data, double threshold)
        {
            var matchingDays = data
                .Where(d => d.Temperature.HasValue && d.Temperature.Value > threshold)
                .Select(d => new MatchingWeatherDay
                {
                    Date = d.Date,
                    Year = d.Date.Year,
                    Value = d.Temperature!.Value,
                    Threshold = threshold,
                    WeatherType = "VeryHot",
                    Unit = "°C",
                    Context = new WeatherDayContext
                    {
                        Temperature = d.Temperature,
                        Precipitation = d.Precipitation,
                        WindSpeed = d.WindSpeed,
                        Humidity = d.Humidity
                    }
                })
                .OrderByDescending(d => d.Value)
                .ToList();

            return (matchingDays.Count, threshold, $"Probability of temperature exceeding {threshold}°C", matchingDays);
        }

        private (int count, double threshold, string description, List<MatchingWeatherDay> matchingDays) CountColdDays(List<WeatherData> data, double threshold)
        {
            var matchingDays = data
                .Where(d => d.Temperature.HasValue && d.Temperature.Value < threshold)
                .Select(d => new MatchingWeatherDay
                {
                    Date = d.Date,
                    Year = d.Date.Year,
                    Value = d.Temperature!.Value,
                    Threshold = threshold,
                    WeatherType = "VeryCold",
                    Unit = "°C",
                    Context = new WeatherDayContext
                    {
                        Temperature = d.Temperature,
                        Precipitation = d.Precipitation,
                        WindSpeed = d.WindSpeed,
                        Humidity = d.Humidity
                    }
                })
                .OrderBy(d => d.Value)
                .ToList();

            return (matchingDays.Count, threshold, $"Probability of temperature dropping below {threshold}°C", matchingDays);
        }

        private (int count, double threshold, string description, List<MatchingWeatherDay> matchingDays) CountWetDays(List<WeatherData> data, double threshold)
        {
            var matchingDays = data
                .Where(d => d.Precipitation.HasValue && d.Precipitation.Value > threshold)
                .Select(d => new MatchingWeatherDay
                {
                    Date = d.Date,
                    Year = d.Date.Year,
                    Value = d.Precipitation!.Value,
                    Threshold = threshold,
                    WeatherType = "VeryWet",
                    Unit = "mm",
                    Context = new WeatherDayContext
                    {
                        Temperature = d.Temperature,
                        Precipitation = d.Precipitation,
                        WindSpeed = d.WindSpeed,
                        Humidity = d.Humidity
                    }
                })
                .OrderByDescending(d => d.Value)
                .ToList();

            return (matchingDays.Count, threshold, $"Probability of precipitation exceeding {threshold} mm", matchingDays);
        }

        private (int count, double threshold, string description, List<MatchingWeatherDay> matchingDays) CountWindyDays(List<WeatherData> data, double threshold)
        {
            var matchingDays = data
                .Where(d => d.WindSpeed.HasValue && d.WindSpeed.Value > threshold)
                .Select(d => new MatchingWeatherDay
                {
                    Date = d.Date,
                    Year = d.Date.Year,
                    Value = d.WindSpeed!.Value,
                    Threshold = threshold,
                    WeatherType = "VeryWindy",
                    Unit = "m/s",
                    Context = new WeatherDayContext
                    {
                        Temperature = d.Temperature,
                        Precipitation = d.Precipitation,
                        WindSpeed = d.WindSpeed,
                        Humidity = d.Humidity
                    }
                })
                .OrderByDescending(d => d.Value)
                .ToList();

            return (matchingDays.Count, threshold, $"Probability of wind speed exceeding {threshold} m/s", matchingDays);
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