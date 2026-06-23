using Cheda.Core.Storage;
using Cheda.Tests.Storage.InMemory;
using FluentAssertions;

namespace Cheda.Tests.Storage;

public sealed class SettingsRepositoryTests
{
    private static ISettingsRepository Repo() => new InMemorySettingsRepository();

    [Fact]
    public void Get_returns_null_for_unknown_key()
    {
        Repo().Get("missing").Should().BeNull();
    }

    [Fact]
    public void Set_and_Get_roundtrip()
    {
        var repo = Repo();
        repo.Set("theme", "dark");
        repo.Get("theme").Should().Be("dark");
    }

    [Fact]
    public void Set_overwrites_existing_value()
    {
        var repo = Repo();
        repo.Set("key", "first");
        repo.Set("key", "second");
        repo.Get("key").Should().Be("second");
    }

    [Fact]
    public void Remove_deletes_the_entry()
    {
        var repo = Repo();
        repo.Set("key", "value");
        repo.Remove("key");
        repo.Get("key").Should().BeNull();
    }

    [Fact]
    public void Remove_nonexistent_key_does_not_throw()
    {
        var act = () => Repo().Remove("ghost");
        act.Should().NotThrow();
    }

    [Fact]
    public void Contains_returns_true_only_when_key_exists()
    {
        var repo = Repo();
        repo.Contains("k").Should().BeFalse();
        repo.Set("k", "v");
        repo.Contains("k").Should().BeTrue();
        repo.Remove("k");
        repo.Contains("k").Should().BeFalse();
    }

    [Fact]
    public void Multiple_keys_are_independent()
    {
        var repo = Repo();
        repo.Set("a", "1");
        repo.Set("b", "2");
        repo.Set("c", "3");
        repo.Get("a").Should().Be("1");
        repo.Get("b").Should().Be("2");
        repo.Get("c").Should().Be("3");
        repo.Remove("b");
        repo.Get("b").Should().BeNull();
        repo.Get("a").Should().Be("1");
    }
}
