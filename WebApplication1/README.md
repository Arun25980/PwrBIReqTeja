# Power BI Embedded — ASP.NET MVC 4.8

An ASP.NET MVC 5 web application (targeting .NET Framework 4.8) that embeds Power BI reports using server-issued embed tokens and applies default filters at embed time via the Power BI JavaScript SDK. Filter targets (table/column) map directly to the stored procedure parameters used by DirectQuery datasets.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Azure AD App Registration](#2-azure-ad-app-registration)
3. [Configure Web.config](#3-configure-webconfig)
4. [Configure Reports in the View](#4-configure-reports-in-the-view)
5. [Install / Restore NuGet Packages](#5-install--restore-nuget-packages)
6. [Build and Run](#6-build-and-run)
7. [Test the API Endpoints](#7-test-the-api-endpoints)
8. [Add a New UI Control and Mapping](#8-add-a-new-ui-control-and-mapping)
9. [Mapping Reference](#9-mapping-reference)
10. [Validation](#10-validation)
11. [Project Structure](#11-project-structure)
12. [Known Limitations and Assumptions](#12-known-limitations-and-assumptions)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. Prerequisites

| Requirement | Version |
|---|---|
| Visual Studio | 2019 or 2022 or 2026 (Community/Pro/Enterprise) |
| .NET Framework | 4.8 (installed via Windows Update or VS installer) |
| IIS Express | Included with Visual Studio |
| Azure Subscription | Required — for the AD app registration |
| Power BI Workspace | Must already exist with at least one published report |
| NuGet CLI | Bundled in Visual Studio; `nuget.exe` is included in the repo root for CLI use |

---

## 2. Azure AD App Registration

This step is performed **once** by the client-side Azure administrator. You need three values from it: **Client ID**, **Client Secret**, and **Tenant ID**.

### Step-by-step

1. **Open the Azure Portal** → [https://portal.azure.com](https://portal.azure.com)

2. **Navigate to** → **Microsoft Entra ID** → **App registrations** → **New registration**

3. **Fill in the form:**
   - Name: `PowerBIEmbedApp` (or any name)
   - Supported account types: **Accounts in this organizational directory only**
   - Redirect URI: leave blank (not needed for service principal auth)
   - Click **Register**

4. **Copy the Client ID and Tenant ID:**
   - On the app overview page, copy:
     - **Application (client) ID** → this is your `PowerBi:ClientId`
     - **Directory (tenant) ID** → this is your `PowerBi:TenantId`

5. **Create a Client Secret:**
   - Go to **Certificates & secrets** → **New client secret**
   - Set a description and expiry (e.g. 24 months)
   - Click **Add**
   - **Copy the secret Value immediately** — it is only shown once
   - This is your `PowerBi:ClientSecret`

6. **Grant Power BI API permissions:**
   - Go to **API permissions** → **Add a permission** → **Power BI Service**
   - Select **Application permissions**
   - Enable all of the following:
     - `Dataset.Read.All`
     - `Report.Read.All`
     - `Workspace.Read.All`
   - Click **Add permissions**
   - Click **Grant admin consent for [your tenant]** (requires Global Admin role)

7. **Add the service principal to your Power BI Workspace:**
   - Open [https://app.powerbi.com](https://app.powerbi.com)
   - Navigate to your workspace → **Manage access**
   - Add the app registration by name (e.g. `PowerBIEmbedApp`) with **Member** or **Admin** role

8. **Enable service principals in Power BI tenant settings** *(one-time, done by Power BI admin):*
   - Go to Power BI Admin portal → **Tenant settings**
   - Under **Developer settings** → **Allow service principals to use Power BI APIs** → Enable
   - Optionally restrict to a specific security group

---

## 3. Configure Web.config

Open `WebApplication1\Web.config` and replace the placeholder values in the `<appSettings>` block:

```xml
<!-- Power BI Configuration -->
<add key="PowerBi:ClientId"            value="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" />
<add key="PowerBi:ClientSecret"        value="your~actual~secret~value" />
<add key="PowerBi:TenantId"            value="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" />
<add key="PowerBi:Scope"               value="https://analysis.windows.net/powerbi/api/.default" />
<add key="PowerBi:ApiRoot"             value="https://api.powerbi.com" />

<!-- Default parameter values sent on every initial report load -->
<add key="PowerBi:DefaultUserId"           value="SystemUser" />
<add key="PowerBi:DefaultPeriod"           value="2024.FY" />
<add key="PowerBi:DefaultRegion"           value="All" />
<add key="PowerBi:DefaultConvertCurrency"  value="0" />
```

### Key descriptions

| Key | Description |
|---|---|
| `PowerBi:ClientId` | Application (client) ID from Azure AD app registration |
| `PowerBi:ClientSecret` | Client secret value from Azure AD app registration |
| `PowerBi:TenantId` | Directory (tenant) ID from Azure AD |
| `PowerBi:Scope` | OAuth scope — do not change unless using a sovereign cloud |
| `PowerBi:ApiRoot` | Power BI REST API root — do not change unless using a sovereign cloud |
| `PowerBi:DefaultUserId` | Passed as the `UserId` built-in parameter in every embed payload |
| `PowerBi:DefaultPeriod` | Initial period filter applied when the report first loads |
| `PowerBi:DefaultRegion` | Initial region filter (`All` means no region filter is applied) |
| `PowerBi:DefaultConvertCurrency` | `0` = off, `1` = on |

> **Security note:** Never commit real secrets to source control. Use a `Web.user.config` override or environment variables in production.

---

## 4. Configure Reports in the View

Report workspace and report GUIDs are configured in `Views\Report\ReportViewer.cshtml` inside the `reportConfigs` JavaScript array.

Open the file and locate this block near the bottom:

```javascript
var reportConfigs = [
    // { name: "Sales Report", workspaceGuid: "YOUR-WORKSPACE-GUID", reportGuid: "YOUR-REPORT-GUID" }
];
```

Replace the comment with your actual GUIDs:

```javascript
var reportConfigs = [
    {
        name: "Sales Report",
        workspaceGuid: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        reportGuid:    "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy"
    },
    {
        name: "Finance Dashboard",
        workspaceGuid: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        reportGuid:    "zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz"
    }
];
```

### How to find your Workspace and Report GUIDs

**From the Power BI Service URL:**
- Open your report in [https://app.powerbi.com](https://app.powerbi.com)
- The URL will look like:
  ```
  https://app.powerbi.com/groups/{workspaceGuid}/reports/{reportGuid}/...
  ```
- Copy both GUIDs from that URL

**Via REST API:**
```
GET https://api.powerbi.com/v1.0/myorg/groups
GET https://api.powerbi.com/v1.0/myorg/groups/{workspaceGuid}/reports
```

---

## 5. Install / Restore NuGet Packages

All packages are already declared in `packages.config`. If the `packages` folder is missing (e.g. after a fresh clone), restore them:

**Option A — Visual Studio (recommended):**
- Right-click the solution in Solution Explorer
- Select **Restore NuGet Packages**

**Option B — Package Manager Console (inside Visual Studio):**
```powershell
Update-Package -reinstall
```

**Option C — Command line using the bundled nuget.exe:**
```powershell
cd C:\Users\AKPC\source\repos\WebApplication1
.\nuget.exe restore WebApplication1\WebApplication1.csproj -PackagesDirectory packages
```

### Required NuGet packages (already in packages.config)

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.PowerBI.Api` | 4.19.0 | Power BI REST API client |
| `Microsoft.Identity.Client` | 4.61.3 | Azure AD token acquisition (MSAL) |
| `Microsoft.Rest.ClientRuntime` | 2.3.21 | HTTP client used by PowerBI.Api |
| `Microsoft.AspNet.WebApi` | 5.2.9 | Web API controller support |
| `Newtonsoft.Json` | 13.0.3 | JSON serialization |
| `Microsoft.AspNet.Mvc` | 5.2.9 | MVC framework |

---

## 6. Build and Run

1. Open `WebApplication1.sln` (or `WebApplication1.slnx`) in Visual Studio
2. Ensure `WebApplication1` is set as the **Startup Project** (right-click → Set as Startup Project)
3. Press **F5** or click the green **IIS Express** run button
4. The browser will open at `https://localhost:44392`
5. Navigate to the Report Viewer:
   ```
   https://localhost:44392/Report/ReportViewer
   ```
6. Select a report from the **Report** dropdown — the embed will load if your GUIDs and credentials are correct

> The port `44392` is set in the project properties. If it differs on your machine, check **Project Properties → Web → IIS Express → Project URL**.

---

## 7. Test the API Endpoints

### GetPowerBIReport — GET
Returns the embed token, report metadata, and initial filter parameters.

**Browser or curl:**
```bash
curl -k "https://localhost:44392/api/PowerBI/GetPowerBIReport?workspaceGuid=YOUR-WORKSPACE-GUID&reportGuid=YOUR-REPORT-GUID"
```

**Expected response shape:**
```json
{
  "Report": {
    "Id": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    "Name": "Sales Report",
    "EmbedUrl": "https://app.powerbi.com/reportEmbed?reportId=..."
  },
  "EmbedToken": {
    "Token": "H4sI...",
    "Expiration": "2024-12-31T12:00:00Z"
  },
  "InitialParameters": [
    { "Name": "UserId",          "Value": "SystemUser" },
    { "Name": "DefaultPeriod",   "Value": "2024.FY" },
    { "Name": "DefaultRegion",   "Value": "All" },
    { "Name": "ConvertCurrency", "Value": "0" }
  ],
  "InitialFilters": [
    { "Table": "FactDate",  "Column": "ReportingPeriod", "Values": ["2024.FY"] },
    { "Table": "DimRegion", "Column": "RegionName",      "Values": ["All"] },
    { "Table": "FactTable", "Column": "ConvertCurrency",  "Values": ["0"] }
  ]
}
```

**Error responses:**

| HTTP Code | Cause |
|---|---|
| 400 | Missing or invalid GUID parameters |
| 500 | Missing `Web.config` keys (`PowerBi:ClientId` etc.) |
| 502 | Power BI API rejected the request (wrong GUIDs, insufficient permissions) |

---

### EmbedReport — POST
Primary embed endpoint. Returns embed token, report metadata, initial filter definitions,
and optionally updates dataset M parameters when explicitly provided.

```bash
curl -k -X POST "https://localhost:44392/api/PowerBI/EmbedReport" \
     -H "Content-Type: application/json" \
     -d "{\"WorkspaceGuid\":\"YOUR-WORKSPACE-GUID\",\"ReportGuid\":\"YOUR-REPORT-GUID\",\"DatasetParameters\":[],\"RefreshDataset\":false}"
```

To update dataset M parameters (e.g. after changing UserId), include them in the payload:

```bash
curl -k -X POST "https://localhost:44392/api/PowerBI/EmbedReport" \
     -H "Content-Type: application/json" \
     -d "{\"WorkspaceGuid\":\"YOUR-WORKSPACE-GUID\",\"ReportGuid\":\"YOUR-REPORT-GUID\",\"DatasetParameters\":[{\"Name\":\"UserId\",\"Value\":\"jdoe\"}],\"RefreshDataset\":true}"
```

---

### ResolveMapping — POST
Validates a UI control value and returns dataset/stored proc parameter names.

```bash
curl -k -X POST "https://localhost:44392/api/PowerBI/ResolveMapping" \
     -H "Content-Type: application/json" \
     -d "{\"ControlName\":\"ddlRegion\",\"UiValue\":\"North\"}"
```

**Multi-select example:**
```bash
curl -k -X POST "https://localhost:44392/api/PowerBI/ResolveMapping" \
     -H "Content-Type: application/json" \
     -d "{\"ControlName\":\"ddlRegion\",\"UiValue\":[\"North\",\"South\"]}"
```

**Expected response:**
```json
{
  "DatasetValues":    ["North"],
  "DatasetParamName": "RegionParam",
  "StoredProcParamName": "@Region",
  "ReportTable":      "DimRegion",
  "ReportColumn":     "RegionName"
}
```

---

### GetMappings — GET
Returns the full mapping table (useful for debugging).

```bash
curl -k "https://localhost:44392/api/PowerBI/GetMappings"
```

---

## 8. Add a New UI Control and Mapping

### Step 1 — Add the mapping entry in MappingService.cs

Open `Services\MappingService.cs` and add an entry to `BuildDefaultMappings()`:

```csharp
{
    "ddlCountry", new MappingEntry
    {
        UIControl      = "ddlCountry",
        ReportTable    = "DimGeography",
        ReportColumn   = "CountryName",
        DatasetParam   = "CountryParam",
        StoredProcParam = "@Country",
        AllowedValues  = new[] { "USA", "UK", "Germany", "France", "All" }
    }
}
```

> **AllowedValues** acts as a server-side whitelist. Set it to `null` or an empty array to skip validation.

### Step 2 — Add the HTML control in ReportViewer.cshtml

```html
<div class="col-auto control-group">
    <label for="ddlCountry">Country:</label>
    <select id="ddlCountry" class="form-select form-select-sm d-inline-block w-auto">
        <option value="">-- All Countries --</option>
        <option value="USA">USA</option>
        <option value="UK">UK</option>
        <option value="Germany">Germany</option>
        <option value="France">France</option>
    </select>
</div>
```

### Step 3 — Wire the change event in site.js

```javascript
$('#ddlCountry').on('change', function () {
    var val = $(this).val();
    if (!report) return;
    if (!val) return;
    resolveAndApplyFilter('ddlCountry', val);
});
```

That is all. The `resolveAndApplyFilter` function calls `/api/PowerBI/ResolveMapping` automatically, builds the Power BI basic filter, and applies it to the embedded report.

---

## 9. Mapping Reference

| UI Control | Report Table | Report Column | Dataset Param | Stored Proc Param |
|---|---|---|---|---|
| `ddlRegion` | `DimRegion` | `RegionName` | `RegionParam` | `@Region` |
| `ddlPeriod` | `FactDate` | `ReportingPeriod` | `PeriodParam` | `@ReportingPeriod` |
| `chkConvertCurrency` | `FactTable` | `ConvertCurrency` | `ConvertCurrency` | `@ConvertCurrency` |

The full mapping table is defined in `Services\MappingService.cs` inside `BuildDefaultMappings()`.

---

## 10. Validation

After configuring `Web.config` and `reportConfigs`, verify the setup:

1. **Build** the solution in Visual Studio — it should compile with zero errors
2. **Run** the application (F5) and confirm the browser opens
3. **Navigate** to `/Report/ReportViewer` (the home page redirects there automatically)
4. **Select** a report from the Report dropdown — it should embed successfully in the container
5. **Change** the Region or Period dropdown — the report should update instantly without a page reload
6. **Toggle** the Convert Currency checkbox — the report should re-filter immediately
7. **Check** the browser DevTools console (F12) for any JavaScript errors — there should be none
8. **Click Re-embed** after changing the UserId field — the report should re-embed with the new UserId

---

## 11. Project Structure

```
WebApplication1/
├── App_Start/
│   ├── BundleConfig.cs          — Script/style bundles
│   ├── FilterConfig.cs          — MVC global filters
│   ├── RouteConfig.cs           — MVC routes
│   └── WebApiConfig.cs          — Web API routes + JSON formatter
│
├── Controllers/
│   ├── HomeController.cs        — Redirects to ReportViewer
│   ├── PowerBIController.cs     — API: EmbedReport, GetPowerBIReport, ResolveMapping, GetMappings
│   └── ReportController.cs      — MVC: serves the ReportViewer page
│
├── Models/
│   └── PowerBiModels.cs         — DTOs: EmbedResponse, MappingEntry, MappingResult, etc.
│
├── Services/
│   ├── PowerBiService.cs        — MSAL token acquisition + embed token generation
│   └── MappingService.cs        — UI control → dataset param → stored proc param mapping
│
├── Scripts/
│   └── site.js                  — Client-side embed, filter, and event logic
│
├── Views/
│   └── Report/
│       └── ReportViewer.cshtml  — Report viewer page (dropdowns, embed container)
│
├── Web.config                   — App settings including Power BI credentials
├── README.md                    — This file
└── DEPLOY_CHECKLIST.md          — Pre-deployment verification steps
```

---

## 12. Known Limitations and Assumptions

- **Power BI artifacts must already exist.** The app does not create workspaces, reports, or datasets.
- **Stored procedure must already accept the mapped parameters.** The app passes filter values to Power BI; the dataset/DirectQuery handles the stored proc call.
- **Token expiry is 60 minutes** (Power BI embed token default). The client retries `loadReport` automatically when an auth error is detected, but a full page interaction is needed to trigger the refresh.
- **AllowedValues in MappingService are hardcoded.** If your report's allowed values change, update `MappingService.BuildDefaultMappings()` and redeploy.
- **No authentication on the web app itself.** The app currently has no login page. Add ASP.NET Identity or Windows Authentication before any shared/production deployment.
- **Client secret in Web.config.** For production, use Azure Key Vault, environment variables, or `Web.Release.config` transforms to avoid storing secrets in source control.
- **Single workspace per app instance.** Multiple workspaces are supported by adding entries to `reportConfigs` in the view; the workspace GUID is passed per report.
- **Sovereign clouds** (GCC, GCC High, China) require changing `PowerBi:ApiRoot` and `PowerBi:Scope` to the appropriate endpoints.

---

## 13. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| HTTP 500.19 on startup | Malformed `Web.config` | Ensure `<handlers>` is inside `<system.webServer>`, not `<system.web>` |
| `ConfigurationError` from API | Missing `Web.config` keys | Fill in all four `PowerBi:*` keys in `Web.config` |
| HTTP 502 from API | Wrong GUIDs or insufficient permissions | Verify workspace/report GUIDs; confirm service principal has workspace Member role and admin consent granted |
| `AADSTS700016` token error | App registration not found in tenant | Confirm `PowerBi:TenantId` matches the tenant where the app is registered |
| `AADSTS7000215` secret error | Wrong or expired client secret | Regenerate the secret in Azure AD and update `Web.config` |
| Report container blank | Service principal not added to workspace | Add app to Power BI workspace with Member/Admin role |
| `PowerBI is not defined` in browser | CDN script failed to load | Check browser console; ensure internet access or host `powerbi.min.js` locally |
| Filters not applying | Table/column name mismatch | Open Power BI Desktop, check the exact `Table` and `Column` names — they are case-sensitive in the filter API |
| 404 on `/api/PowerBI/...` | Web API not registered | Confirm `GlobalConfiguration.Configure(WebApiConfig.Register)` is called before `RouteConfig` in `Global.asax.cs` |
