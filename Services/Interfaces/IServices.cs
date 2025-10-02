using ParadeGuard.Api.Models;

namespace ParadeGuard.Api.Services.Interfaces
{
    public interface IGeocodingService
    {
        Task<(double lat, double lon)> GetCoordinatesAsync(string place);
    }

    public interface INasaWeatherService
    {
        /// <summary>
        /// Gets historical weather data - maintains existing signature for backward compatibility
        /// Internally optimized with streaming JSON parsing
        /// </summary>
        Task<List<WeatherData>> GetHistoricalDataAsync(double lat, double lon, int years);
    }

    public interface IProbabilityCalculator
    {
        (string label, double probability, int observations, string description,
         WeatherStats stats, List<HistoricalWeatherDay> allDays, int extremeCount,
         Dictionary<string, double> allProbabilities)
        CalculateAutomatic(List<WeatherData> historical, DateTime targetDate);
    }
}