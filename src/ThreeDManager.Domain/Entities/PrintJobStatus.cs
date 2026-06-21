namespace ThreeDManager.Domain.Entities;

public static class PrintJobStatus
{
    public const string Imported = "Imported";
    public const string Planned = "Planned";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Canceled = "Canceled";

    public static readonly string[] All =
    [
        Imported,
        Planned,
        Completed,
        Failed,
        Canceled
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
