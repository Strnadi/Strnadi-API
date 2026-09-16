using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Administration.Api.TagHelpers;

// <ss-table> wraps a plain <thead>/<tbody> in the .ss-table-wrapper/.ss-table markup every data
// table in the admin panel needs (see design-system.md - Data table). Centralizing the wrapper
// here means the overflow-x fix (a wide table pushing past its card's right edge instead of
// scrolling) lives in one place instead of being copy-pasted onto every table page.
[HtmlTargetElement("ss-table")]
public class SsTableTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = await output.GetChildContentAsync();

        var table = new TagBuilder("table");
        table.AddCssClass("ss-table");
        table.InnerHtml.AppendHtml(content);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "ss-table-wrapper");
        output.Content.SetHtmlContent(table);
    }
}
