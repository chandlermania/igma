using Igma.Models;
using Igma.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Igma.Pages;

public class IndexModel(IIpGroupService ipGroupService, ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<IpGroupSummary> Groups { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadGroupsAsync();
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            Groups = (await ipGroupService.GetSummariesAsync())
                .Where(g => g.TotalCount > 0)
                .OrderBy(g => g.IpGroupName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load IP Groups from Azure");
            Error = $"Failed to load IP Groups: {ex.Message}";
        }
    }
}
