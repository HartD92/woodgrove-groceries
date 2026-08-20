namespace Woodgrove.Migration.Abstractions;

public sealed record LegacyUserRecord(
    string LegacyUserId,
    string Email,
    string DisplayName,
    string? GivenName = null,
    string? Surname = null);
