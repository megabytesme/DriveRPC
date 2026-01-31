using System.Threading.Tasks;
using DriveRPC.Shared.Models;

namespace DriveRPC.Shared.Services
{
    public class NominatimGeocodingService : IGeocodingService
    {
        private readonly NominatimReverseGeocoder _geocoder = new NominatimReverseGeocoder();

        public Task<LocationInfo> ReverseGeocodeAsync(double lat, double lon)
            => _geocoder.LookupAsync(lat, lon);
    }
}