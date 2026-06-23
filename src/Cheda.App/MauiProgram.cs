using Cheda.App.Converters;
using Cheda.App.Pages.Analytics;
using Cheda.App.Pages.Dashboard;
using Cheda.App.Pages.Lock;
using Cheda.App.Pages.Onboarding;
using Cheda.App.Pages.Review;
using Cheda.App.Pages.Settings;
using Cheda.App.Pages.Transactions;
using Cheda.App.Security;
using Cheda.App.Storage;
using Cheda.Core.Analytics;
using Cheda.Core.Bills;
using Cheda.Core.Budgets;
using Cheda.Core.Categorization;
using Cheda.Core.Insights;
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
        RegisterPages(builder.Services);

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
        services.AddSingleton<IAppLockService,        AppLockService>();
        services.AddSingleton<SecureBiometricKeyStore>();

#if ANDROID
        services.AddSingleton<IBiometricService,
            Cheda.App.Platforms.Android.Security.AndroidBiometricService>();
#endif
    }

    private static void RegisterStorage(IServiceCollection services)
    {
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
            return engine;
        });

        services.AddSingleton<ICategorizer>(sp =>
            new RuleBasedCategorizer(sp.GetRequiredService<ICategorizerStore>()));

        services.AddSingleton<IAnalyticsEngine, AnalyticsEngine>();
        services.AddSingleton<IBudgetEngine,    BudgetEngine>();
        services.AddSingleton<IBillEngine,      BillEngine>();
        services.AddSingleton<IInsightsEngine,  InsightsEngine>();
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

    private static void RegisterPages(IServiceCollection services)
    {
        // ViewModels
        services.AddTransient<LockViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<ReviewQueueViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Shell (singleton — one instance for the authenticated session)
        services.AddSingleton<AppShell>();

        // Pages
        services.AddTransient<LockPage>();
        services.AddTransient<OnboardingPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<TransactionsPage>();
        services.AddTransient<ReviewQueuePage>();
        services.AddTransient<AnalyticsPage>();
        services.AddTransient<SettingsPage>();
    }
}
