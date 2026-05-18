using LocalWhisperTranscriber.Models;
using LocalWhisperTranscriber.Services;

namespace LocalWhisperTranscriber;

public partial class MainPage : ContentPage
{
    // ──────────────────────────────────────────────────────────────────────────
    // Dependencies
    // ──────────────────────────────────────────────────────────────────────────
    private readonly IWhisperService _whisper;
    private readonly AudioConversionService _audio;
    private readonly FileDialogService _fileDialog;

    // ──────────────────────────────────────────────────────────────────────────
    // State
    // ──────────────────────────────────────────────────────────────────────────
    private string? _selectedAudioPath;
    private string? _lastOutputFilePath;
    private CancellationTokenSource? _cts;

    // Output directory: user app-data/LocalWhisperTranscriber/output
    private static readonly string OutputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalWhisperTranscriber", "output");

    private static readonly string TempDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalWhisperTranscriber", "temp");

    // Language code map (display → whisper language code)
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "auto",      "auto" },
        { "english",   "en"   },
        { "chinese",   "zh"   },
        { "cantonese", "yue"  }
    };

    // ──────────────────────────────────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────────────────────────────────
    public MainPage(IWhisperService whisper, AudioConversionService audio, FileDialogService fileDialog)
    {
        InitializeComponent();
        _whisper = whisper;
        _audio = audio;
        _fileDialog = fileDialog;

        // Set safe defaults
        PickerModel.SelectedIndex = 1;    // base
        PickerLanguage.SelectedIndex = 0; // auto
        PickerFormat.SelectedIndex = 0;   // txt
    }

    // ──────────────────────────────────────────────────────────────────────────
    // UI event handlers
    // ──────────────────────────────────────────────────────────────────────────

    private async void OnSelectFileClicked(object sender, EventArgs e)
    {
        var path = await _fileDialog.PickAudioFileAsync();
        if (path is null) return;

        _selectedAudioPath = path;
        LblSelectedFile.Text = Path.GetFileName(path);
        ToolTipProperties.SetText(LblSelectedFile, path);
        BtnTranscribe.IsEnabled = true;
        SetStatus("File selected. Ready to transcribe.");
    }

    private void OnOptionChanged(object sender, EventArgs e)
    {
        // nothing to do — options are read on Transcribe click
    }

    private async void OnTranscribeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedAudioPath))
        {
            await ShowErrorAsync("Please select an audio file first.");
            return;
        }

        await RunTranscriptionAsync();
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        _cts?.Cancel();
        SetStatus("Cancelling…");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var text = EditorTranscript.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            await ShowErrorAsync("There is no transcript to save.");
            return;
        }

        var format = SelectedFormat();
        var defaultName = $"transcript_{DateTime.Now:yyyyMMdd_HHmmss}.{format}";
        var savePath = await _fileDialog.PickSaveFileAsync(defaultName, OutputDirectory);
        if (savePath is null) return;

        try
        {
            await File.WriteAllTextAsync(savePath, text);
            _lastOutputFilePath = savePath;
            SetStatus($"Saved → {savePath}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to save: {ex.Message}");
        }
    }

    private void OnOpenFolderClicked(object sender, EventArgs e)
    {
        Directory.CreateDirectory(OutputDirectory);
        _fileDialog.OpenFolder(OutputDirectory);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Transcription pipeline
    // ──────────────────────────────────────────────────────────────────────────

    private async Task RunTranscriptionAsync()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetBusy(true);
        BtnSave.IsEnabled = false;
        EditorTranscript.Text = string.Empty;

        var progress = new Progress<string>(msg => MainThread.BeginInvokeOnMainThread(
            () => SetStatus(msg)));

        try
        {
            // 1. Ensure WAV
            var wavPath = await _audio.EnsureWavAsync(
                _selectedAudioPath!, TempDirectory, progress, ct);

            // 2. Build options
            var options = new TranscriptionOptions
            {
                AudioFilePath  = wavPath,
                Model          = SelectedModel(),
                Language       = SelectedLanguageCode(),
                OutputFormat   = SelectedFormat(),
                OutputDirectory = OutputDirectory,
                OutputBaseName = $"transcript_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            // 3. Transcribe
            var result = await _whisper.TranscribeAsync(options, progress, ct);

            // 4. Show result
            if (result.Success)
            {
                EditorTranscript.Text = result.Text;
                _lastOutputFilePath = result.OutputFilePath;
                BtnSave.IsEnabled = true;
                SetStatus($"✅ Done in {result.ElapsedMilliseconds / 1000.0:F1}s" +
                          (result.OutputFilePath is not null
                              ? $"  →  {Path.GetFileName(result.OutputFilePath)}"
                              : string.Empty));
            }
            else
            {
                SetStatus($"❌ {result.ErrorMessage}");
                if (!string.IsNullOrWhiteSpace(result.DiagnosticOutput))
                    EditorTranscript.Text = result.DiagnosticOutput;
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("⚠️ Cancelled.");
        }
        catch (FileNotFoundException fnf)
        {
            await ShowErrorAsync(fnf.Message);
            SetStatus($"❌ {fnf.Message}");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Unexpected error:\n{ex.Message}");
            SetStatus($"❌ Error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private string SelectedModel()
    {
        var item = PickerModel.SelectedItem as string ?? "base";
        return item;
    }

    private string SelectedLanguageCode()
    {
        var display = PickerLanguage.SelectedItem as string ?? "auto";
        return LanguageMap.TryGetValue(display, out var code) ? code : "auto";
    }

    private string SelectedFormat()
    {
        return PickerFormat.SelectedItem as string ?? "txt";
    }

    private void SetBusy(bool busy)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            BusyIndicator.IsRunning = busy;
            BusyIndicator.IsVisible = busy;
            BtnTranscribe.IsEnabled = !busy;
            BtnSelectFile.IsEnabled = !busy;
            BtnCancel.IsVisible = busy;
        });
    }

    private void SetStatus(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LblStatus.Text = message;
        });
    }

    private Task ShowErrorAsync(string message) =>
        DisplayAlertAsync("Error", message, "OK");
}
