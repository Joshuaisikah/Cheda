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
            "MPESA", "M-PESA", "M-PESA APP", "SAFARICOM", "22141",    // M-Pesa
            "Equity Bank", "EquityBank", "0763000000",                  // Equity Bank
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

                // ── DIAGNOSTIC: dump all available column names once ─────────────
                var allCols = cursor.GetColumnNames();
                Log.Error(Tag, $"[DIAG] Columns ({allCols.Length}): {string.Join(", ", allCols)}");

                var addrIdx = cursor.GetColumnIndex("address");
                var bodyIdx = cursor.GetColumnIndex("body");
                var dateIdx = cursor.GetColumnIndex("date");
                var threadIdx = cursor.GetColumnIndex("thread_id");
                var msgIdIdx  = cursor.GetColumnIndex("_id");

                // MIUI uses "sim_id" (values 1/2); stock Android uses "subscription_id"
                // (arbitrary ints). Try each in order so dual-SIM works on both.
                var simIdxName = "subscription_id";
                var simIdx     = cursor.GetColumnIndex("subscription_id");
                if (simIdx < 0) { simIdx = cursor.GetColumnIndex("sim_id");   simIdxName = "sim_id"; }
                if (simIdx < 0) { simIdx = cursor.GetColumnIndex("sim_slot"); simIdxName = "sim_slot"; }
                Log.Error(Tag, simIdx >= 0
                    ? $"[DIAG] SIM column: '{simIdxName}' at index {simIdx}"
                    : "[DIAG] SIM column: NONE FOUND — single-SIM or unsupported ROM");

                var rawCount    = 0;
                var matchCount  = 0;

                // Track per-sender counts for diagnostic summary
                var senderCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                // Track msg IDs to detect cursor-level duplicates
                var seenMsgIds = new HashSet<string>();

                while (cursor.MoveToNext())
                {
                    rawCount++;
                    var sender = (cursor.GetString(addrIdx) ?? "").Trim();

                    // ── DIAGNOSTIC: count every unique sender ─────────────────────
                    senderCounts.TryGetValue(sender, out var sc);
                    senderCounts[sender] = sc + 1;

                    // ── DIAGNOSTIC: detect cursor-level duplicate rows ─────────────
                    var msgId = msgIdIdx >= 0 ? (cursor.GetString(msgIdIdx) ?? "") : "";
                    if (!string.IsNullOrEmpty(msgId))
                    {
                        if (!seenMsgIds.Add(msgId))
                            Log.Warn(Tag, $"[DIAG] DUPLICATE cursor row! _id={msgId} sender={sender}");
                    }

                    if (!FinancialSenders.Contains(sender))
                    {
                        // Log senders that look M-Pesa-like but didn't match our list
                        if (sender.Contains("PESA", StringComparison.OrdinalIgnoreCase) ||
                            sender.Contains("MPESA", StringComparison.OrdinalIgnoreCase) ||
                            sender.Contains("SAFAR", StringComparison.OrdinalIgnoreCase) ||
                            sender.Contains("22141", StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Warn(Tag, $"[DIAG] Near-match sender NOT in filter list: '{sender}'");
                        }
                        continue;
                    }

                    matchCount++;
                    var body      = cursor.GetString(bodyIdx) ?? "";
                    var dateMs    = cursor.GetLong(dateIdx);
                    var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dateMs);
                    int? simSlot  = simIdx >= 0 ? cursor.GetInt(simIdx) : null;
                    var threadId  = threadIdx >= 0 ? cursor.GetString(threadIdx) : "n/a";

                    Log.Debug(Tag, $"[DIAG] SMS #{matchCount}: sender={sender} simSlot={simSlot?.ToString() ?? "null"} "
                        + $"msgId={msgId} threadId={threadId} ts={timestamp:yyyy-MM-dd HH:mm} "
                        + $"body[0..60]={body[..Math.Min(60, body.Length)]}");

                    messages.Add(new SmsMessage
                    {
                        Sender    = sender,
                        Body      = body,
                        Timestamp = timestamp,
                        SimSlot   = simSlot,
                    });
                }

                LastRawCount = rawCount;
                Log.Error(Tag, $"[DIAG] Scan complete via {uriString}: {rawCount} total SMS, {matchCount} MPESA matches");

                // ── DIAGNOSTIC: print top-10 senders by count ─────────────────────
                var top = senderCounts.OrderByDescending(kv => kv.Value).Take(10);
                foreach (var kv in top)
                    Log.Error(Tag, $"[DIAG] Sender '{kv.Key}': {kv.Value} SMS");

                if (rawCount > 0) break; // Got rows — don't try next URI
                Log.Warn(Tag, $"No SMS rows at {uriString}, trying fallback");
            }
            catch (Exception ex)
            {
                LastError = $"{uriString}: {ex.Message}";
                Log.Error(Tag, $"SMS read error at {uriString}: {ex}");
            }
        }

        // ── DIAGNOSTIC: report overall dedup surface ───────────────────────────
        if (messages.Count > 0)
        {
            var simGroups = messages
                .GroupBy(m => m.SimSlot?.ToString() ?? "null")
                .Select(g => $"SIM={g.Key}: {g.Count()} msgs");
            Log.Error(Tag, $"[DIAG] Messages by SIM slot: {string.Join(", ", simGroups)}");

            var senderGroups = messages
                .GroupBy(m => m.Sender, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"'{g.Key}'×{g.Count()}");
            Log.Error(Tag, $"[DIAG] Matched senders: {string.Join(", ", senderGroups)}");
        }

        return messages;
    }
}
