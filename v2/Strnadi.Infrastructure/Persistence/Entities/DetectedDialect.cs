using System;
using System.Collections.Generic;

namespace Strnadi.Infrastructure.Persistence.Entities;

public partial class DetectedDialect
{
    public int Id { get; set; }

    public int? UserGuessDialectId { get; set; }

    public int? ConfirmedDialectId { get; set; }

    public int FilteredRecordingPartId { get; set; }

    public int? PredictedDialectId { get; set; }

    public virtual Dialect? ConfirmedDialect { get; set; }

    public virtual FilteredRecordingPart FilteredRecordingPart { get; set; } = null!;

    public virtual Dialect? UserGuessDialect { get; set; }
}
