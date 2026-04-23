using Igma.Models;
using Igma.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Igma.Pages;

public class UnlabeledModel(IIpGroupService ipGroupService) : PageModel
{
    public IReadOnlyList<IpGroupEntry> Unlabeled { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Unlabeled = await ipGroupService.GetAllUnlabeledAsync();
    }
}
