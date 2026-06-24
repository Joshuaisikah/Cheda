using Android.Content;
using Android.Util;
using Cheda.Core.Sms;
using SmsMessage = Cheda.Core.Sms.SmsMessage;

namespace Cheda.App.Platforms.Android.Sms;

public sealed class AndroidSmsReader : ISmsReader
{
    private const string Tag = "Cheda.SMS";

    // All known financial sender identifiers (M-Pesa + bank SMS channels).
    private static readonly HashSet<string> FinancialSenders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "MPESA", "M-PESA", "SAFARICOM", "22141",    // M-Pesa
            "Equity Bank", "EquityBank", "0763000000",   // Equity Bank
        };

    private static Context AppContext => global::Android.App.Application.Context;

    public bool HasPermission =>
        AppContext.CheckSelfPermission(global::Android.Manifest.Permission.ReadSms)
            == global::Android.Content.PM.Permission.Granted;

    public int     LastRawCount { get; private set; }
    public string? LastError    { get; private set; }

    public IReadOnlyList<SmsMessage> ReadInbox(DateTimeOffset? since = null)
    {
        LastError    = null;
        LastRawCount = 0;
        var messages = new List<SmsMessage>();

        if (!HasPermission)
        {
            LastError = "READ_SMS permission not granted.";
            Log.Warn(Tag, LastError);
            return messages;
        }

        // Try multiple URIs. On MIUI, content://sms/inbox sometimes returns nothing
        // while content://sms/ (with type filter) does work.
        var uriStrings = new[]
        {
            "content://sms/inbox",
            "content://sms/",
        };

        foreach (var uriString in uriStrings)
        {
            var uri = global::Android.Net.Uri.Parse(uriString);
            if (uri is null) continue;

            try
            {
                // For content://sms/ (no subfolder), filter to inbox type=1.
                // For content://sms/inbox, no type filter needed.
                var selectionParts = new List<string>();
                var selectionArgs  = new List<string>();

                if (uriString == "content://sms/")
                {
                    selectionParts.Add("type = 1"); // inbox only
                }

                if (since.HasValue)
                {
                    selectionParts.Add("date >= ?");
                    selectionArgs.Add(since.Value.ToUnixTimeMilliseconds().ToString());
                }

                var selection     = selectionParts.Count > 0 ? string.Join(" AND ", selectionParts) : null;
                var selectionArgA = selectionArgs.Count   > 0 ? selectionArgs.ToArray()             : null;

                Log.Debug(Tag, $"Querying {uriString} (selection={selection ?? "none"})");

                // Use null projection (all columns) so MIUI's non-standard sms_restricted
                // table doesn't crash with "no such column: subscription_id".
                using var cursor = AppContext.ContentResolver!.Query(
                    uri,
                    projection:    null,
                    selection:     selection,
                    selectionArgs: selectionArgA,
                    sortOrder:     "date DESC");

                if (cursor is null)
                {
                    Log.Warn(Tag, $"Cursor is null for {uriString} — trying next URI");
                    continue;
                }

                var addrIdx = cursor.GetColumnIndex("address");
                var bodyIdx = cursor.GetColumnIndex("body");
                var dateIdx = cursor.GetColumnIndex("date");
                var simIdx  = cursor.GetColumnIndex("subscription_id");

                var rawCount    = 0;
                var matchCount  = 0;

                while (cursor.MoveToNext())
                {
                    rawCount++;
                    var sender = (cursor.GetString(addrIdx) ?? "").Trim();

                    if (!FinancialSenders.Contains(sender)) continue;

                    matchCount++;
                    var body      = cursor.GetString(bodyIdx) ?? "";
                    var dateMs    = cursor.GetLong(dateIdx);
                    var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dateMs);
                    int? simSlot  = simIdx >= 0 ? cursor.GetInt(simIdx) : null;

                    messages.Add(new SmsMessage
                    {
                        Sender    = sender,
                        Body      = body,
                        Timestamp = timestamp,
                        SimSlot   = simSlot,
                    });
                }

                LastRawCount = rawCount;
                Log.Info(Tag, $"Scan complete via {uriString}: {rawCount} total SMS, {matchCount} MPESA matches");

                if (rawCount > 0) break; // Got rows — don't try next URI
                Log.Warn(Tag, $"No SMS rows at {uriString}, trying fallback");
            }
            catch (Exception ex)
            {
                LastError = $"{uriString}: {ex.Message}";
                Log.Error(Tag, $"SMS read error at {uriString}: {ex}");
            }
        }

        return messages;
    }
}
