using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Igma.Pages;

[AllowAnonymous]
public class SwitchAccountModel(IConfiguration config) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(config["AzureAd:ClientId"]))
            return RedirectToPage("/Index");

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var props = new OpenIdConnectChallengeProperties
        {
            Prompt = "select_account",
            RedirectUri = "/"
        };
        return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
    }
}
