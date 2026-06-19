namespace ThreeDManager.Web.ViewModels;

public class DashboardViewModel
{
    public int TotalPrintJobs { get; set; }

    public int TotalPrintImports { get; set; }

    public int CompletedPrintJobs { get; set; }

    public int FailedPrintJobs { get; set; }

    public int PlannedPrintJobs { get; set; }

    public int ImportedPrintJobs { get; set; }

    public int ParsedPrintImports { get; set; }

    public int FailedPrintImports { get; set; }

    public decimal TotalFilamentUsedGrams { get; set; }

    public int TotalEstimatedTimeMinutes { get; set; }

    public int TotalActualTimeMinutes { get; set; }

    public decimal TotalReportedCost { get; set; }

    public decimal TotalCalculatedMaterialCost { get; set; }

    public int LowStockMaterialsCount { get; set; }

    public List<DashboardRecentPrintJobViewModel> RecentPrintJobs { get; set; } = new();

    public List<DashboardFailedPrintImportViewModel> RecentFailedImports { get; set; } = new();

    public List<DashboardStockMovementViewModel> RecentStockMovements { get; set; } = new();

    public List<DashboardLowStockMaterialViewModel> LowStockMaterials { get; set; } = new();
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

    public decimal? CalculatedMaterialCost { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class DashboardFailedPrintImportViewModel
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public DateTime ImportedAt { get; set; }
}

public class DashboardStockMovementViewModel
{
    public Guid Id { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    public string MovementType { get; set; } = string.Empty;

    public decimal QuantityGrams { get; set; }

    public decimal? StockBeforeGrams { get; set; }

    public decimal? StockAfterGrams { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class DashboardLowStockMaterialViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal CurrentStockGrams { get; set; }

    public decimal MinimumStockGrams { get; set; }
}
