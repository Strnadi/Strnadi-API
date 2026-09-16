using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.WebUtilities;

namespace Administration.Api.TagHelpers;

// <ss-th sort="key">Header</ss-th> - a sortable column header for a <ss-table>'s <thead>. Renders
// a plain <th> when no sort key is given (most columns, e.g. row-action columns, aren't
// sortable). Fixed query-string names (Sort/Dir) so every table sorts the same way with zero
// per-table wiring beyond the page's own OnGetAsync reading them.
[HtmlTargetElement("ss-th")]
public class SsThTagHelper : TagHelper
{
    private const string SortQueryKey = "Sort";
    private const string DirQueryKey = "Dir";

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    [HtmlAttributeName("sort")]
    public string? Sort { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = await output.GetChildContentAsync();
        output.TagName = "th";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (string.IsNullOrEmpty(Sort))
        {
            output.Content.SetHtmlContent(content);
            return;
        }

        var request = ViewContext.HttpContext.Request;
        var isActive = string.Equals(request.Query[SortQueryKey], Sort, StringComparison.OrdinalIgnoreCase);
        var currentDir = request.Query[DirQueryKey].ToString();
        var nextDir = isActive && currentDir == "asc" ? "desc" : "asc";

        var query = QueryHelpers.ParseQuery(request.QueryString.Value ?? string.Empty)
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        query[SortQueryKey] = Sort;
        query[DirQueryKey] = nextDir;

        var href = QueryHelpers.AddQueryString(request.Path.Value ?? "/", query!);

        var link = new TagBuilder("a");
        link.AddCssClass("ss-table__sort-link");
        if (isActive)
            link.AddCssClass("ss-table__sort-link--active");
        link.Attributes["href"] = href;
        link.InnerHtml.AppendHtml(content);

        var arrow = new TagBuilder("span");
        arrow.AddCssClass("ss-table__sort-arrow");
        if (isActive)
            arrow.InnerHtml.Append(currentDir == "asc" ? "▲" : "▼");
        link.InnerHtml.AppendHtml(arrow);

        output.Content.SetHtmlContent(link);
    }
}