using DriveRPC.Shared.Models;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Services
{
    public interface IGeocodingService
    {
        Task<LocationInfo> ReverseGeocodeAsync(double lat, double lon);
    }
}