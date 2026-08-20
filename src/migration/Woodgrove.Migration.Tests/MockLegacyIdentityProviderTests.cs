using Woodgrove.Migration.Mock;
using Xunit;

namespace Woodgrove.Migration.Tests;

public class MockLegacyIdentityProviderTests
{
    private readonly MockLegacyIdentityProvider _provider = new();

    [Fact]
    public async Task ValidateAsync_ReturnsMigrateForKnownStrongPasswordUser()
    {
        var result = await _provider.ValidateAsync("ada@example.com", "P@ssw0rd123!");

        Assert.True(result.UserFound);
        Assert.True(result.PasswordValid);
        Assert.False(result.RequiresPasswordUpdate);
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsUpdatePasswordForWeakUser()
    {
        var result = await _provider.ValidateAsync("weak@example.com", "weakpass");

        Assert.True(result.UserFound);
        Assert.True(result.PasswordValid);
        Assert.True(result.RequiresPasswordUpdate);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsRetryForWrongPassword()
    {
        var result = await _provider.ValidateAsync("alan@example.com", "wrong");

        Assert.True(result.UserFound);
        Assert.False(result.PasswordValid);
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsBlockForLockedUser()
    {
        var result = await _provider.ValidateAsync("locked@example.com", "CantUseThis1!");

        Assert.True(result.UserFound);
        Assert.True(result.IsBlocked);
        Assert.Equal("Legacy account is locked.", result.BlockReason);
    }
}
