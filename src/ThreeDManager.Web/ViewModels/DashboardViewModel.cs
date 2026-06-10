namespace ThreeDManager.Web.ViewModels;

public class DashboardViewModel
{
    public int TotalPrintJobs { get; set; }

    public int CompletedPrintJobs { get; set; }

    public int FailedPrintJobs { get; set; }

    public int PlannedPrintJobs { get; set; }

    public int ImportedPrintJobs { get; set; }

    public decimal TotalFilamentUsedGrams { get; set; }

    public int TotalEstimatedTimeMinutes { get; set; }

    public int TotalActualTimeMinutes { get; set; }

    public decimal TotalReportedCost { get; set; }

    public List<DashboardRecentPrintJobViewModel> RecentPrintJobs { get; set; } = new();
}

public class DashboardRecentPrintJobViewModel
{
    public Guid Id { get; set; }

    public string ProductName { get; set; } = "Não vinculado";

    public string MaterialName { get; set; } = "Não vinculado";

    public string PrinterName { get; set; } = "Não vinculada";

    public string? SourceFileName { get; set; }

    public decimal? FilamentUsedGrams { get; set; }

    public int? EstimatedTimeMinutes { get; set; }

    public decimal? ReportedCost { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}