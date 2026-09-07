namespace Tenant.Domain.Entities;

public partial class Dialect
{
    public int Id { get; set; }

    public string DialectCode { get; set; } = null!;

    public string Color { get; set; } = null!;

    public int HintOrder { get; set; }

    public bool IsDialect { get; set; }

    public virtual ICollection<DetectedDialect> DetectedDialectConfirmedDialects { get; set; } = new List<DetectedDialect>();

    public virtual ICollection<DetectedDialect> DetectedDialectUserGuessDialects { get; set; } = new List<DetectedDialect>();
}
