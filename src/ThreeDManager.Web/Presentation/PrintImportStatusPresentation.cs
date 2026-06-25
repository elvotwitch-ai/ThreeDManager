using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Web.Presentation;

public static class PrintImportStatusPresentation
{
    public static string GetDisplayLabel(string? status)
    {
        return PrintImportStatus.Normalize(status) switch
        {
            PrintImportStatus.Uploaded => "Importado",
            PrintImportStatus.Parsed => "Processado",
            PrintImportStatus.Error => "Erro",
            _ => string.IsNullOrWhiteSpace(status) ? "Não informado" : status
        };
    }

    public static string GetBadgeClass(string? status)
    {
        return PrintImportStatus.Normalize(status) switch
        {
            PrintImportStatus.Uploaded => "bg-primary",
            PrintImportStatus.Parsed => "bg-success",
            PrintImportStatus.Error => "bg-danger",
            _ => "bg-secondary"
        };
    }
}
