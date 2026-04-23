# IGMA  
**IP Group Metadata & Analysis**  
*The missing enrichment layer for Azure IP Groups.*

IGMA adds labels and context to [Azure IP Groups](https://learn.microsoft.com/en-us/azure/firewall/ip-groups) - all read‑only, all from your existing subscriptions. Azure gives you the objects. IGMA gives you the insight.

## Features

- **IP Group list** — pulls all IP Groups across one or more Azure subscriptions and shows labeled vs. unlabeled counts at a glance
- **Group detail** — view every IP/CIDR in a group with its label and notes; edit inline
- **Unlabeled view** — cross-group list of every IP that hasn't been labeled yet
- **Search** — find IPs, CIDRs, labels, or notes across all groups from the navbar
- **Always in sync** — visiting any page pulls the latest data from Azure; new IPs are tracked automatically and removed IPs are cleaned up

## Tech stack

- .NET 10 ASP.NET Core Razor Pages
- Dapper + SQLite (labels and metadata stored locally)
- Azure SDK (`Azure.ResourceManager.Network`) with `DefaultAzureCredential`
- Microsoft Identity Web (Entra ID authentication)

## Configuration

Set the following values in App Service configuration (or `appsettings.json` locally):

| Key | Description |
|-----|-------------|
| `AzureAd:TenantId` | Entra ID tenant ID |
| `AzureAd:ClientId` | App registration client ID |
| `Azure:ExcludeSubscriptionIds` | *(Optional)* JSON array of subscription IDs to exclude from discovery |
| `App:ShowDetailedErrors` | *(Optional)* Show raw exception messages in the UI instead of generic error text. Recommended as a slot-specific setting on a non-production slot only. |

By default, the app discovers all subscriptions accessible to the Managed Identity — no subscription config is required. New subscriptions become visible automatically as access is granted.

In App Service, use double-underscore as the hierarchy separator in application setting names (e.g. `AzureAd__TenantId`, `AzureAd__ClientId`, `Azure__ExcludeSubscriptionIds`). These override any values in `appsettings.json`. For array values, use a JSON array string: `["sub-id-1","sub-id-2"]`.

## Azure setup

### App registration (Entra ID)

1. Register an application in Entra ID
2. Add a redirect URI to the App Registreation: `https://<your-app>.azurewebsites.net/signin-oidc`
3. Set `AzureAd:TenantId` and `AzureAd:ClientId` in App Service configuration

### Managed Identity (Azure SDK access)

1. Enable the System-Assigned Managed Identity on the App Service
2. Grant it the **Reader** role on each subscription

   For a least-privilege alternative, create a custom role (e.g. `Network - IP Group Reader`) with the following actions and assign that instead of the broad Reader role:

   | Action | Purpose |
   |--------|---------|
   | `Microsoft.Network/ipGroups/read` | Read IP Group data |
   | `Microsoft.Resources/subscriptions/read` | Enumerate accessible subscriptions (required for auto-discovery) |

   The `Microsoft.Resources/subscriptions/read` action is needed because the app discovers subscriptions dynamically at runtime. Without it, auto-discovery will fail. Assign the custom role at the subscription or management group scope — not resource group scope, as subscription enumeration requires subscription-level visibility.

### Database

Labels are stored in a SQLite database at `/home/data/igma.db` on App Service. The `/home` directory is persistent across restarts. No database setup is required — the schema is created automatically on first run.

## Logging

Application logs are written via `Microsoft.Extensions.Logging.AzureAppServices`, which is a no-op locally and activates automatically when running on App Service.

To enable log capture in the portal:

1. Go to **App Service → Monitoring → App Service Logs**
2. Set **Application Logging (Filesystem)** to **On**
3. Set a retention period (e.g. 3–7 days) to prevent unbounded log accumulation

Logs are then available in two places:
- **Log Stream** (`Monitoring → Log stream`) — live tail of the current output
- **Filesystem** — written to `/home/LogFiles/` on the App Service instance

## Local development

### Prerequisites

- .NET 10 SDK
- Azure CLI (`az login`) if developing against real Azure subscriptions

### Setup

1. Copy the example local config file:

   ```bash
   cp Igma/appsettings.Local.json.example Igma/appsettings.Local.json
   ```

2. Edit `appsettings.Local.json` with your local settings. At minimum:

   ```json
   {
     "SeedDatabase": true
   }
   ```

   To develop against real Azure, also set:

   ```json
   {
     "SeedDatabase": false,
     "AzureAd": {
       "TenantId": "your-tenant-id",
       "ClientId": "your-client-id"
     }
   }
   ```

3. Run the app:

   ```bash
   dotnet run --project Igma/Igma.csproj
   ```

### Local config notes

- `appsettings.Local.json` is gitignored and never deployed — it is safe to put real subscription IDs and tenant IDs here
- `SeedDatabase: true` populates the database with realistic fake IP Groups and IPs on first run; it is a no-op if the database already contains data. Delete `Igma/igma.db` and restart to reseed
- When `AzureAd:ClientId` is not set, authentication is bypassed and you are signed in automatically as `dev@localhost`
- When developing against real Azure, `DefaultAzureCredential` picks up your `az login` token automatically
