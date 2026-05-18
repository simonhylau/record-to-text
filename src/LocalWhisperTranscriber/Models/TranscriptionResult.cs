namespace LocalWhisperTranscriber.Models;

/// <summary>
/// Result returned after a transcription run.
/// </summary>
public class TranscriptionResult
{
    /// <summary>Whether the transcription succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>The transcribed text (plain, SRT, or JSON depending on the requested format).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Full path to the output file written by whisper-cli (if any).</summary>
    public string? OutputFilePath { get; set; }

    /// <summary>Error message if <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Raw stderr from whisper-cli (useful for diagnostics).</summary>
    public string? DiagnosticOutput { get; set; }

    /// <summary>Wall-clock time taken for the transcription in milliseconds.</summary>
    public long ElapsedMilliseconds { get; set; }

    public static TranscriptionResult FromError(string error, string? diagnostics = null) =>
        new() { Success = false, ErrorMessage = error, DiagnosticOutput = diagnostics };
}
