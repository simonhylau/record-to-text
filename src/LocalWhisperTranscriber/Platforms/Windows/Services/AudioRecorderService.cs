#if WINDOWS
using NAudio.Wave;

namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Windows implementation of <see cref="IAudioRecorderService"/> using NAudio.
/// Captures microphone input via WaveInEvent and optionally system audio via WASAPI loopback.
/// </summary>
public class AudioRecorderService : IAudioRecorderService
{
    private WaveInEvent? _micCapture;
    private WaveFileWriter? _micWriter;
    private WasapiLoopbackCapture? _loopbackCapture;
    private WaveFileWriter? _loopbackWriter;

    public bool IsRecording { get; private set; }
    public bool SupportsLoopback => true;

    // ──────────────────────────────────────────────────────────────────────────
    // Device enumeration
    // ──────────────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<string>> GetInputDevicesAsync()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add(caps.ProductName);
        }
        return Task.FromResult<IReadOnlyList<string>>(devices);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Recording
    // ──────────────────────────────────────────────────────────────────────────

    public Task StartRecordingAsync(string micOutputPath, string? deviceName, string? loopbackOutputPath = null)
    {
        // Resolve device index
        int deviceIndex = -1; // -1 = default
        if (deviceName is not null)
        {
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                if (WaveInEvent.GetCapabilities(i).ProductName == deviceName)
                {
                    deviceIndex = i;
                    break;
                }
            }
        }

        // Microphone
        _micCapture = new WaveInEvent
        {
            DeviceNumber = Math.Max(deviceIndex, 0),
            WaveFormat = new WaveFormat(16000, 16, 1)
        };
        _micWriter = new WaveFileWriter(micOutputPath, _micCapture.WaveFormat);
        _micCapture.DataAvailable += (_, e) =>
        {
            _micWriter?.Write(e.Buffer, 0, e.BytesRecorded);
        };
        _micCapture.StartRecording();

        // System audio (loopback)
        if (loopbackOutputPath is not null)
        {
            _loopbackCapture = new WasapiLoopbackCapture();
            _loopbackWriter = new WaveFileWriter(loopbackOutputPath, _loopbackCapture.WaveFormat);
            _loopbackCapture.DataAvailable += (_, e) =>
            {
                _loopbackWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            };
            _loopbackCapture.StartRecording();
        }

        IsRecording = true;
        return Task.CompletedTask;
    }

    public async Task StopRecordingAsync()
    {
        // Stop and flush microphone
        if (_micCapture is not null)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _micCapture.RecordingStopped += (_, _) => tcs.TrySetResult(true);
            _micCapture.StopRecording();
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            _micCapture.Dispose();
            _micCapture = null;
        }
        _micWriter?.Dispose();
        _micWriter = null;

        // Stop and flush loopback
        if (_loopbackCapture is not null)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _loopbackCapture.RecordingStopped += (_, _) => tcs.TrySetResult(true);
            _loopbackCapture.StopRecording();
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            _loopbackCapture.Dispose();
            _loopbackCapture = null;
        }
        _loopbackWriter?.Dispose();
        _loopbackWriter = null;

        IsRecording = false;
    }
}
#endif
