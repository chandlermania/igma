using Igma.Models;
using Igma.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Igma.Pages;

public class SearchModel(IIpGroupService ipGroupService) : PageModel
{
    public string? Query { get; private set; }
    public IReadOnlyList<IpGroupEntry> Results { get; private set; } = [];

    public async Task OnGetAsync(string? q)
    {
        Query = q?.Trim();
        if (!string.IsNullOrEmpty(Query))
        {
            ViewData["SearchQuery"] = Query;
            Results = await ipGroupService.SearchAsync(Query);
        }
    }
}
