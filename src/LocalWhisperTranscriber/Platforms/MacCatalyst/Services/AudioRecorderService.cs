#if MACCATALYST
using AVFoundation;
using AudioToolbox;
using Foundation;

namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Mac Catalyst implementation of <see cref="IAudioRecorderService"/> using AVFoundation.
/// Records microphone input only (system audio loopback is not available on Mac Catalyst
/// without a virtual audio driver or screen recording entitlement).
/// </summary>
public class AudioRecorderService : IAudioRecorderService
{
    private AVAudioRecorder? _recorder;

    public bool IsRecording { get; private set; }
    public bool SupportsLoopback => false;

    // ──────────────────────────────────────────────────────────────────────────
    // Device enumeration
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> GetInputDevicesAsync()
    {
        // Request microphone permission via callback
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        AVAudioSession.SharedInstance().RequestRecordPermission(granted => tcs.TrySetResult(granted));
        await tcs.Task.ConfigureAwait(false);

        // Enumerate available audio inputs via the shared session
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSession.CategoryRecord, out _);
        session.SetActive(true, out _);

        var inputs = session.AvailableInputs;
        if (inputs is null || inputs.Length == 0)
            return new List<string> { "Default Microphone" };

        return inputs.Select(p => p.PortName).ToList();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Recording
    // ──────────────────────────────────────────────────────────────────────────

    public async Task StartRecordingAsync(string micOutputPath, string? deviceName, string? loopbackOutputPath = null)
    {
        var session = AVAudioSession.SharedInstance();

        // Select preferred input if a specific device was requested
        if (deviceName is not null)
        {
            var inputs = session.AvailableInputs;
            var preferred = inputs?.FirstOrDefault(p => p.PortName == deviceName);
            if (preferred is not null)
                session.SetPreferredInput(preferred, out _);
        }

        session.SetCategory(AVAudioSession.CategoryRecord, out _);
        session.SetActive(true, out _);

        var url = NSUrl.FromFilename(micOutputPath);
        var settings = new AudioSettings
        {
            SampleRate = 16000,
            Format = AudioFormatType.LinearPCM,
            NumberChannels = 1,
            LinearPcmBitDepth = 16
        };

        _recorder = AVAudioRecorder.Create(url, settings, out var error);
        if (error is not null)
            throw new InvalidOperationException($"Failed to create audio recorder: {error.LocalizedDescription}");

        _recorder!.PrepareToRecord();
        _recorder.Record();
        IsRecording = true;

        await Task.CompletedTask;
    }

    public Task StopRecordingAsync()
    {
        _recorder?.Stop();
        _recorder?.Dispose();
        _recorder = null;

        var session = AVAudioSession.SharedInstance();
        session.SetActive(false, out _);

        IsRecording = false;
        return Task.CompletedTask;
    }
}
#endif

