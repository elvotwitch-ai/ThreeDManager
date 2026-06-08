namespace ThreeDManager.Application.DTOs;

public class ParsedPrintMetadata
{
    public string? SlicerName { get; set; }
    public int? EstimatedTimeMinutes { get; set; }
    public decimal? FilamentUsedGrams { get; set; }
    public decimal? FilamentUsedMeters { get; set; }
    public string? MaterialType { get; set; }
    public decimal? NozzleTemperature { get; set; }
    public decimal? BedTemperature { get; set; }
    public decimal? LayerHeight { get; set; }
    public decimal? InfillPercentage { get; set; }
    public decimal? ReportedCost { get; set; }
    public List<string> Warnings { get; set; } = new();
}