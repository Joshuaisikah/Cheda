using Cheda.App.Storage;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
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

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterStorage(IServiceCollection services)
    {
        // DatabaseService is the single owner of the SQLite connection.
        // Call InitializeAsync() in App.OnStart() before any repositories are used.
        services.AddSingleton<DatabaseService>();

        services.AddSingleton<ITransactionRepository, SqliteTransactionRepository>();
        services.AddSingleton<ICategorizerStore,      SqliteCategorizerStore>();
        services.AddSingleton<IBudgetStore,           SqliteBudgetStore>();
        services.AddSingleton<IBillStore,             SqliteBillStore>();
        services.AddSingleton<ISettingsRepository,    SqliteSettingsRepository>();
        services.AddSingleton<IBackupService,         BackupService>();
    }
}
