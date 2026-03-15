# Power BI Embedded Web App

A web application that lets you view and interact with your Power BI reports directly in the browser — no need to open Power BI itself. Reports load inside the page, and you can filter them using simple dropdowns and checkboxes.

---

## What Does It Do?

- **Embeds Power BI reports** directly in a webpage using a secure server-side token
- **Applies filters automatically** when the page loads (e.g. a default period or region)
- **Updates reports in real time** when you change a dropdown or toggle a checkbox — no page reload needed
- **Supports multiple reports** — just pick one from a dropdown list
- **Works with DirectQuery datasets** — filters pass through to the underlying stored procedures

---

## How It Works (Quick Overview)

1. The app connects to Azure AD to get a secure access token for Power BI
2. It uses that token to generate an embed token for the specific report you want
3. The report is loaded inside the page using the Power BI JavaScript SDK
4. Default filters (period, region, currency) are applied as soon as the report loads
5. When you change a filter dropdown, the app resolves the correct column/table names and applies the filter to the live report

---

## Getting Started

### What You Need Before You Begin

| Requirement | Details |
|---|---|
| Visual Studio | 2019, 2022, or 2026 |
| .NET Framework 4.8 | Install via Windows Update or the VS installer |
| Azure Subscription | Needed to register the app in Azure AD |
| Power BI Workspace | Must already exist with at least one published report |

---

### Step 1 — Register the App in Azure AD

This is a **one-time setup** done by your Azure administrator.

1. Go to the [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Give it a name (e.g. `PowerBIEmbedApp`), select **Accounts in this organizational directory only**, and click **Register**
3. Copy the **Client ID** and **Tenant ID** from the app overview page
4. Go to **Certificates & secrets** → **New client secret**, set an expiry, and copy the secret value (shown only once)
5. Go to **API permissions** → **Add a permission** → **Power BI Service** → **Application permissions**, and enable:
   - `Dataset.Read.All`, `Report.Read.All`, `Workspace.Read.All`
   - Then click **Grant admin consent**
6. In [app.powerbi.com](https://app.powerbi.com), open your workspace → **Manage access** → add the app registration with **Member** role
7. In the Power BI Admin portal → **Tenant settings** → **Allow service principals to use Power BI APIs** → Enable

---

### Step 2 — Add Your Credentials to Web.config

Open `WebApplication1\Web.config` and fill in your values:

```xml
<add key="PowerBi:ClientId"     value="YOUR-CLIENT-ID" />
<add key="PowerBi:ClientSecret" value="YOUR-CLIENT-SECRET" />
<add key="PowerBi:TenantId"     value="YOUR-TENANT-ID" />
```

You can also set the default filters that apply when a report first loads:

```xml
<add key="PowerBi:DefaultPeriod"          value="2024.FY" />
<add key="PowerBi:DefaultRegion"          value="All" />
<add key="PowerBi:DefaultConvertCurrency" value="0" />
```

> ⚠️ **Never commit real secrets to source control.** For production, store secrets in Azure Key Vault or as environment variables — never in config files that get committed to a repository.

---

### Step 3 — Add Your Reports

Open `Views\Report\ReportViewer.cshtml` and find the `reportConfigs` array near the bottom. Add an entry for each report you want to show:

```javascript
var reportConfigs = [
    {
        name: "Sales Report",
        workspaceGuid: "YOUR-WORKSPACE-GUID",
        reportGuid:    "YOUR-REPORT-GUID"
    }
];
```

**To find your GUIDs:** Open your report in [app.powerbi.com](https://app.powerbi.com) — the URL contains both:
```
https://app.powerbi.com/groups/{workspaceGuid}/reports/{reportGuid}/...
```

---

### Step 4 — Build and Run

1. Open `WebApplication1.sln` in Visual Studio
2. Right-click the solution → **Restore NuGet Packages**
3. Press **F5** to run
4. The app opens at `https://localhost:44392/Report/ReportViewer`
5. Pick a report from the dropdown — it should embed and load with filters applied

---

## Using the App

Once the app is running:

- **Select a report** from the Report dropdown at the top
- **Change the Period or Region** dropdowns to filter the data — the report updates instantly
- **Toggle Convert Currency** to switch currency conversion on or off
- **Change the User ID** field and click **Re-embed** to reload the report for a different user

---

## Adding a New Filter

To add a new filter (e.g. a Country dropdown):

1. **Add the mapping** in `Services\MappingService.cs` — link the dropdown ID to a Power BI table/column
2. **Add the HTML dropdown** in `Views\Report\ReportViewer.cshtml`
3. **Wire the change event** in `Scripts\site.js` using `resolveAndApplyFilter('ddlCountry', val)`

The `resolveAndApplyFilter` helper does the rest — it calls the API, builds the filter, and applies it to the live report.

---

## Project Structure

```
WebApplication1/
├── Controllers/
│   ├── PowerBIController.cs     — API endpoints for embed tokens and filter mapping
│   └── ReportController.cs      — Serves the report viewer page
│
├── Services/
│   ├── PowerBiService.cs        — Connects to Azure AD and generates embed tokens
│   └── MappingService.cs        — Maps UI controls to Power BI table/column names
│
├── Scripts/
│   └── site.js                  — Handles embedding, filtering, and user interactions
│
├── Views/Report/
│   └── ReportViewer.cshtml      — The main report viewer page
│
└── Web.config                   — App settings (credentials, default filters)
```

---

## Troubleshooting

| Problem | Likely Cause | Fix |
|---|---|---|
| Report doesn't load | Wrong GUIDs or missing credentials | Double-check `Web.config` values and workspace/report GUIDs |
| Blank report container | Service principal not added to workspace | Add the app to your Power BI workspace with Member role |
| `AADSTS` auth errors | Wrong Client ID, Secret, or Tenant ID | Re-check your Azure AD app registration values |
| Filters not applying | Table/column name mismatch | Check the exact table and column names in Power BI Desktop (they are case-sensitive) |
| `PowerBI is not defined` | CDN script failed to load | Check your internet connection or host `powerbi.min.js` locally |
