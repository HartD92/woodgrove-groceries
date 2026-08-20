namespace Woodgrove.Migration.Abstractions;

public sealed record LegacyValidationResult(
    bool UserFound,
    bool PasswordValid,
    bool RequiresPasswordUpdate,
    bool IsBlocked,
    LegacyUserRecord? User = null,
    string? BlockReason = null)
{
    public static LegacyValidationResult NotFound() => new(false, false, false, false);

    public static LegacyValidationResult Retry(LegacyUserRecord user) => new(true, false, false, false, user);

    public static LegacyValidationResult Migrate(LegacyUserRecord user) => new(true, true, false, false, user);

    public static LegacyValidationResult UpdatePassword(LegacyUserRecord user) => new(true, true, true, false, user);

    public static LegacyValidationResult Blocked(LegacyUserRecord user, string? reason = null) => new(true, false, false, true, user, reason);
}
