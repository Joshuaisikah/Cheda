using Cheda.Core.Security;
using FluentAssertions;

namespace Cheda.Tests.Security;

public sealed class PinHashServiceTests
{
    private static readonly PinHashService Svc = new();

    // ── DeriveKeys determinism ─────────────────────────────────────────────────

    [Fact]
    public void DeriveKeys_same_pin_same_salt_produces_same_keys()
    {
        var salt = PinHashService.GenerateSalt();
        var (v1, db1) = Svc.DeriveKeys("1234", salt);
        var (v2, db2) = Svc.DeriveKeys("1234", salt);

        v1.Should().Equal(v2);
        db1.Should().Equal(db2);
    }

    [Fact]
    public void DeriveKeys_different_pin_different_keys()
    {
        var salt = PinHashService.GenerateSalt();
        var (v1, _) = Svc.DeriveKeys("1234", salt);
        var (v2, _) = Svc.DeriveKeys("5678", salt);

        v1.Should().NotEqual(v2);
    }

    [Fact]
    public void DeriveKeys_different_salt_different_keys()
    {
        var (v1, _) = Svc.DeriveKeys("1234", PinHashService.GenerateSalt());
        var (v2, _) = Svc.DeriveKeys("1234", PinHashService.GenerateSalt());

        v1.Should().NotEqual(v2);
    }

    [Fact]
    public void DeriveKeys_verifier_and_db_keys_are_different()
    {
        var salt = PinHashService.GenerateSalt();
        var (verifierKey, dbKey) = Svc.DeriveKeys("1234", salt);

        verifierKey.Should().NotEqual(dbKey);
    }

    [Fact]
    public void DeriveKeys_output_is_32_bytes_each()
    {
        var (vk, dk) = Svc.DeriveKeys("1234", PinHashService.GenerateSalt());
        vk.Should().HaveCount(32);
        dk.Should().HaveCount(32);
    }

    // ── GenerateSalt ───────────────────────────────────────────────────────────

    [Fact]
    public void GenerateSalt_returns_32_bytes()
    {
        PinHashService.GenerateSalt().Should().HaveCount(32);
    }

    [Fact]
    public void GenerateSalt_returns_different_value_each_call()
    {
        var s1 = PinHashService.GenerateSalt();
        var s2 = PinHashService.GenerateSalt();
        s1.Should().NotEqual(s2);
    }

    // ── CreateVerifierAndDbKey + Verify round-trip ────────────────────────────

    [Fact]
    public void Verify_correct_pin_returns_valid_and_db_key()
    {
        var (verifier, expectedDbKey) = Svc.CreateVerifierAndDbKey("9876");
        var (ok, dbKey) = Svc.Verify("9876", verifier);

        ok.Should().BeTrue();
        dbKey.Should().NotBeNull();
        dbKey.Should().Equal(expectedDbKey);
    }

    [Fact]
    public void Verify_wrong_pin_returns_false_and_null_key()
    {
        var (verifier, _) = Svc.CreateVerifierAndDbKey("1234");
        var (ok, dbKey) = Svc.Verify("9999", verifier);

        ok.Should().BeFalse();
        dbKey.Should().BeNull();
    }

    [Fact]
    public void Verify_corrupted_verifier_returns_false()
    {
        Svc.Verify("1234", "not-a-valid-blob").IsValid.Should().BeFalse();
        Svc.Verify("1234", "v2:abc:def").IsValid.Should().BeFalse();
        Svc.Verify("1234", "v1:!!!:???").IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_db_key_is_same_on_each_successful_verify()
    {
        var (verifier, _) = Svc.CreateVerifierAndDbKey("4321");
        var (_, key1) = Svc.Verify("4321", verifier);
        var (_, key2) = Svc.Verify("4321", verifier);

        key1.Should().Equal(key2);
    }
}
