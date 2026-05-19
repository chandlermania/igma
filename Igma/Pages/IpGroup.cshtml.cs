using Igma.Data;
using Igma.Models;
using Igma.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Igma.Pages;

public class IpGroupModel(
    IIpGroupService ipGroupService,
    IpLabelRepository labels,
    IpGroupRepository ipGroupRepo,
    ILogger<IpGroupModel> logger,
    IConfiguration config) : PageModel
{
    private const int PageSize = 100;

    public IReadOnlyList<IpGroupEntry> Entries { get; private set; } = [];
    public int GroupDbId { get; private set; }
    public string? GroupName { get; private set; }
    public string? ResourceGroup { get; private set; }
    public string? SubscriptionId { get; private set; }
    public string? SubscriptionName { get; private set; }
    public string? GroupDescription { get; private set; }
    public string? Error { get; private set; }
    public string? SuccessMessage { get; private set; }
    public int TotalCount { get; private set; }
    public int UnlabeledCount { get; private set; }
    public int CurrentPage { get; private set; } = 1;
    public int TotalPages { get; private set; } = 1;
    public int PageStart => (CurrentPage - 1) * PageSize + 1;
    public int PageEnd => PageStart + Entries.Count - 1;

    public async Task<IActionResult> OnGetAsync(int id, int page = 1)
    {
        GroupDbId = id;
        var azureId = ipGroupRepo.GetAzureId(id);
        if (azureId is null) return NotFound();
        GroupDescription = ipGroupRepo.GetDescription(id);
        return await LoadAsync(azureId, page);
    }

    public IActionResult OnPostSaveDescriptionJson(int id, string? description)
    {
        if (ipGroupRepo.GetAzureId(id) is null) return NotFound();

        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (description?.Length > 500)
            return new JsonResult(new { success = false, error = "Description must be 500 characters or fewer." });

        try
        {
            ipGroupRepo.UpdateDescription(id, description);
            ipGroupService.InvalidateSummaryCache();
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            var error = config.GetValue<bool>("App:ShowDetailedErrors")
                ? $"Failed to save description: {ex.Message}"
                : "Failed to save description. Please try again.";
            return new JsonResult(new { success = false, error }) { StatusCode = 500 };
        }
    }

    public IActionResult OnPostSaveLabelJson(int id, string ipAddress, string? label, string? notes)
    {
        var ipGroupId = ipGroupRepo.GetAzureId(id);
        if (ipGroupId is null) return NotFound();

        label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (label?.Length > 200)
            return new JsonResult(new { success = false, error = "Label must be 200 characters or fewer." });
        if (notes?.Length > 300)
            return new JsonResult(new { success = false, error = "Notes must be 300 characters or fewer." });

        try
        {
            labels.Upsert(ipGroupId, ipAddress, label, notes, User.Identity?.Name);
            ipGroupService.InvalidateSummaryCache();
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            var error = config.GetValue<bool>("App:ShowDetailedErrors")
                ? $"Failed to save label: {ex.Message}"
                : "Failed to save label. Please try again.";
            return new JsonResult(new { success = false, error }) { StatusCode = 500 };
        }
    }

    private async Task<IActionResult> LoadAsync(string ipGroupId, int page = 1)
    {
        try
        {
            var allEntries = await ipGroupService.GetEntriesAsync(ipGroupId);
            TotalCount = allEntries.Count;
            UnlabeledCount = allEntries.Count(e => !e.IsLabeled);
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            CurrentPage = Math.Clamp(page, 1, TotalPages);
            Entries = allEntries.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            if (allEntries.Count > 0)
            {
                GroupName = allEntries[0].IpGroupName;
                ResourceGroup = allEntries[0].ResourceGroup;
                SubscriptionId = allEntries[0].SubscriptionId;
                SubscriptionName = allEntries[0].SubscriptionName;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load IP Group {Id}", ipGroupId);
            Error = $"Failed to load IP Group: {ex.Message}";
        }

        return Page();
    }
}
