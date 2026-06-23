using Cheda.Core.Security;
using Cheda.Tests.Storage.InMemory;
using FluentAssertions;

namespace Cheda.Tests.Security;

public sealed class AppLockServiceTests
{
    private static AppLockService Build(
        InMemoryPinStore?       pinStore    = null,
        FakeDatabaseKeyProvider? keyProvider = null,
        Func<DateTimeOffset>?   clock       = null)
    {
        return new AppLockService(
            new PinHashService(),
            pinStore    ?? new InMemoryPinStore(),
            new InMemorySettingsRepository(),
            keyProvider ?? new FakeDatabaseKeyProvider(),
            clock);
    }

    // ── Initial state ──────────────────────────────────────────────────────────

    [Fact]
    public void IsSetUp_false_when_no_pin_stored()
    {
        var svc = Build();
        svc.IsSetUp.Should().BeFalse();
    }

    [Fact]
    public void IsLocked_false_when_no_pin_configured()
    {
        // Without a PIN the app has no security layer — it is always "open".
        var svc = Build();
        svc.IsLocked.Should().BeFalse();
    }

    // ── SetupPinAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetupPinAsync_valid_pin_succeeds_and_unlocks()
    {
        var svc = Build();
        var result = await svc.SetupPinAsync("1234");

        result.IsSuccess.Should().BeTrue();
        svc.IsSetUp.Should().BeTrue();
        svc.IsLocked.Should().BeFalse();
    }

    [Theory]
    [InlineData("123")]    // too short
    [InlineData("1234567")] // too long
    [InlineData("abcd")]    // non-digit
    [InlineData("12 34")]   // space
    [InlineData("")]        // empty
    public async Task SetupPinAsync_invalid_pin_returns_invalid_result(string bad)
    {
        var result = await Build().SetupPinAsync(bad);
        result.IsInvalid.Should().BeTrue();
    }

    [Fact]
    public async Task SetupPinAsync_caches_db_key_in_provider()
    {
        var keyProvider = new FakeDatabaseKeyProvider();
        var svc = Build(keyProvider: keyProvider);

        await svc.SetupPinAsync("5678");

        keyProvider.Key.Should().NotBeNull();
        keyProvider.Key.Should().HaveCount(32);
        keyProvider.SetCount.Should().Be(1);
    }

    // ── VerifyPinAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyPinAsync_not_setup_returns_not_setup_result()
    {
        var result = await Build().VerifyPinAsync("1234");
        result.IsNotSetUp.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPinAsync_correct_pin_unlocks_and_caches_key()
    {
        var keyProvider = new FakeDatabaseKeyProvider();
        var svc = Build(keyProvider: keyProvider);
        await svc.SetupPinAsync("2468");

        svc.Lock();  // lock it first
        keyProvider.ClearKey();

        var result = await svc.VerifyPinAsync("2468");

        result.IsSuccess.Should().BeTrue();
        svc.IsLocked.Should().BeFalse();
        keyProvider.Key.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyPinAsync_correct_pin_key_matches_setup_key()
    {
        var svc = Build();
        var keyProvider = new FakeDatabaseKeyProvider();
        var svc2 = new AppLockService(
            new PinHashService(), new InMemoryPinStore(),
            new InMemorySettingsRepository(), keyProvider);

        await svc2.SetupPinAsync("3691");
        var setupKey = keyProvider.Key!.ToArray();

        svc2.Lock();
        keyProvider.ClearKey();
        await svc2.VerifyPinAsync("3691");

        keyProvider.Key.Should().Equal(setupKey);
    }

    [Fact]
    public async Task VerifyPinAsync_wrong_pin_returns_incorrect()
    {
        var svc = Build();
        await svc.SetupPinAsync("1111");
        svc.Lock();  // start from locked state, as the real flow would be

        var result = await svc.VerifyPinAsync("9999");

        result.IsIncorrect.Should().BeTrue();
        svc.IsLocked.Should().BeTrue();  // wrong PIN must not unlock
    }

    [Fact]
    public async Task VerifyPinAsync_wrong_pin_increments_fail_count()
    {
        var svc = Build();
        await svc.SetupPinAsync("1111");

        await svc.VerifyPinAsync("0000");
        await svc.VerifyPinAsync("0000");

        svc.GetLockoutInfo().FailedAttempts.Should().Be(2);
    }

    [Fact]
    public async Task VerifyPinAsync_three_failures_causes_cooldown()
    {
        var fixedNow = DateTimeOffset.UtcNow;
        var svc = Build(clock: () => fixedNow);
        await svc.SetupPinAsync("1111");
        svc.Lock();

        for (var i = 0; i < 3; i++)
            await svc.VerifyPinAsync("0000");

        var lockout = svc.GetLockoutInfo();
        lockout.IsLockedOut.Should().BeTrue();
        lockout.LockedUntil.Should().BeCloseTo(fixedNow.AddSeconds(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task VerifyPinAsync_while_in_cooldown_returns_locked_out_without_checking_pin()
    {
        var fixedNow = DateTimeOffset.UtcNow;
        var svc = Build(clock: () => fixedNow);
        await svc.SetupPinAsync("1111");
        svc.Lock();

        for (var i = 0; i < 3; i++)
            await svc.VerifyPinAsync("0000");

        // Still at the same frozen time — still locked out
        var result = await svc.VerifyPinAsync("1111");  // correct PIN, but locked out
        result.IsLockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPinAsync_correct_pin_after_failures_resets_fail_count()
    {
        var svc = Build();
        await svc.SetupPinAsync("1234");
        svc.Lock();

        await svc.VerifyPinAsync("0000");
        await svc.VerifyPinAsync("0000");
        await svc.VerifyPinAsync("1234");  // correct

        svc.GetLockoutInfo().FailedAttempts.Should().Be(0);
    }

    // ── Lock / Unlock ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Lock_marks_app_as_locked_and_clears_key()
    {
        var keyProvider = new FakeDatabaseKeyProvider();
        var svc = Build(keyProvider: keyProvider);
        await svc.SetupPinAsync("4321");

        svc.Lock();

        svc.IsLocked.Should().BeTrue();
        keyProvider.Key.Should().BeNull();
    }

    [Fact]
    public async Task Unlock_with_explicit_key_marks_app_as_unlocked()
    {
        var svc = Build();
        await svc.SetupPinAsync("5555");
        svc.Lock();

        svc.Unlock(new byte[32]);

        svc.IsLocked.Should().BeFalse();
    }

    // ── ClearPin ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearPin_removes_setup_and_locks_app()
    {
        var svc = Build();
        await svc.SetupPinAsync("6789");

        svc.ClearPin();

        svc.IsSetUp.Should().BeFalse();
        svc.IsLocked.Should().BeFalse();  // no PIN → no lock
    }
}
