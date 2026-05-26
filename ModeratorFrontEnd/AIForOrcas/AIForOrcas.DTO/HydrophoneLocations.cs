using System.Collections.Generic;
using System.Linq;

namespace AIForOrcas.DTO
{
    /// <summary>
    /// Static mapping of hydrophone locations to their corresponding IDs.
    /// Initialized at application startup.
    /// </summary>
    public static class HydrophoneLocations
    {
        private static IReadOnlyDictionary<string, string> _locationToIdMap = new Dictionary<string, string>
        {
            // Default hard-coded values
            { "Andrews Bay", "rpi_andrews_bay" },
            { "Bush Point", "rpi_bush_point" },
            { "MaST Center", "rpi_mast_center" },
            { "North San Juan Channel", "rpi_north_sjc" },
            { "Orcasound Lab", "rpi_orcasound_lab" },
            { "Point Robinson", "rpi_point_robinson" },
            { "Port Townsend", "rpi_port_townsend" },
            { "Sunset Bay", "rpi_sunset_bay" },
        };

        /// <summary>
        /// Initializes the hydrophone location map from an API or other source.
        /// Should be called once at application startup.
        /// </summary>
        /// <param name="locationMap">The location-to-ID mapping to use.</param>
        public static void Initialize(IReadOnlyDictionary<string, string> locationMap)
        {
            if (locationMap != null && locationMap.Any())
            {
                _locationToIdMap = locationMap;
            }
        }

        /// <summary>
        /// Gets the hydrophone ID for a given location name.
        /// </summary>
        /// <param name="location">The location name.</param>
        /// <returns>The hydrophone ID, or null if not found.</returns>
        public static string GetIdByLocation(string location)
        {
            return _locationToIdMap.TryGetValue(location, out var id) ? id : null;
        }

        /// <summary>
        /// Gets all available location names.
        /// </summary>
        public static IEnumerable<string> Locations => _locationToIdMap.Keys;
    }
}
