using LocalWhisperTranscriber.Models;

namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Contract for a service that runs whisper.cpp transcription.
/// </summary>
public interface IWhisperService
{
    /// <summary>
    /// Transcribes the audio file described by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Transcription configuration.</param>
    /// <param name="progress">Optional progress reporter for status messages.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<TranscriptionResult> TranscribeAsync(
        TranscriptionOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full path to the model file for the given model name.
    /// Returns <c>null</c> if the file is not found.
    /// </summary>
    string? FindModelPath(string modelName);

    /// <summary>
    /// Returns the full path to the whisper-cli executable, or <c>null</c> if not found.
    /// </summary>
    string? FindExecutablePath();
}
