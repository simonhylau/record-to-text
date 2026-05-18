namespace LocalWhisperTranscriber;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell())
        {
            Title = "Local Whisper Transcriber",
            MinimumWidth = 820,
            MinimumHeight = 640,
            Width = 1000,
            Height = 720
        };
        return window;
    }
}
