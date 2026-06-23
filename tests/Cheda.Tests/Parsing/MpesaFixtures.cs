namespace Cheda.Tests.Parsing;

/// <summary>
/// Real-world M-Pesa SMS samples (sanitised). One per message type.
/// </summary>
internal static class MpesaFixtures
{
    public const string Sender = "MPESA";
    public static readonly DateTimeOffset BaseTime = new(2025, 6, 15, 10, 30, 0, TimeSpan.FromHours(3));

    public const string Sent =
        "QGH2XK1A23 Confirmed. Ksh1,000.00 sent to JOHN DOE 0712345678 on 15/6/25 at 10:30 AM." +
        " New M-PESA balance is Ksh4,500.00. Transaction cost, Ksh11.00.";

    public const string Received =
        "RBT5YZ2B34 Confirmed. You have received Ksh500.00 from JANE DOE 0722000000 on 15/6/25 at 2:00 PM." +
        " New M-PESA balance is Ksh5,000.00.";

    public const string PaidTill =
        "SCP6WA3C45 Confirmed. Ksh250.00 paid to JAVA HOUSE Till 123456 on 15/6/25 at 1:15 PM." +
        " New M-PESA balance is Ksh3,250.00. Transaction cost, Ksh0.00.";

    public const string PaidPaybill =
        "TDQ7XB4D56 Confirmed. Ksh2,500.00 paid to KPLC PREPAID Paybill 888880 account 54321 on 15/6/25 at 8:00 AM." +
        " New M-PESA balance is Ksh1,500.00. Transaction cost, Ksh33.00.";

    public const string Withdrawn =
        "UER8YC5E67 Confirmed. On 15/6/25 at 3:00 PM Withdraw Ksh3,000.00 from Agent 123456 - JOHN AGENT" +
        " New M-PESA balance is Ksh2,000.00. Transaction cost, Ksh33.00.";

    public const string Deposit =
        "VFS9ZD6F78 Confirmed. You have deposited Ksh1,000.00 to your M-PESA account." +
        " New M-PESA balance is Ksh6,000.00.";

    public const string Airtime =
        "WGT0AE7G89 confirmed. You have bought Ksh50.00 of airtime on 15/6/25 at 7:00 AM." +
        " New M-PESA balance is Ksh950.00.";

    public const string Fuliza =
        "XHU1BF8H90 Confirmed. You have used Fuliza M-PESA for Ksh200.00. Repay by 22/6/25." +
        " Fuliza M-PESA balance is Ksh500.00.";

    public const string MShwari =
        "YIV2CG9I01 Confirmed. Ksh1,000.00 transferred to M-Shwari Lock Savings Account on 15/6/25." +
        " New M-PESA balance is Ksh500.00.";

    public const string Reversal =
        "ZJW3DH0J12 Confirmed. Your transaction of Ksh500.00 to JOHN DOE has been reversed." +
        " The reversal for QGH2XK1A23 is complete. New M-PESA balance is Ksh5,500.00.";

    // ── Real-world variants discovered from SMS backup ─────────────────────────

    // Paybill via "sent to X for account Y" (KPLC, Equity, data bundles, Tunukiwa)
    public const string SentPaybill =
        "UE92Q3N5H7 Confirmed. Ksh200.00 sent to KPLC PREPAID for account 45136199804 on 9/5/26 at 11:23 AM" +
        " New M-PESA balance is Ksh3,331.90. Transaction cost, Ksh5.00.Amount you can transact within the day is 499,800.00.";

    public const string SentPaybillDataBundles =
        "UF32Q6I8Z1 Confirmed. Ksh20.00 sent to SAFARICOM DATA BUNDLES for account SAFARICOM DATA BUNDLES on 3/6/26 at 6:08 PM." +
        " New M-PESA balance is Ksh8,737.01. Transaction cost, Ksh0.00.";

    // Buy Goods / Till — real format: period after merchant name, no till number in message
    public const string BuyGoods =
        "UE62Q3C3MK Confirmed. Ksh100.00 paid to MAXWELL CHEMIST. on 6/5/26 at 5:48 PM." +
        "New M-PESA balance is Ksh5,764.90. Transaction cost, Ksh0.00.";

    public const string BuyGoodsViaAgent =
        "UEB2Q3WSKF Confirmed. Ksh150.00 paid to JUSTUS Mokoya via MPAYA. on 11/5/26 at 5:24 PM." +
        "New M-PESA balance is Ksh2,231.90. Transaction cost, Ksh0.00.";

    // Airtime short form: "confirmed.You bought" (no space after period, no "have")
    public const string AirtimeShort =
        "UF12Q6AORB confirmed.You bought Ksh5.00 of airtime on 1/6/26 at 7:46 PM." +
        "New M-PESA balance is Ksh518.01. Transaction cost, Ksh0.00.";

    // Received with no space after "Confirmed." and no "at" in time
    public const string ReceivedNoSpace =
        "UE92Q3Q454 Confirmed.You have received Ksh8,000.00 from ZIIDI on 9/5/26 10:00 PM." +
        " New M-PESA balance is Ksh9,723.90.";

    // Received with masked phone number (privacy-redacted by M-PESA for received from strangers)
    public const string ReceivedMaskedPhone =
        "UFF2Q7WPTV Confirmed.You have received Ksh5,000.00 from Joshua Martin 0716***698 on 15/6/26 at 6:31 PM" +
        "  New M-PESA balance is Ksh5,004.00.";

    // Sent to person with no phone number (contact saved in M-PESA)
    public const string SentNoPhone =
        "UE72Q3E6P3 Confirmed. Ksh50.00 sent to Keneth Wambu on 7/5/26 at 7:58 AM." +
        " New M-PESA balance is Ksh5,482.90. Transaction cost, Ksh0.00.";

    public const string OtpMessage =
        "Your M-PESA PIN reset OTP is 123456. Do not share this code with anyone.";

    public const string MarketingMessage =
        "Dear customer, use M-PESA Ratiba to set up standing orders. Dial *334# to start.";

    public const string UnknownFormat =
        "AKX4EI1K23 Confirmed. Something new happened for Ksh100.00 that we don't have a pattern for yet.";
}
