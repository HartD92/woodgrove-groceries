namespace Woodgrove.Migration.BulkImport;

public sealed record BulkImportReportEntry(
    string LegacyUserId,
    string Email,
    string Status,
    string? ExternalIdObjectId = null,
    string? Error = null)
{
    public static BulkImportReportEntry Success(string legacyUserId, string email, string externalIdObjectId) =>
        new(legacyUserId, email, "Succeeded", externalIdObjectId);

    public static BulkImportReportEntry Failure(string legacyUserId, string email, string error) =>
        new(legacyUserId, email, "Failed", null, error);
}