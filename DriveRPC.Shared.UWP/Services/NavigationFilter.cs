using DriveRPC.Shared.Models;
using System;
using System.Numerics;
using Windows.Devices.Sensors;

namespace DriveRPC.Shared.UWP.Services
{
    public class NavigationFilter
    {
        public bool IsInitialized { get; private set; } = false;
        public Vector2 Position { get; private set; }
        public double? Heading { get; private set; }
        public double Speed { get; private set; }

        public HeadingMode Mode { get; set; } = HeadingMode.Hybrid;

        private double _smoothedCompassHeading = 0;
        private bool _hasCompassSignal = false;

        private double QuaternionToHeading(SensorQuaternion q)
        {
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            double yawRadians = Math.Atan2(siny_cosp, cosy_cosp);
            double yawDegrees = yawRadians * (180.0 / Math.PI);
            double correctedDegrees = 360 - yawDegrees;
            return (correctedDegrees + 360) % 360;
        }

        public void Initialize(Vector2 initialPosition, double? initialHeading)
        {
            Position = initialPosition;
            Heading = initialHeading;
            if (initialHeading.HasValue)
                _smoothedCompassHeading = initialHeading.Value;
            Speed = 0;
            IsInitialized = true;
        }

        public void SetHeading(OrientationSensorReading orient, double displayCorrection)
        {
            if (!IsInitialized)
                return;

            if (Mode == HeadingMode.GpsOnly)
                return;
            if (Mode == HeadingMode.Hybrid && Speed > 4.0)
                return;

            double rawCompass = QuaternionToHeading(orient.Quaternion);
            double targetHeading = (rawCompass + displayCorrection + 360) % 360;

            if (!_hasCompassSignal)
            {
                _smoothedCompassHeading = targetHeading;
                _hasCompassSignal = true;
            }
            else
            {
                _smoothedCompassHeading = SmoothAngles(
                    _smoothedCompassHeading,
                    targetHeading,
                    0.05
                );
            }

            Heading = _smoothedCompassHeading;
        }

        public void SetHeadingRaw(float x, float y, float z, float w, double displayCorrection)
        {
            if (!IsInitialized)
                return;
            if (Mode == HeadingMode.GpsOnly)
                return;
            if (Mode == HeadingMode.Hybrid && Speed > 4.0)
                return;

            double siny_cosp = 2 * (w * z + x * y);
            double cosy_cosp = 1 - 2 * (y * y + z * z);
            double yawRadians = Math.Atan2(siny_cosp, cosy_cosp);
            double yawDegrees = yawRadians * (180.0 / Math.PI);
            double raw = 360 - yawDegrees;

            Heading = (raw + displayCorrection + 360) % 360;
        }

        public void Update(Vector2 gpsPosition, double? speed, double? gpsHeading)
        {
            if (!IsInitialized)
            {
                Initialize(gpsPosition, null);
                return;
            }

            Position = gpsPosition;

            if (speed.HasValue)
                Speed = speed.Value;

            bool validGpsHeading = gpsHeading.HasValue && !double.IsNaN(gpsHeading.Value);

            if (Mode != HeadingMode.SensorOnly && validGpsHeading)
            {
                if (Mode == HeadingMode.GpsOnly || (Mode == HeadingMode.Hybrid && Speed > 4.0))
                {
                    Heading = gpsHeading.Value;

                    _smoothedCompassHeading = Heading.Value;
                }
            }
        }

        private double SmoothAngles(double current, double target, double factor)
        {
            double delta = target - current;
            if (delta > 180)
                delta -= 360;
            if (delta < -180)
                delta += 360;
            return (current + delta * factor + 360) % 360;
        }
    }
}
