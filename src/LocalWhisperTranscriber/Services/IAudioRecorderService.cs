namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Platform-agnostic audio recorder: enumerates input devices and records mic / system audio.
/// </summary>
public interface IAudioRecorderService
{
    /// <summary>True while a recording session is active.</summary>
    bool IsRecording { get; }

    /// <summary>
    /// True if the platform supports capturing system (loopback) audio in addition to the mic.
    /// Windows only via WASAPI loopback.
    /// </summary>
    bool SupportsLoopback { get; }

    /// <summary>
    /// Returns the display names of available audio input devices.
    /// </summary>
    Task<IReadOnlyList<string>> GetInputDevicesAsync();

    /// <summary>
    /// Starts recording.
    /// </summary>
    /// <param name="micOutputPath">WAV file to write microphone audio to.</param>
    /// <param name="deviceName">Input device name, or null for the system default.</param>
    /// <param name="loopbackOutputPath">
    /// WAV file for system audio, or null to skip loopback capture.
    /// </param>
    Task StartRecordingAsync(string micOutputPath, string? deviceName, string? loopbackOutputPath = null);

    /// <summary>
    /// Stops the current recording and flushes all output files.
    /// </summary>
    Task StopRecordingAsync();
}
