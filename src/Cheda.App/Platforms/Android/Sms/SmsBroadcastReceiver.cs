using Android.Content;
using Android.Util;
using Cheda.Core.Notifications;
using Cheda.Core.Sms;
using Microsoft.Extensions.DependencyInjection;
using SmsMessage = Cheda.Core.Sms.SmsMessage;

namespace Cheda.App.Platforms.Android.Sms;

/// <summary>
/// Listens for incoming SMS messages in real time.
/// When a message arrives from a known financial sender (e.g. MPESA), it is immediately
/// piped through the ImportService pipeline (parse → categorize → persist), then
/// AlertCoordinator evaluates and dispatches any relevant notifications.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[global::Android.App.IntentFilter(
    ["android.provider.Telephony.SMS_RECEIVED"],
    Priority = 0)]
public sealed class SmsBroadcastReceiver : BroadcastReceiver
{
    private static readonly HashSet<string> FinancialSenders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "MPESA", "M-PESA", "M-PESA APP", "SAFARICOM", "22141",    // M-Pesa
            "Equity Bank", "EquityBank", "0763000000",                  // Equity Bank
        };

    private const string Tag = "Cheda.SMS";

    public override void OnReceive(Context? context, Intent? intent)
    {
        Log.Error(Tag, $"[RECV] OnReceive fired — action={intent?.Action ?? "null"}");

        if (intent?.Action != "android.provider.Telephony.SMS_RECEIVED")
        {
            Log.Error(Tag, $"[RECV] Ignoring — not SMS_RECEIVED");
            return;
        }

        var incomingMessages = ExtractMessages(intent);
        Log.Error(Tag, $"[RECV] ExtractMessages returned {incomingMessages.Count} financial SMS");

        if (incomingMessages.Count == 0) return;

        var services      = IPlatformApplication.Current?.Services;
        var importService = services?.GetService<IImportService>();
        if (importService is null)
        {
            // App process is dead — MAUI DI isn't initialised yet.
            // Wake the app silently (FLAG_ACTIVITY_NO_USER_ACTION keeps it in background).
            // App.OnStart will auto-scan when it wakes up.
            Log.Error(Tag, "[RECV] DI not ready — waking app to trigger auto-scan on start");
            if (context is not null)
            {
                var wake = new Intent(context, typeof(MainActivity));
                wake.SetFlags(ActivityFlags.NewTask
                            | ActivityFlags.ReorderToFront
                            | ActivityFlags.NoUserAction);
                context.StartActivity(wake);
            }
            return;
        }

        // OnReceive runs on the main thread; offload processing immediately.
        // Each message is short — parse + DB insert completes well within Android's
        // 10-second broadcast window, but we run in background for UI safety.
        _ = Task.Run(async () =>
        {
            Log.Error(Tag, $"[RECV] Processing {incomingMessages.Count} SMS in background");
            var coordinator = services?.GetService<AlertCoordinator>();
            foreach (var msg in incomingMessages)
            {
                Log.Error(Tag, $"[RECV] Processing: sender={msg.Sender} sim={msg.SimSlot?.ToString() ?? "null"} body[0..60]={msg.Body[..Math.Min(60, msg.Body.Length)]}");
                var result = await importService.ProcessSingleAsync(msg);
                Log.Error(Tag, $"[RECV] Result: new={result.NewTransactions} dups={result.Duplicates} unparseable={result.Unparseable}");
                if (coordinator is not null)
                    await coordinator.EvaluateAndAlertAsync(result);
            }
        });
    }

    private static List<SmsMessage> ExtractMessages(Intent intent)
    {
        var result = new List<SmsMessage>();

        // GetMessagesFromIntent handles all PDU formats (GSM/CDMA) and multipart SMS.
        var androidMessages =
            global::Android.Provider.Telephony.Sms.Intents.GetMessagesFromIntent(intent);

        if (androidMessages is null) return result;

        // The subscription ID identifying which SIM received the message.
        var subId = intent.GetIntExtra("subscription", -1);

        foreach (var msg in androidMessages)
        {
            if (msg is null) continue;

            var sender = (msg.OriginatingAddress ?? "").Trim();
            if (!FinancialSenders.Contains(sender)) continue;

            result.Add(new SmsMessage
            {
                Sender    = sender,
                Body      = msg.MessageBody ?? "",
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(msg.TimestampMillis),
                SimSlot   = subId >= 0 ? subId : null,
            });
        }

        return result;
    }
}
