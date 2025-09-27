using ParadeGuard.Api.Services;
using System.ComponentModel.DataAnnotations;

public class UserQuery
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Location name must be between 2 and 100 characters")]
    public string? LocationName { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public double? Longitude { get; set; }

    [Required(ErrorMessage = "Target date is required")]
    public DateTime TargetDate { get; set; }

    [Required(ErrorMessage = "Weather type is required")]
    [EnumDataType(typeof(WeatherType), ErrorMessage = "Invalid weather type")]
    public WeatherType WeatherType { get; set; } = WeatherType.VeryHot;

    [Range(0, double.MaxValue, ErrorMessage = "Threshold must be positive")]
    public double? Threshold { get; set; }

    [Range(1, 40, ErrorMessage = "Years must be between 1 and 40")]
    public int Years { get; set; } = 30;

    public bool IsValid()
    {
        return (Latitude.HasValue && Longitude.HasValue) || !string.IsNullOrWhiteSpace(LocationName);
    }

    public double GetEffectiveThreshold()
    {
        if (Threshold.HasValue) return Threshold.Value;
        return WeatherType switch
        {
            WeatherType.VeryHot => 35.0,
            WeatherType.VeryCold => 5.0,
            WeatherType.VeryWet => 10.0,
            WeatherType.VeryWindy => 10.0,
            _ => 0.0
        };
    }
}