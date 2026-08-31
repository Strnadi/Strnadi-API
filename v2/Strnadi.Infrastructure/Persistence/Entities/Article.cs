using System;
using System.Collections.Generic;

namespace Strnadi.Infrastructure.Persistence.Entities;

public partial class Article
{
    public string Name { get; set; } = null!;

    public int Id { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<ArticleAttachment> ArticleAttachments { get; set; } = new List<ArticleAttachment>();

    public virtual ICollection<ArticleCategoryAssignment> ArticleCategoryAssignments { get; set; } = new List<ArticleCategoryAssignment>();
}
