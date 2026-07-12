namespace ThreeDManager.Web.ViewModels;

public static class PrinterQueueThresholds
{
    public const int AttentionMinutes = 8 * 60;
}

public class PrinterQueueSummaryViewModel
{
    public int QueuedJobsCount { get; set; }

    public int QueuedEstimatedTimeMinutes { get; set; }

    public DateTime EstimatedClearAt { get; set; }

    public bool RequiresAttention => QueuedEstimatedTimeMinutes >= PrinterQueueThresholds.AttentionMinutes;
}
