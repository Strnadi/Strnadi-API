using System;
using System.Collections.Generic;

namespace Strnadi.Infrastructure.Persistence.Entities;

public partial class ArticleAttachment
{
    public string FileName { get; set; } = null!;

    public int? ArticleId { get; set; }

    public int Id { get; set; }

    public virtual Article? Article { get; set; }
}
