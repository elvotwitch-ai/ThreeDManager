using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Web.Presentation;

public static class PrintJobStatusPresentation
{
    public static string GetDisplayLabel(string? status)
    {
        return PrintJobStatus.Normalize(status) switch
        {
            PrintJobStatus.Imported => "Importada",
            PrintJobStatus.Planned => "Planejada",
            PrintJobStatus.Completed => "Concluída",
            PrintJobStatus.Failed => "Falhou",
            PrintJobStatus.Canceled => "Cancelada",
            _ => string.IsNullOrWhiteSpace(status) ? "Não informado" : status
        };
    }

    public static string GetBadgeClass(string? status)
    {
        return PrintJobStatus.Normalize(status) switch
        {
            PrintJobStatus.Completed => "bg-success",
            PrintJobStatus.Failed => "bg-danger",
            PrintJobStatus.Planned => "bg-primary",
            PrintJobStatus.Canceled => "bg-dark",
            _ => "bg-secondary"
        };
    }
}
