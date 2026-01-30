using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DriveRPC.Shared.Models;
using Newtonsoft.Json;

namespace DriveRPC.Shared.Services
{
    public class SensorRecorder
    {
        private List<SensorEvent> _events = new List<SensorEvent>();
        private Stopwatch _recordStopwatch;
        private bool _isRecording;

        private bool _isPlaying;
        private bool _isPaused;
        private double _playbackSpeed = 1.0;
        private long _currentPlaybackTick = 0;
        private long _totalDurationTicks = 0;
        private int _lastEventIndex = 0;
        private CancellationTokenSource _playbackCts;

        public event Action<SensorEvent> OnGpsPlayback;
        public event Action<SensorEvent> OnOrientationPlayback;
        public event Action<SensorEvent> OnAccelPlayback;
        public event EventHandler<TimeSpan> PlaybackTimeChanged;
        public event EventHandler PlaybackFinished;
        public event EventHandler<TimeSpan> RecordingDurationUpdated;

        private Timer _durationTimer;

        public TimeSpan CurrentDuration =>
            _isRecording ? _recordStopwatch.Elapsed : TimeSpan.FromTicks(_totalDurationTicks);

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            _events.Clear();
            _recordStopwatch = Stopwatch.StartNew();
            _isRecording = true;

            _durationTimer = new Timer(
                (state) =>
                {
                    RecordingDurationUpdated?.Invoke(this, _recordStopwatch.Elapsed);
                },
                null,
                0,
                1000
            );
        }

        public void RecordGps(double lat, double lon, double? speed, double? course)
        {
            if (!_isRecording)
                return;

            _events.Add(new SensorEvent
            {
                Type = SensorType.Gps,
                TimestampTicks = _recordStopwatch.ElapsedTicks,
                Lat = lat,
                Lon = lon,
                Speed = speed,
                Course = course,
            });
        }

        public void RecordOrientation(float x, float y, float z, float w)
        {
            if (!_isRecording)
                return;

            _events.Add(new SensorEvent
            {
                Type = SensorType.Orientation,
                TimestampTicks = _recordStopwatch.ElapsedTicks,
                Qx = x,
                Qy = y,
                Qz = z,
                Qw = w,
            });
        }

        public async Task StopAndSaveAsync(Stream outputStream)
        {
            _isRecording = false;
            _recordStopwatch?.Stop();

            string json = JsonConvert.SerializeObject(_events);

            using (var writer = new StreamWriter(
                outputStream,
                System.Text.Encoding.UTF8,
                1024,
                true))
            {
                await writer.WriteAsync(json);
                await writer.FlushAsync();
            }
        }

        public async Task LoadAsync(Stream inputStream)
        {
            string json;

            using (var reader = new StreamReader(
                inputStream,
                System.Text.Encoding.UTF8,
                true,
                1024,
                true))
            {
                json = await reader.ReadToEndAsync();
            }

            _events = JsonConvert.DeserializeObject<List<SensorEvent>>(json);

            if (_events.Count > 0)
            {
                long startOffset = _events[0].TimestampTicks;
                foreach (var e in _events)
                    e.TimestampTicks -= startOffset;

                _totalDurationTicks = _events.Last().TimestampTicks;
            }

            _currentPlaybackTick = 0;
            _lastEventIndex = 0;
        }

        public void Play()
        {
            if (_isPlaying)
                return;

            _isPlaying = true;
            _isPaused = false;
            _playbackCts = new CancellationTokenSource();
            _ = PlaybackLoop(_playbackCts.Token);
        }

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;
        public void SetSpeed(double speed) => _playbackSpeed = speed;

        public void Stop()
        {
            _isPlaying = false;
            _playbackCts?.Cancel();
        }

        public void SeekTo(double progress0to1)
        {
            _currentPlaybackTick = (long)(_totalDurationTicks * progress0to1);
            _lastEventIndex = 0;

            SensorEvent lastGps = null;
            SensorEvent lastOrient = null;

            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].TimestampTicks > _currentPlaybackTick)
                {
                    _lastEventIndex = i;
                    break;
                }

                if (_events[i].Type == SensorType.Gps)
                    lastGps = _events[i];
                if (_events[i].Type == SensorType.Orientation)
                    lastOrient = _events[i];
            }

            if (lastGps != null)
                OnGpsPlayback?.Invoke(lastGps);
            if (lastOrient != null)
                OnOrientationPlayback?.Invoke(lastOrient);

            PlaybackTimeChanged?.Invoke(this, TimeSpan.FromTicks(_currentPlaybackTick));
        }

        private async Task PlaybackLoop(CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            long lastSystemTicks = stopwatch.ElapsedTicks;

            while (_isPlaying && !token.IsCancellationRequested)
            {
                long currentSystemTicks = stopwatch.ElapsedTicks;
                long deltaSystemTicks = currentSystemTicks - lastSystemTicks;
                lastSystemTicks = currentSystemTicks;

                if (_isPaused)
                {
                    await Task.Delay(30);
                    continue;
                }

                _currentPlaybackTick += (long)(deltaSystemTicks * _playbackSpeed);

                if (_currentPlaybackTick >= _totalDurationTicks)
                {
                    _currentPlaybackTick = _totalDurationTicks;
                    _isPlaying = false;
                    PlaybackFinished?.Invoke(this, EventArgs.Empty);
                    break;
                }

                while (_lastEventIndex < _events.Count &&
                       _events[_lastEventIndex].TimestampTicks <= _currentPlaybackTick)
                {
                    var evt = _events[_lastEventIndex];

                    switch (evt.Type)
                    {
                        case SensorType.Gps:
                            OnGpsPlayback?.Invoke(evt);
                            break;

                        case SensorType.Orientation:
                            OnOrientationPlayback?.Invoke(evt);
                            break;
                    }

                    _lastEventIndex++;
                }

                PlaybackTimeChanged?.Invoke(this, TimeSpan.FromTicks(_currentPlaybackTick));

                await Task.Delay(16);
            }
        }

        public void StopPlayback()
        {
            _isPlaying = false;
            _playbackCts?.Cancel();
        }
    }
}