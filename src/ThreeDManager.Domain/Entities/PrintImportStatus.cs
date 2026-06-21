namespace ThreeDManager.Domain.Entities;

public static class PrintImportStatus
{
    public const string Uploaded = "Uploaded";
    public const string Parsed = "Parsed";
    public const string Error = "Error";

    public static readonly string[] All =
    [
        Uploaded,
        Parsed,
        Error
    ];

    public static string? Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return All.FirstOrDefault(knownStatus =>
            string.Equals(knownStatus, status.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
