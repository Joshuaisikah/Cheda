using Cheda.App.Security;
using Cheda.App.Storage;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
using Cheda.Core.Notifications;
using Cheda.Core.Parsing;
using Cheda.Core.Parsing.Parsers;
using Cheda.Core.Security;
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

        RegisterSecurity(builder.Services);
        RegisterStorage(builder.Services);
        RegisterCore(builder.Services);
        RegisterSms(builder.Services);
        RegisterNotifications(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterSecurity(IServiceCollection services)
    {
        services.AddSingleton<IDatabaseKeyProvider, InMemoryDatabaseKeyProvider>();
        services.AddSingleton<IPinStore,            SecureStoragePinStore>();
        services.AddSingleton<PinHashService>();
        services.AddSingleton<IAppLockService, AppLockService>();

#if ANDROID
        services.AddSingleton<IBiometricService,
            Cheda.App.Platforms.Android.Security.AndroidBiometricService>();
#endif
    }

    private static void RegisterStorage(IServiceCollection services)
    {
        // DatabaseService owns the encrypted SQLite connection.
        // Call InitializeAsync() after successful PIN/biometric auth (Phase 11 lock screen).
        // Before a PIN is configured, InitializeAsync() uses a random fallback key.
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

    private static void RegisterNotifications(IServiceCollection services)
    {
        services.AddSingleton<NotificationSettingsService>();
        services.AddSingleton<IAlertEvaluator, AlertEvaluator>();
        services.AddSingleton<AlertCoordinator>();

#if ANDROID
        services.AddSingleton<INotificationService,
            Cheda.App.Platforms.Android.Notifications.AndroidNotificationService>();
        services.AddSingleton<
            Cheda.App.Platforms.Android.Notifications.DigestScheduler>();
#endif
    }
}
