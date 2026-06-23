using Cheda.App.Storage;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
using Cheda.Core.Parsing;
using Cheda.Core.Parsing.Parsers;
using Cheda.Core.Sms;
using Cheda.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Cheda.App;

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

        RegisterStorage(builder.Services);
        RegisterCore(builder.Services);
        RegisterSms(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterStorage(IServiceCollection services)
    {
        // DatabaseService owns the encrypted SQLite connection.
        // Call InitializeAsync() in App.OnStart() before any repositories are used.
        services.AddSingleton<DatabaseService>();

        services.AddSingleton<ITransactionRepository, SqliteTransactionRepository>();
        services.AddSingleton<ICategorizerStore,      SqliteCategorizerStore>();
        services.AddSingleton<IBudgetStore,           SqliteBudgetStore>();
        services.AddSingleton<IBillStore,             SqliteBillStore>();
        services.AddSingleton<ISettingsRepository,    SqliteSettingsRepository>();
        services.AddSingleton<IBackupService,         BackupService>();
    }

    private static void RegisterCore(IServiceCollection services)
    {
        services.AddSingleton<IParserEngine>(sp =>
        {
            var engine = new ParserEngine();
            engine.Register(new MpesaParser());
            // Future: engine.Register(new EquityParser()); — no downstream changes needed
            return engine;
        });

        services.AddSingleton<ICategorizer>(sp =>
            new RuleBasedCategorizer(sp.GetRequiredService<ICategorizerStore>()));
    }

    private static void RegisterSms(IServiceCollection services)
    {
#if ANDROID
        services.AddSingleton<ISmsReader,
            Cheda.App.Platforms.Android.Sms.AndroidSmsReader>();
#endif
        services.AddSingleton<IImportService, Cheda.Core.Sms.ImportService>();
    }
}
