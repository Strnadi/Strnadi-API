using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Administration.Api.TagHelpers;

// One option in the <ss-table search-fields="..."> column picker.
public record SsTableSearchField(string Value, string Text);

// <ss-table> wraps a plain <thead>/<tbody> in the .ss-table-wrapper/.ss-table markup every data
// table in the admin panel needs (see design-system.md - Data table). Centralizing the wrapper
// here means the overflow-x fix (a wide table pushing past its card's right edge instead of
// scrolling) lives in one place instead of being copy-pasted onto every table page.
//
// create-href/create-text render a small "New X" button top-right, above the table, instead of
// each page hand-building its own title-row button - which is how Documents/Roles ended up with
// a full-width .ss-btn (the default .ss-btn is width:100%, meant for auth forms; only .ss-btn--sm
// resets that, and the per-page buttons forgot it).
//
// search-* attributes render a GET search form the same way, in the same toolbar - the actual
// filtering still happens in the page's OnGetAsync (only the page model knows how to query its
// own columns), this only renders the input/column-select/button and round-trips the bound values.
[HtmlTargetElement("ss-table")]
public class SsTableTagHelper : TagHelper
{
    [HtmlAttributeName("create-href")]
    public string? CreateHref { get; set; }

    [HtmlAttributeName("create-text")]
    public string? CreateText { get; set; }

    [HtmlAttributeName("search-name")]
    public string? SearchName { get; set; }

    [HtmlAttributeName("search-value")]
    public string? SearchValue { get; set; }

    [HtmlAttributeName("search-placeholder")]
    public string? SearchPlaceholder { get; set; }

    // Optional column picker sitting next to the search input - omit search-field-name to get a
    // plain single-box search with no column choice.
    [HtmlAttributeName("search-field-name")]
    public string? SearchFieldName { get; set; }

    [HtmlAttributeName("search-field-value")]
    public string? SearchFieldValue { get; set; }

    [HtmlAttributeName("search-fields")]
    public IReadOnlyList<SsTableSearchField>? SearchFields { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = await output.GetChildContentAsync();

        var table = new TagBuilder("table");
        table.AddCssClass("ss-table");
        table.InnerHtml.AppendHtml(content);

        var tableWrapper = new TagBuilder("div");
        tableWrapper.AddCssClass("ss-table-wrapper");
        tableWrapper.InnerHtml.AppendHtml(table);

        output.TagName = null;
        output.Content.Clear();

        var hasSearch = !string.IsNullOrEmpty(SearchName);
        var hasCreate = !string.IsNullOrEmpty(CreateHref);

        if (hasSearch || hasCreate)
        {
            var toolbar = new TagBuilder("div");
            toolbar.AddCssClass("ss-table-toolbar");
            if (hasCreate && !hasSearch)
                toolbar.AddCssClass("ss-table-toolbar--end");

            if (hasSearch)
                toolbar.InnerHtml.AppendHtml(BuildSearchForm());

            if (hasCreate)
                toolbar.InnerHtml.AppendHtml(BuildCreateLink());

            output.Content.AppendHtml(toolbar);
        }

        output.Content.AppendHtml(tableWrapper);
    }

    private TagBuilder BuildCreateLink()
    {
        var link = new TagBuilder("a");
        link.AddCssClass("ss-btn");
        link.AddCssClass("ss-btn--primary");
        link.AddCssClass("ss-btn--sm");
        link.Attributes["href"] = CreateHref;
        link.InnerHtml.Append(CreateText ?? string.Empty);
        return link;
    }

    // No submit button - a search/text input submits its enclosing form on Enter on its own, and
    // a visible button next to it was redundant chrome.
    private TagBuilder BuildSearchForm()
    {
        var form = new TagBuilder("form");
        form.Attributes["method"] = "get";
        form.AddCssClass("ss-table-search");

        if (!string.IsNullOrEmpty(SearchFieldName) && SearchFields is { Count: > 0 })
        {
            var select = new TagBuilder("select");
            select.AddCssClass("ss-field__input");
            select.AddCssClass("ss-table-search__field");
            select.Attributes["name"] = SearchFieldName;

            foreach (var field in SearchFields)
            {
                var option = new TagBuilder("option");
                option.Attributes["value"] = field.Value;
                if (string.Equals(field.Value, SearchFieldValue, StringComparison.Ordinal))
                    option.Attributes["selected"] = "selected";
                option.InnerHtml.Append(field.Text);
                select.InnerHtml.AppendHtml(option);
            }

            form.InnerHtml.AppendHtml(select);
        }

        var input = new TagBuilder("input");
        input.Attributes["type"] = "search";
        input.Attributes["name"] = SearchName!;
        input.Attributes["value"] = SearchValue ?? string.Empty;
        if (!string.IsNullOrEmpty(SearchPlaceholder))
            input.Attributes["placeholder"] = SearchPlaceholder;
        input.AddCssClass("ss-field__input");
        input.AddCssClass("ss-table-toolbar__search");
        form.InnerHtml.AppendHtml(input);

        return form;
    }
}
