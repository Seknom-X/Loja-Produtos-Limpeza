using Microsoft.Extensions.Logging;
using LojaLimpeza.Mobile.Services; // Adiciona este using no topo

namespace LojaLimpeza.Mobile;

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

// ... (código anterior)
#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Serviços da Aplicação
        builder.Services.AddSingleton<LojaApiService>();
        builder.Services.AddSingleton<CarrinhoService>(); // <-- NOVA LINHA

        // Ecrãs
        builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<CarrinhoPage>(); // <-- NOVA LINHA

        return builder.Build();

        
    }
}