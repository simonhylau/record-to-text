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
    private readonly IAudioRecorderService _recorder;

    // ──────────────────────────────────────────────────────────────────────────
    // State
    // ──────────────────────────────────────────────────────────────────────────
    private string? _selectedAudioPath;
    private string? _lastOutputFilePath;
    private CancellationTokenSource? _cts;

    // Recording state
    private CancellationTokenSource? _recordingTimerCts;
    private string? _micTempPath;
    private string? _loopbackTempPath;

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
    public MainPage(IWhisperService whisper, AudioConversionService audio,
                    FileDialogService fileDialog, IAudioRecorderService recorder)
    {
        InitializeComponent();
        _whisper  = whisper;
        _audio    = audio;
        _fileDialog = fileDialog;
        _recorder = recorder;

        // Set safe defaults
        PickerModel.SelectedIndex    = 1; // base
        PickerLanguage.SelectedIndex = 0; // auto
        PickerFormat.SelectedIndex   = 0; // txt

        // Show system-audio panel only on platforms that support loopback
        SystemAudioPanel.IsVisible = _recorder.SupportsLoopback;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Page lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadInputDevicesAsync();
    }

    private async Task LoadInputDevicesAsync()
    {
        try
        {
            var devices = await _recorder.GetInputDevicesAsync();
            PickerInputDevice.Items.Clear();
            foreach (var d in devices)
                PickerInputDevice.Items.Add(d);
            if (PickerInputDevice.Items.Count > 0)
                PickerInputDevice.SelectedIndex = 0;
        }
        catch
        {
            // If enumeration fails (e.g. permission denied), leave the picker empty
        }
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

    private async void OnRecordClicked(object sender, EventArgs e)
    {
        if (_recorder.IsRecording)
            await StopRecordingAsync();
        else
            await StartRecordingAsync();
    }

    private void OnOptionChanged(object sender, EventArgs e)
    {
        // nothing to do — options are read on Transcribe click
    }

    private async void OnTranscribeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedAudioPath))
        {
            await ShowErrorAsync("Please select or record an audio file first.");
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
    // Recording
    // ──────────────────────────────────────────────────────────────────────────

    private async Task StartRecordingAsync()
    {
        try
        {
            Directory.CreateDirectory(TempDirectory);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _micTempPath      = Path.Combine(TempDirectory, $"rec_mic_{stamp}.wav");
            _loopbackTempPath = null;

            string? loopbackPath = null;
            if (_recorder.SupportsLoopback && SwitchSystemAudio.IsToggled)
            {
                _loopbackTempPath = Path.Combine(TempDirectory, $"rec_sys_{stamp}.wav");
                loopbackPath = _loopbackTempPath;
            }

            var deviceName = PickerInputDevice.SelectedItem as string;
            await _recorder.StartRecordingAsync(_micTempPath, deviceName, loopbackPath);

            // Update UI
            BtnRecord.Text        = "⏹  Stop Recording";
            BtnSelectFile.IsEnabled = false;
            BtnTranscribe.IsEnabled = false;
            LblSelectedFile.Text  = "Recording…";
            LblRecordingTime.IsVisible = true;
            SetStatus("🔴 Recording…");

            // Start elapsed timer
            _recordingTimerCts = new CancellationTokenSource();
            _ = RunRecordingTimerAsync(_recordingTimerCts.Token);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to start recording:\n{ex.Message}");
        }
    }

    private async Task StopRecordingAsync()
    {
        // Stop timer
        _recordingTimerCts?.Cancel();
        _recordingTimerCts?.Dispose();
        _recordingTimerCts = null;

        SetStatus("Stopping recording…");
        await _recorder.StopRecordingAsync();

        // Reset recording UI
        BtnRecord.Text          = "🔴  Record Audio";
        BtnSelectFile.IsEnabled = true;
        LblRecordingTime.IsVisible = false;

        try
        {
            // Determine the final audio path (merge if loopback was captured)
            string finalPath;
            if (_loopbackTempPath is not null && File.Exists(_loopbackTempPath))
            {
                var mergedPath = Path.Combine(TempDirectory,
                    $"rec_merged_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                var progress = new Progress<string>(msg =>
                    MainThread.BeginInvokeOnMainThread(() => SetStatus(msg)));
                finalPath = await _audio.MixAudioFilesAsync(
                    _micTempPath!, _loopbackTempPath, mergedPath, progress);
            }
            else
            {
                finalPath = _micTempPath!;
            }

            _selectedAudioPath       = finalPath;
            LblSelectedFile.Text     = Path.GetFileName(finalPath);
            ToolTipProperties.SetText(LblSelectedFile, finalPath);
            BtnTranscribe.IsEnabled  = true;
            SetStatus("✅ Recording saved. Ready to transcribe.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to finalise recording:\n{ex.Message}");
            SetStatus("❌ Recording error.");
        }
    }

    private async Task RunRecordingTimerAsync(CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(500, ct); }
            catch (OperationCanceledException) { break; }

            var elapsed = DateTime.UtcNow - start;
            var text = $"● {(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (LblRecordingTime.IsVisible)
                    LblRecordingTime.Text = text;
            });
        }
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
                AudioFilePath   = wavPath,
                Model           = SelectedModel(),
                Language        = SelectedLanguageCode(),
                OutputFormat    = SelectedFormat(),
                OutputDirectory = OutputDirectory,
                OutputBaseName  = $"transcript_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            // 3. Transcribe
            var result = await _whisper.TranscribeAsync(options, progress, ct);

            // 4. Show result
            if (result.Success)
            {
                EditorTranscript.Text = result.Text;
                _lastOutputFilePath   = result.OutputFilePath;
                BtnSave.IsEnabled     = true;
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
            BtnRecord.IsEnabled     = !busy;
            BtnCancel.IsVisible     = busy;
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
