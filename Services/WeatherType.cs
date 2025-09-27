namespace ParadeGuard.Api.Services
{
    public enum WeatherType
    {
        /// <summary>
        /// Very hot conditions (default threshold: 35°C)
        /// </summary>
        VeryHot = 0,

        /// <summary>
        /// Very cold conditions (default threshold: 5°C)
        /// </summary>
        VeryCold = 1,

        /// <summary>
        /// Very wet conditions (default threshold: 10mm)
        /// </summary>
        VeryWet = 2,

        /// <summary>
        /// Very windy conditions (default threshold: 10 m/s)
        /// </summary>
        VeryWindy = 3
    }
}