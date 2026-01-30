using System;
using System.Threading.Tasks;

namespace DriveRPC.Shared.Models
{
    public enum OobePermissionStatus
    {
        Allowed,
        Denied,
        Restricted
    }

    public enum SpeedLodMode
    {
        Off,
        ExactSpeed,
        SpeedRange,
        Emoji
    }

    public enum LocationLodMode
    {
        Country,
        Region,
        City,
        Town,
        Road
    }

    public enum SensorType
    {
        Gps,
        Orientation,
        Accelerometer,
    }

    public enum HeadingMode
    {
        Hybrid,
        GpsOnly,
        SensorOnly,
    }

    public enum GpsSource
    {
        Live,
        Replay
    }

    public enum SpeedUnit
    {
        Auto,
        Kmh,
        Mph
    }

    public enum PositionStatus
    {
        Ready,
        Initializing,
        NoData,
        Disabled,
        NotInitialized,
        NotAvailable
    }
}