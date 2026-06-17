using System.Reflection;
using System.Text.Json;
using Azure.Identity;
using Igma.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Igma.Data;
using Igma.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddAzureWebAppDiagnostics();

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

var useDevAuth = string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"]);

if (useDevAuth && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "AzureAd:ClientId is not configured. Dev auth cannot run outside the Development environment. " +
        "Set AzureAd:ClientId (and TenantId) in your App Service configuration.");

if (useDevAuth)
{
    builder.Services.AddAuthentication(DevAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
    builder.Services.AddRazorPages();
    builder.Services.AddControllersWithViews();
}
else
{
    builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAd");
    builder.Services.AddRazorPages();
    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();

}

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("Reader", "Writer")
        .Build();

    options.AddPolicy("Writer", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Writer"));
});

var dbPath = builder.Environment.IsDevelopment()
    ? Path.Combine(builder.Environment.ContentRootPath, "igma.db")
    : "/home/data/igma.db";

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/home/data/keys"))
        .SetApplicationName("Igma");
}

builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory($"Data Source={dbPath}"));
builder.Services.AddSingleton<IpLabelRepository>();
builder.Services.AddSingleton<IpGroupRepository>();

builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddSingleton<DefaultAzureCredential>();
builder.Services.AddScoped<IIpGroupService, IpGroupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    DbInitializer.Initialize(db);
    if (app.Configuration.GetValue<bool>("SeedDatabase"))
        DbSeeder.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unknown";

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            version,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await ctx.Response.WriteAsync(result);
    }
}).AllowAnonymous();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapControllers();

app.Run();
