using System.ComponentModel.DataAnnotations;

namespace ParadeGuard.Api.Models
{
    public class UserQuery
    {
        /// <summary>
        /// Location name - required if coordinates are not provided
        /// If provided, will be geocoded to get coordinates
        /// </summary>
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Location name must be between 2 and 100 characters")]
        public string? LocationName { get; set; }

        /// <summary>
        /// Latitude coordinate - optional if LocationName is provided
        /// </summary>
        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
        public double? Latitude { get; set; }

        /// <summary>
        /// Longitude coordinate - optional if LocationName is provided
        /// </summary>
        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
        public double? Longitude { get; set; }

        /// <summary>
        /// Target date for weather prediction (optional - defaults to today)
        /// </summary>
        public DateTime? TargetDate { get; set; }

        /// <summary>
        /// Validates that either LocationName or both coordinates are provided
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(LocationName) ||
                   (Latitude.HasValue && Longitude.HasValue);
        }

        /// <summary>
        /// Gets the effective target date (uses today if not specified)
        /// </summary>
        public DateTime GetEffectiveTargetDate()
        {
            return TargetDate ?? DateTime.Today;
        }
    }
}