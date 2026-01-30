using System;
using System.Threading.Tasks;
using DriveRPC.Shared.Models;

namespace DriveRPC.Shared.Services
{
    public interface ILocationService
    {
        bool IsListening { get; }
        Task StartListeningAsync();
        void StopListening();

        (float lat, float lon)? CurrentLocation { get; }
        double? HeadingDegrees { get; }
        double? SpeedMetersPerSecond { get; }
        PositionStatus CurrentStatus { get; }
        bool IsHeadingAvailable { get; }

        bool IsRecording { get; }
        void StartRecording();
        Task StopRecordingAsync(System.IO.Stream outputStream);

        bool IsReplaying { get; }
        TimeSpan ReplayDuration { get; }
        Task StartReplayAsync(System.IO.Stream inputStream);
        void StopReplay();
        void PauseReplay();
        void ResumeReplay();
        void SetReplaySpeed(double speed);
        void SeekReplay(double progress0to1);

        void InitializeWithStaleLocation((float lat, float lon) staleLocation);

        event EventHandler LocationUpdated;
        event EventHandler HeadingUpdated;
        event EventHandler<TimeSpan> RecordingTimerTick;
        event EventHandler<TimeSpan> ReplayTimeChanged;
        event EventHandler ReplayFinished;
    }
}