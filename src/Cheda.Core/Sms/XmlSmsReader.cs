using System.Xml.Linq;

namespace Cheda.Core.Sms;

/// <summary>
/// Reads SMS messages from an SMS Backup &amp; Restore XML export.
/// Format: &lt;smses&gt;&lt;sms address="MPESA" date="1778066364125" body="..." /&gt;&lt;/smses&gt;
/// The <c>date</c> attribute is Unix epoch in milliseconds (UTC).
/// </summary>
public static class XmlSmsReader
{
    public static IReadOnlyList<SmsMessage> ReadAll(Stream xml)
    {
        var doc = XDocument.Load(xml);
        return doc.Descendants("sms")
            .Select(e =>
            {
                var address = e.Attribute("address")?.Value ?? "";
                var body    = e.Attribute("body")?.Value    ?? "";
                var dateMs  = long.TryParse(e.Attribute("date")?.Value, out var ms) ? ms : 0L;
                return new SmsMessage
                {
                    Sender    = address,
                    Body      = body,
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dateMs),
                };
            })
            .Where(m => !string.IsNullOrWhiteSpace(m.Body))
            .ToList();
    }
}
