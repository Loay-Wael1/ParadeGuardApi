using ParadeGuard.Api.Models;

namespace ParadeGuard.Api.Services.Interfaces
{
    public interface IGeocodingService
    {
        Task<(double lat, double lon)> GetCoordinatesAsync(string place);
    }

    public interface INasaWeatherService
    {
        Task<List<WeatherData>> GetHistoricalDataAsync(double lat, double lon, int years);
    }

    public interface IProbabilityCalculator
    {
        (string label, double probability, int observations, string description, WeatherStats stats, List<MatchingWeatherDay> matchingDays) Calculate(
            List<WeatherData> historical, DateTime targetDate, WeatherType weatherType, double? threshold);
    }
}