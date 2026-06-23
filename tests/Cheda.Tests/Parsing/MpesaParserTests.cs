using Cheda.Core.Models;
using Cheda.Core.Parsing;
using Cheda.Core.Parsing.Parsers;
using FluentAssertions;

namespace Cheda.Tests.Parsing;

public class MpesaParserTests
{
    private readonly MpesaParser _parser = new();
    private readonly DateTimeOffset _ts = MpesaFixtures.BaseTime;

    // ── CanHandle ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_KnownSenderWithTransactionCode_ReturnsTrue()
        => _parser.CanHandle(MpesaFixtures.Sender, MpesaFixtures.Sent).Should().BeTrue();

    [Fact]
    public void CanHandle_OtpMessage_ReturnsFalse()
        => _parser.CanHandle(MpesaFixtures.Sender, MpesaFixtures.OtpMessage).Should().BeFalse();

    [Fact]
    public void CanHandle_MarketingMessage_ReturnsFalse()
        => _parser.CanHandle(MpesaFixtures.Sender, MpesaFixtures.MarketingMessage).Should().BeFalse();

    [Fact]
    public void CanHandle_WrongSender_ReturnsFalse()
        => _parser.CanHandle("EQUITY", MpesaFixtures.Sent).Should().BeFalse();

    // ── Sent ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Sent_ExtractsAllFields()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.Sent, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.TransactionCode.Should().Be("QGH2XK1A23");
        tx.Type.Should().Be(TransactionType.Sent);
        tx.Amount.Should().Be(1000.00m);
        tx.Counterparty.Should().Contain("JOHN DOE");
        tx.BalanceAfter.Should().Be(4500.00m);
        tx.TransactionCost.Should().Be(11.00m);
        tx.Source.Should().Be(TransactionSource.Mpesa);
        tx.Timestamp.Should().Be(_ts);
    }

    // ── Received ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Received_ExtractsAllFields()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.Received, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.TransactionCode.Should().Be("RBT5YZ2B34");
        tx.Type.Should().Be(TransactionType.Received);
        tx.Amount.Should().Be(500.00m);
        tx.Counterparty.Should().Contain("JANE DOE");
        tx.BalanceAfter.Should().Be(5000.00m);
    }

    // ── Pay Till ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PaidTill_ExtractsAllFields()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.PaidTill, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.TransactionCode.Should().Be("SCP6WA3C45");
        tx.Type.Should().Be(TransactionType.PaidTill);
        tx.Amount.Should().Be(250.00m);
        tx.Counterparty.Should().Contain("JAVA HOUSE").And.Contain("123456");
        tx.TransactionCost.Should().Be(0.00m);
    }

    // ── Pay Paybill ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PaidPaybill_ExtractsAllFields()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.PaidPaybill, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.TransactionCode.Should().Be("TDQ7XB4D56");
        tx.Type.Should().Be(TransactionType.PaidPaybill);
        tx.Amount.Should().Be(2500.00m);
        tx.Counterparty.Should().Contain("KPLC PREPAID").And.Contain("888880").And.Contain("54321");
        tx.TransactionCost.Should().Be(33.00m);
    }

    // ── Withdrawn ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Withdrawn_IsNonExpenseTransfer()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.Withdrawn, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Withdrawn);
        tx.Amount.Should().Be(3000.00m);
        tx.IsNonExpenseTransfer.Should().BeTrue();
    }

    // ── Deposit ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Deposit_IsNonExpenseTransfer()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.Deposit, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Deposit);
        tx.Amount.Should().Be(1000.00m);
        tx.IsNonExpenseTransfer.Should().BeTrue();
    }

    // ── Airtime ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Airtime_ExtractsAmount()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.Airtime, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Airtime);
        tx.Amount.Should().Be(50.00m);
    }

    // ── Fuliza ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Fuliza_ExtractsAmount()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.Fuliza, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Fuliza);
        tx.Amount.Should().Be(200.00m);
    }

    // ── M-Shwari ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MShwari_IsNonExpenseTransfer()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.MShwari, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.MShwari);
        tx.Amount.Should().Be(1000.00m);
        tx.IsNonExpenseTransfer.Should().BeTrue();
        tx.Counterparty.Should().Be("M-Shwari");
    }

    // ── Reversal ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Reversal_LinksToOriginalTransaction()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.Reversal, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Reversal);
        tx.ReversesTransactionCode.Should().Be("QGH2XK1A23");
        tx.Amount.Should().Be(500.00m);
        tx.BalanceAfter.Should().Be(5500.00m);
    }

    // ── Real-world variants ────────────────────────────────────────────────────

    [Fact]
    public void Parse_SentPaybill_ClassifiedAsPaidPaybillWithAccount()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.SentPaybill, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.TransactionCode.Should().Be("UE92Q3N5H7");
        tx.Type.Should().Be(TransactionType.PaidPaybill);
        tx.Amount.Should().Be(200.00m);
        tx.Counterparty.Should().Contain("KPLC PREPAID").And.Contain("45136199804");
        tx.TransactionCost.Should().Be(5.00m);
    }

    [Fact]
    public void Parse_SentPaybill_SameBizAndAccount_CounterpartyNotDuplicated()
    {
        // "SAFARICOM DATA BUNDLES for account SAFARICOM DATA BUNDLES" → no redundant repetition
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.SentPaybillDataBundles, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.PaidPaybill);
        tx.Counterparty.Should().Be("SAFARICOM DATA BUNDLES");
    }

    [Fact]
    public void Parse_BuyGoods_ClassifiedAsPaidTillWithMerchantName()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.BuyGoods, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.TransactionCode.Should().Be("UE62Q3C3MK");
        tx.Type.Should().Be(TransactionType.PaidTill);
        tx.Amount.Should().Be(100.00m);
        tx.Counterparty.Should().Be("MAXWELL CHEMIST");
        tx.TransactionCost.Should().Be(0.00m);
    }

    [Fact]
    public void Parse_BuyGoodsViaAgent_StripsMpayaSuffix()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.BuyGoodsViaAgent, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.PaidTill);
        tx.Counterparty.Should().Contain("JUSTUS Mokoya");
    }

    [Fact]
    public void Parse_AirtimeShortForm_NoHaveNoBspace_ExtractsAmount()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.AirtimeShort, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Airtime);
        tx.Amount.Should().Be(5.00m);
    }

    [Fact]
    public void Parse_ReceivedNoSpace_ExtractsFields()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.ReceivedNoSpace, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Received);
        tx.Amount.Should().Be(8000.00m);
        tx.Counterparty.Should().Be("ZIIDI");
    }

    [Fact]
    public void Parse_ReceivedMaskedPhone_IncludesMaskedNumberInCounterparty()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.ReceivedMaskedPhone, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Received);
        tx.Amount.Should().Be(5000.00m);
        tx.Counterparty.Should().Contain("Joshua Martin");
    }

    [Fact]
    public void Parse_SentNoPhone_CounterpartyIsNameOnly()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.SentNoPhone, _ts);

        result.Success.Should().BeTrue();
        var tx = result.Transaction!;
        tx.Type.Should().Be(TransactionType.Sent);
        tx.Amount.Should().Be(50.00m);
        tx.Counterparty.Should().Be("Keneth Wambu");
    }

    [Fact]
    public void Parse_CounterpartyDoubleSpaceNormalized()
    {
        // M-PESA includes double spaces between first name and surname.
        const string body =
            "UE62Q3B5VZ Confirmed. Ksh200.00 sent to FAITH  GACHEMI 0797460219 on 6/5/26 at 2:19 PM." +
            " New M-PESA balance is Ksh5,914.90. Transaction cost, Ksh7.00.";

        var tx = _parser.Parse(MpesaFixtures.Sender, body, _ts).Transaction!;

        tx.Counterparty.Should().Be("FAITH GACHEMI 0797460219");  // single space
    }

    // ── Unknown format — never dropped ─────────────────────────────────────────

    [Fact]
    public void Parse_UnknownFormat_ReturnsUnknownType()
    {
        var result = _parser.Parse(MpesaFixtures.Sender, MpesaFixtures.UnknownFormat, _ts);

        result.Success.Should().BeTrue();
        result.Transaction!.Type.Should().Be(TransactionType.Unknown);
        result.Transaction.TransactionCode.Should().Be("AKX4EI1K23");
    }

    // ── ParserEngine integration ────────────────────────────────────────────────

    [Fact]
    public void ParserEngine_WithMpesaParser_RoutesCorrectly()
    {
        var engine = new ParserEngine();
        engine.Register(new MpesaParser());

        var result = engine.Parse(MpesaFixtures.Sender, MpesaFixtures.Sent, _ts);

        result.Success.Should().BeTrue();
        result.Transaction!.Type.Should().Be(TransactionType.Sent);
    }

    [Fact]
    public void ParserEngine_UnknownSender_ReturnsFail()
    {
        var engine = new ParserEngine();
        engine.Register(new MpesaParser());

        var result = engine.Parse("UNKNOWN", "some message", _ts);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ParserEngine_BatchParse_ReturnsAllSuccessful()
    {
        var engine = new ParserEngine();
        engine.Register(new MpesaParser());

        var messages = new[]
        {
            (MpesaFixtures.Sender, MpesaFixtures.Sent, _ts),
            (MpesaFixtures.Sender, MpesaFixtures.Received, _ts),
            (MpesaFixtures.Sender, MpesaFixtures.PaidTill, _ts),
            ("OTHER", "not financial", _ts),          // should be excluded
            (MpesaFixtures.Sender, MpesaFixtures.OtpMessage, _ts), // filtered out
        };

        var txs = engine.ParseBatch(messages);

        txs.Should().HaveCount(3);
    }
}
