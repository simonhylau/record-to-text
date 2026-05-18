using Microsoft.Extensions.Logging;
using LocalWhisperTranscriber.Services;

namespace LocalWhisperTranscriber;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<IWhisperService, WhisperCppService>();
        builder.Services.AddSingleton<AudioConversionService>();
        builder.Services.AddSingleton<FileDialogService>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
