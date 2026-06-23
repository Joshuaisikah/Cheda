using Android.Content;
using Cheda.Core.Sms;
using SmsMessage = Cheda.Core.Sms.SmsMessage;

namespace Cheda.App.Platforms.Android.Sms;

/// <summary>
/// Reads M-Pesa (and future financial-source) SMS from the Android inbox content provider.
/// Filters to known sender addresses at query time; OTP / marketing messages from those
/// senders are excluded downstream by the parser (which checks for a transaction code).
/// </summary>
public sealed class AndroidSmsReader : ISmsReader
{
    // Extend this set when additional financial sources (e.g. "EQUITY") are added.
    private static readonly HashSet<string> FinancialSenders =
        new(StringComparer.OrdinalIgnoreCase) { "MPESA" };

    private static Context AppContext =>
        global::Android.App.Application.Context;

    public bool HasPermission =>
        AppContext.CheckSelfPermission(global::Android.Manifest.Permission.ReadSms)
            == global::Android.Content.PM.Permission.Granted;

    public IReadOnlyList<SmsMessage> ReadInbox(DateTimeOffset? since = null)
    {
        var messages = new List<SmsMessage>();

        // Build sender filter: address IN (?, ?, ...)
        var placeholders  = string.Join(",", FinancialSenders.Select(_ => "?"));
        var selectionParts = new List<string> { $"address IN ({placeholders})" };
        var selectionArgs  = FinancialSenders.ToList();

        if (since.HasValue)
        {
            selectionParts.Add("date >= ?");
            selectionArgs.Add(since.Value.ToUnixTimeMilliseconds().ToString());
        }

        var uri = global::Android.Net.Uri.Parse("content://sms/inbox");
        if (uri is null) return messages;  // literal parse cannot fail; guard for binding
        var selection = string.Join(" AND ", selectionParts);

        try
        {
            using var cursor = AppContext.ContentResolver!.Query(
                uri,
                projection:    ["address", "body", "date", "subscription_id"],
                selection:     selection,
                selectionArgs: [.. selectionArgs],
                sortOrder:     "date DESC");

            if (cursor is null) return messages;

            var addrIdx = cursor.GetColumnIndex("address");
            var bodyIdx = cursor.GetColumnIndex("body");
            var dateIdx = cursor.GetColumnIndex("date");
            var simIdx  = cursor.GetColumnIndex("subscription_id");

            while (cursor.MoveToNext())
            {
                var sender    = (cursor.GetString(addrIdx) ?? "").Trim();
                var body      = cursor.GetString(bodyIdx) ?? "";
                var dateMs    = cursor.GetLong(dateIdx);
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dateMs);

                // subscription_id is available from API 22; default to null on older devices.
                int? simSlot = simIdx >= 0 ? cursor.GetInt(simIdx) : null;

                messages.Add(new SmsMessage
                {
                    Sender    = sender,
                    Body      = body,
                    Timestamp = timestamp,
                    SimSlot   = simSlot,
                });
            }
        }
        catch (Exception ex)
        {
            // Partial results are better than a crash; permission issues surface here.
            System.Diagnostics.Debug.WriteLine($"[Cheda] SMS read error: {ex.Message}");
        }

        return messages;
    }
}
