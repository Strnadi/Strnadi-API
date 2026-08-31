using System;
using System.Collections.Generic;

namespace Strnadi.Infrastructure.Persistence.Entities;

public partial class ArticleCategory
{
    public int Id { get; set; }

    public string? Label { get; set; }

    public string Name { get; set; } = null!;

    public int? Order { get; set; }

    public virtual ICollection<ArticleCategoryAssignment> ArticleCategoryAssignments { get; set; } = new List<ArticleCategoryAssignment>();
}
