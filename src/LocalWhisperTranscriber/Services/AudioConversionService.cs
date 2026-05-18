using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Converts audio files to the 16 kHz mono WAV format required by whisper.cpp,
/// using a locally bundled ffmpeg binary.
/// </summary>
public class AudioConversionService
{
    private static readonly HashSet<string> WavExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".wav" };

    private static string FfmpegName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

    private static string NativeSubfolder =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "macos";

    // ──────────────────────────────────────────────────────────────────────────
    // Executable discovery
    // ──────────────────────────────────────────────────────────────────────────

    private IEnumerable<string> FfmpegSearchPaths()
    {
        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "Native", NativeSubfolder);
        var projectNative = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Native", NativeSubfolder);
        yield return Path.GetFullPath(projectNative);
    }

    public string? FindFfmpegPath()
    {
        foreach (var dir in FfmpegSearchPaths())
        {
            var path = Path.Combine(dir, FfmpegName);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Conversion
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// If <paramref name="inputPath"/> is already a WAV file, returns it unchanged.
    /// Otherwise converts it to 16 kHz mono WAV and returns the path to the new file.
    /// </summary>
    /// <param name="inputPath">Source audio file path.</param>
    /// <param name="tempDirectory">Directory for temporary converted files.</param>
    /// <param name="progress">Optional status reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to a 16 kHz mono WAV file ready for whisper-cli.</returns>
    public async Task<string> EnsureWavAsync(
        string inputPath,
        string tempDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var ext = Path.GetExtension(inputPath);
        if (WavExtensions.Contains(ext))
        {
            progress?.Report("Input is already a WAV file — skipping conversion.");
            return inputPath;
        }

        var ffmpeg = FindFfmpegPath()
            ?? throw new FileNotFoundException(
                $"ffmpeg executable not found. Place '{FfmpegName}' in the app folder " +
                $"or Native/{NativeSubfolder}/. See README.md for details.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            EnsureExecutable(ffmpeg);

        Directory.CreateDirectory(tempDirectory);

        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + "_converted.wav";
        var outputPath = Path.Combine(tempDirectory, outputFileName);

        // Remove stale converted file
        if (File.Exists(outputPath)) File.Delete(outputPath);

        progress?.Report($"Converting {Path.GetFileName(inputPath)} → WAV (16 kHz mono)…");

        var stdErr = new StringBuilder();
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            // -y: overwrite, -ar 16000: 16 kHz, -ac 1: mono, -c:a pcm_s16le: standard WAV encoding
            Arguments = $"-y -i \"{inputPath}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{outputPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdErr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new OperationCanceledException("Audio conversion was cancelled.");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg failed (exit code {process.ExitCode}):\n{stdErr}");

        if (!File.Exists(outputPath))
            throw new InvalidOperationException(
                "ffmpeg reported success but output file was not created.");

        progress?.Report("Audio conversion complete.");
        return outputPath;
    }

    /// <summary>
    /// Mixes two mono 16 kHz WAV files into a single output WAV using ffmpeg's amix filter.
    /// </summary>
    public async Task<string> MixAudioFilesAsync(
        string pathA,
        string pathB,
        string outputPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ffmpeg = FindFfmpegPath()
            ?? throw new FileNotFoundException(
                $"ffmpeg executable not found. Place '{FfmpegName}' in the app folder or Native/{NativeSubfolder}/.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            EnsureExecutable(ffmpeg);

        if (File.Exists(outputPath)) File.Delete(outputPath);

        progress?.Report("Mixing microphone and system audio…");

        var stdErr = new StringBuilder();
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-y -i \"{pathA}\" -i \"{pathB}\" " +
                        $"-filter_complex \"[0:a][1:a]amix=inputs=2:duration=first:dropout_transition=0\" " +
                        $"-ar 16000 -ac 1 -c:a pcm_s16le \"{outputPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdErr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new OperationCanceledException("Audio mixing was cancelled.");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg mix failed (exit code {process.ExitCode}):\n{stdErr}");

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("ffmpeg reported success but mixed output file was not created.");

        progress?.Report("Audio mixing complete.");
        return outputPath;
    }

    private static void EnsureExecutable(string path)
    {
        try
        {
            using var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            chmod?.WaitForExit();
        }
        catch { /* best-effort */ }
    }
}
