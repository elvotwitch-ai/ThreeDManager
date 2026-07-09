using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ThreeDManager.Web.ViewModels;

public class PrintJobFromImportViewModel
{
    public Guid ImportId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? ReturnTo { get; set; }

    [Required(ErrorMessage = "Selecione um produto.")]
    public Guid? ProductId { get; set; }

    [Required(ErrorMessage = "Selecione uma impressora.")]
    public Guid? PrinterId { get; set; }

    [Required(ErrorMessage = "Selecione um material.")]
    public Guid? MaterialId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O filamento usado (g) não pode ser negativo.")]
    public decimal? FilamentUsedGrams { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O filamento usado (m) não pode ser negativo.")]
    public decimal? FilamentUsedMeters { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O tempo estimado não pode ser negativo.")]
    public int? EstimatedTimeMinutes { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O tempo real não pode ser negativo.")]
    public int? ActualTimeMinutes { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O custo informado não pode ser negativo.")]
    public decimal? ReportedCost { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O custo de embalagem não pode ser negativo.")]
    public decimal? PackagingCost { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "As unidades produzidas não podem ser negativas.")]
    public int UnitsProduced { get; set; } = 1;

    public string Status { get; set; } = "Imported";

    public string? ParsedMaterialType { get; set; }

    public IEnumerable<SelectListItem> ProductOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> PrinterOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> MaterialOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IReadOnlyDictionary<string, decimal?> MaterialCostPerGramById { get; set; } =
        new Dictionary<string, decimal?>();

    public IReadOnlyDictionary<string, decimal?> PrinterCostPerHourById { get; set; } =
        new Dictionary<string, decimal?>();

    public IReadOnlyDictionary<string, decimal?> ProductTargetMarginPercentageById { get; set; } =
        new Dictionary<string, decimal?>();

    public IReadOnlyDictionary<string, decimal?> ProductDefaultPackagingCostById { get; set; } =
        new Dictionary<string, decimal?>();

    public IEnumerable<SelectListItem> StatusOptions { get; set; } = Enumerable.Empty<SelectListItem>();
}
