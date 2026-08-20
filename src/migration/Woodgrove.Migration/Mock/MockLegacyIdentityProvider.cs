using System.Runtime.CompilerServices;
using Woodgrove.Migration.Abstractions;

namespace Woodgrove.Migration.Mock;

/// <summary>
/// Demo-only legacy identity provider backed by a seeded in-memory list.
/// Replace this with a real LDAP, SQL, or legacy IdP integration in production.
/// </summary>
public sealed class MockLegacyIdentityProvider : ILegacyIdentityProvider
{
    private static readonly MockLegacyUser[] Users =
    [
        new("legacy-ada", "ada@example.com", "Ada Lovelace", "Ada", "Lovelace", "P@ssw0rd123!", false, false),
        new("legacy-alan", "alan@example.com", "Alan Turing", "Alan", "Turing", "Sup3rS3cret!", false, false),
        new("legacy-weak", "weak@example.com", "Weak Password", "Wendy", "Weak", "weakpass", true, false),
        new("legacy-lock", "locked@example.com", "Locked Account", "Lila", "Locked", "CantUseThis1!", false, true)
    ];

    public async IAsyncEnumerable<LegacyUserRecord> EnumerateUsersAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var user in Users)
        {
            ct.ThrowIfCancellationRequested();
            yield return user.ToRecord();
            await Task.Yield();
        }
    }

    public Task<LegacyValidationResult> ValidateAsync(string usernameOrEmail, string? plaintextPassword, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var user = Users.FirstOrDefault(u =>
            string.Equals(u.Email, usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.LegacyUserId, usernameOrEmail, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            return Task.FromResult(LegacyValidationResult.NotFound());
        }

        if (user.IsBlocked)
        {
            return Task.FromResult(LegacyValidationResult.Blocked(user.ToRecord(), "Legacy account is locked."));
        }

        if (!string.Equals(user.Password, plaintextPassword, StringComparison.Ordinal))
        {
            return Task.FromResult(LegacyValidationResult.Retry(user.ToRecord()));
        }

        return Task.FromResult(user.RequiresPasswordUpdate
            ? LegacyValidationResult.UpdatePassword(user.ToRecord())
            : LegacyValidationResult.Migrate(user.ToRecord()));
    }

    private sealed record MockLegacyUser(
        string LegacyUserId,
        string Email,
        string DisplayName,
        string GivenName,
        string Surname,
        string Password,
        bool RequiresPasswordUpdate,
        bool IsBlocked)
    {
        public LegacyUserRecord ToRecord() => new(LegacyUserId, Email, DisplayName, GivenName, Surname);
    }
}
