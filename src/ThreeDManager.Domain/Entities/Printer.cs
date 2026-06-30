using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Domain.Entities;

public class Printer
{
    public Guid Id {get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Informe o nome da impressora.")]
    public string Name {get; set; } = string.Empty;
    public string? Model {get; set; }
    public string? Brand {get; set; }
    public string? Notes {get; set; }
    public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
}