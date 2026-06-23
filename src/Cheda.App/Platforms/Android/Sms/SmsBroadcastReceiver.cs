using Android.Content;
using Cheda.Core.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;

namespace Cheda.App.Platforms.Android.Sms;

/// <summary>
/// Listens for incoming SMS messages in real time.
/// When a message arrives from a known financial sender (e.g. MPESA), it is immediately
/// piped through the ImportService pipeline (parse → categorize → persist).
/// Phase 9 will add local notifications on top of this callback.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(
    ["android.provider.Telephony.SMS_RECEIVED"],
    Priority = (int)global::Android.Content.IntentFilterPriority.NormalPriority)]
public sealed class SmsBroadcastReceiver : BroadcastReceiver
{
    private static readonly HashSet<string> FinancialSenders =
        new(StringComparer.OrdinalIgnoreCase) { "MPESA" };

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != "android.provider.Telephony.SMS_RECEIVED") return;

        var incomingMessages = ExtractMessages(intent);
        if (incomingMessages.Count == 0) return;

        // Resolve IImportService from MAUI's DI container.
        var importService = IPlatformApplication.Current?.Services
            .GetService<IImportService>();
        if (importService is null) return;

        // OnReceive runs on the main thread; offload processing immediately.
        // Each message is short — parse + DB insert completes well within Android's
        // 10-second broadcast window, but we run in background for UI safety.
        _ = Task.Run(async () =>
        {
            foreach (var msg in incomingMessages)
                await importService.ProcessSingleAsync(msg);
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
