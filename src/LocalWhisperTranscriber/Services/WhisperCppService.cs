using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LocalWhisperTranscriber.Models;

namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Runs whisper-cli (whisper.cpp) as a child process to perform local transcription.
/// </summary>
public class WhisperCppService : IWhisperService
{
    // ──────────────────────────────────────────────────────────────────────────
    // Executable / model discovery
    // ──────────────────────────────────────────────────────────────────────────

    private static string NativeSubfolder =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "macos";

    private static string ExecutableName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "whisper-cli.exe" : "whisper-cli";

    /// <summary>
    /// Ordered list of directories to search for the whisper-cli binary.
    /// </summary>
    private IEnumerable<string> ExecutableSearchPaths()
    {
        // 1. Next to the running assembly (publish output)
        yield return AppContext.BaseDirectory;

        // 2. Native/<platform> subfolder relative to the assembly
        yield return Path.Combine(AppContext.BaseDirectory, "Native", NativeSubfolder);

        // 3. The project's Native/<platform> folder (for dev-time run)
        var projectNative = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Native", NativeSubfolder);
        yield return Path.GetFullPath(projectNative);
    }

    /// <inheritdoc />
    public string? FindExecutablePath()
    {
        foreach (var dir in ExecutableSearchPaths())
        {
            var path = Path.Combine(dir, ExecutableName);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <inheritdoc />
    public string? FindModelPath(string modelName)
    {
        // Normalise: "base" → "ggml-base.bin", "ggml-base.bin" stays as-is
        var fileName = modelName.StartsWith("ggml-", StringComparison.OrdinalIgnoreCase)
            ? modelName
            : $"ggml-{modelName}.bin";

        foreach (var dir in ExecutableSearchPaths())
        {
            // models/ subfolder next to the executable
            var path = Path.Combine(dir, "models", fileName);
            if (File.Exists(path)) return path;

            // Direct next to the executable (less common)
            path = Path.Combine(dir, fileName);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Transcription
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        TranscriptionOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // --- Validate -------------------------------------------------------
        var exePath = FindExecutablePath()
            ?? throw new FileNotFoundException(
                $"whisper-cli executable not found. Place '{ExecutableName}' in the app folder " +
                $"or in Native/{NativeSubfolder}/. See README.md for details.");

        var modelPath = FindModelPath(options.Model)
            ?? throw new FileNotFoundException(
                $"Whisper model file 'ggml-{options.Model}.bin' not found in any models/ folder. " +
                "Download the model and place it in the models/ directory next to whisper-cli. See README.md.");

        if (!File.Exists(options.AudioFilePath))
            throw new FileNotFoundException($"Audio file not found: {options.AudioFilePath}");

        Directory.CreateDirectory(options.OutputDirectory);

        // --- Build arguments ------------------------------------------------
        var outputBase = Path.Combine(options.OutputDirectory, options.OutputBaseName);
        var args = BuildArguments(modelPath, options.AudioFilePath, outputBase, options);

        progress?.Report($"Starting whisper-cli…");
        progress?.Report($"Model: {options.Model}  Language: {options.Language}  Format: {options.OutputFormat}");

        // --- Make executable on macOS ---------------------------------------
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            EnsureExecutable(exePath);

        // --- Launch process -------------------------------------------------
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdOut.AppendLine(e.Data);
            progress?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdErr.AppendLine(e.Data);
            // whisper-cli writes progress to stderr – forward as status
            progress?.Report(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait with cancellation support
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        sw.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return TranscriptionResult.FromError("Transcription was cancelled.");
        }

        if (process.ExitCode != 0)
        {
            var errText = stdErr.ToString().Trim();
            return TranscriptionResult.FromError(
                $"whisper-cli exited with code {process.ExitCode}. {errText}",
                errText);
        }

        // --- Read output file -----------------------------------------------
        var outputFile = FindOutputFile(outputBase, options.OutputFormat);
        if (outputFile is null || !File.Exists(outputFile))
        {
            // Fall back to stdout
            var fallback = stdOut.ToString().Trim();
            if (string.IsNullOrWhiteSpace(fallback))
                return TranscriptionResult.FromError(
                    "whisper-cli completed but produced no output file or stdout.",
                    stdErr.ToString());

            return new TranscriptionResult
            {
                Success = true,
                Text = fallback,
                DiagnosticOutput = stdErr.ToString(),
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }

        var text = await File.ReadAllTextAsync(outputFile, cancellationToken).ConfigureAwait(false);

        return new TranscriptionResult
        {
            Success = true,
            Text = text,
            OutputFilePath = outputFile,
            DiagnosticOutput = stdErr.ToString(),
            ElapsedMilliseconds = sw.ElapsedMilliseconds
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string BuildArguments(
        string modelPath, string audioPath, string outputBase, TranscriptionOptions options)
    {
        var sb = new StringBuilder();

        // Model
        sb.Append($"-m \"{modelPath}\" ");

        // Input file
        sb.Append($"-f \"{audioPath}\" ");

        // Language
        if (!string.IsNullOrWhiteSpace(options.Language) &&
            !options.Language.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($"-l {options.Language} ");
        }

        // Output format flag
        switch (options.OutputFormat.ToLowerInvariant())
        {
            case "srt":
                sb.Append("-osrt ");
                break;
            case "json":
                sb.Append("-ojson ");
                break;
            default: // txt
                sb.Append("-otxt ");
                break;
        }

        // Output base path (whisper-cli appends .txt / .srt / .json)
        sb.Append($"-of \"{outputBase}\" ");

        // Threads (if specified)
        if (options.Threads > 0)
            sb.Append($"-t {options.Threads} ");

        return sb.ToString().TrimEnd();
    }

    private static string? FindOutputFile(string outputBase, string format)
    {
        var ext = format.ToLowerInvariant() switch
        {
            "srt"  => ".srt",
            "json" => ".json",
            _      => ".txt"
        };
        var path = outputBase + ext;
        return File.Exists(path) ? path : null;
    }

    private static void EnsureExecutable(string path)
    {
        try
        {
            // chmod +x equivalent via bash
            using var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            chmod?.WaitForExit();
        }
        catch
        {
            // Best-effort; if it already has execute permission this is a no-op
        }
    }
}
