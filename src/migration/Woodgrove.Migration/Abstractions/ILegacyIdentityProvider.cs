namespace Woodgrove.Migration.Abstractions;

public interface ILegacyIdentityProvider
{
    Task<LegacyValidationResult> ValidateAsync(string usernameOrEmail, string? plaintextPassword, CancellationToken ct = default);

    IAsyncEnumerable<LegacyUserRecord> EnumerateUsersAsync(CancellationToken ct = default);
}
