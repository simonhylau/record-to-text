namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Cross-platform file-picker and folder-picker abstraction.
/// Uses MAUI FilePicker / FolderPicker where available.
/// </summary>
public class FileDialogService
{
    // ──────────────────────────────────────────────────────────────────────────
    // Audio file picker
    // ──────────────────────────────────────────────────────────────────────────

    private static readonly FilePickerFileType AudioFileType = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI,       new[] { ".wav", ".mp3", ".mp4", ".m4a", ".ogg", ".flac", ".aac", ".wma", ".opus", ".webm" } },
            { DevicePlatform.MacCatalyst, new[] { "public.audio", "com.microsoft.waveform-audio", "public.mp3" } },
        });

    /// <summary>
    /// Opens a file picker restricted to common audio formats.
    /// Returns the selected file path, or <c>null</c> if the user cancelled.
    /// </summary>
    public async Task<string?> PickAudioFileAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select an Audio File",
                FileTypes = AudioFileType
            };
            var result = await FilePicker.Default.PickAsync(options);
            return result?.FullPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileDialogService] PickAudioFileAsync failed: {ex}");
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Save-as picker
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// On Windows/macOS the user chooses a save location; falls back to
    /// the app's output directory with a default name on other platforms.
    /// </summary>
    public async Task<string?> PickSaveFileAsync(string defaultFileName, string outputDirectory)
    {
        try
        {
#if WINDOWS
            // WinUI FileSavePicker via MAUI interop
            var savePicker = new FileSaveDialogHelper();
            return await savePicker.PickAsync(defaultFileName);
#else
            // Mac Catalyst: return a path in the app's Documents/output directory
            await Task.CompletedTask;
            Directory.CreateDirectory(outputDirectory);
            return Path.Combine(outputDirectory, defaultFileName);
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileDialogService] PickSaveFileAsync failed: {ex}");
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Open folder
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the given directory in Explorer (Windows) or Finder (macOS).
    /// </summary>
    public void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            System.Diagnostics.Debug.WriteLine($"[FileDialogService] Folder not found: {folderPath}");
            return;
        }

        try
        {
            var psi = System.Runtime.InteropServices.RuntimeInformation
                          .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{folderPath}\"")
                    { UseShellExecute = true }
                : new System.Diagnostics.ProcessStartInfo("open", $"\"{folderPath}\"")
                    { UseShellExecute = true };

            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileDialogService] OpenFolder failed: {ex}");
        }
    }
}
