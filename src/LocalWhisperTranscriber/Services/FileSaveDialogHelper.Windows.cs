#if WINDOWS
using Windows.Storage;
using Windows.Storage.Pickers;

namespace LocalWhisperTranscriber.Services;

/// <summary>
/// Windows-only helper that wraps the WinRT FileSavePicker.
/// </summary>
internal sealed class FileSaveDialogHelper
{
    public async Task<string?> PickAsync(string defaultFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(defaultFileName)
        };

        var ext = Path.GetExtension(defaultFileName);
        picker.FileTypeChoices.Add(GetFriendlyName(ext), new List<string> { ext });

        // Initialise the picker with the current window handle (required on WinUI 3)
        var hwnd = ((MauiWinUIWindow)Application.Current!.Windows[0].Handler.PlatformView!).WindowHandle;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private static string GetFriendlyName(string ext) => ext.ToLowerInvariant() switch
    {
        ".txt"  => "Text Files",
        ".srt"  => "SubRip Subtitle Files",
        ".json" => "JSON Files",
        _       => "Files"
    };
}
#endif
