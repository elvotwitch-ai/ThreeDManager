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

    public decimal? FilamentUsedGrams { get; set; }

    public decimal? FilamentUsedMeters { get; set; }

    public int? EstimatedTimeMinutes { get; set; }

    public int? ActualTimeMinutes { get; set; }

    public decimal? ReportedCost { get; set; }

    public string Status { get; set; } = "Imported";

    public string? ParsedMaterialType { get; set; }

    public IEnumerable<SelectListItem> ProductOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> PrinterOptions { get; set; } = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> MaterialOptions { get; set; } = Enumerable.Empty<SelectListItem>();
}
