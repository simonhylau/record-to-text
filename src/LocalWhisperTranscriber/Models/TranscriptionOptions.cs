namespace LocalWhisperTranscriber.Models;

/// <summary>
/// Options passed to the Whisper transcription engine.
/// </summary>
public class TranscriptionOptions
{
    /// <summary>
    /// Whisper model size: tiny | base | small
    /// </summary>
    public string Model { get; set; } = "base";

    /// <summary>
    /// Language code: auto | en | zh | yue
    /// </summary>
    public string Language { get; set; } = "auto";

    /// <summary>
    /// Output format: txt | srt | json
    /// </summary>
    public string OutputFormat { get; set; } = "txt";

    /// <summary>
    /// Full path to the input audio file (must be WAV by the time it reaches whisper-cli).
    /// </summary>
    public string AudioFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Directory where transcription output files will be written.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Base file name (without extension) for the output file.
    /// </summary>
    public string OutputBaseName { get; set; } = "transcript";

    /// <summary>
    /// Number of threads to use (0 = let whisper-cli decide).
    /// </summary>
    public int Threads { get; set; } = 0;
}
