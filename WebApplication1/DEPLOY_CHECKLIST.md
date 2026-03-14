# Deployment Checklist — Power BI Embedded MVC Application

Complete every item before deploying to a shared environment.

---

## 1. Azure AD & Power BI

- [ ] `PowerBi:ClientId` in `Web.config` matches the **Application (client) ID** in the Azure AD app registration
- [ ] `PowerBi:TenantId` in `Web.config` matches the **Directory (tenant) ID** in the Azure AD app registration
- [ ] `PowerBi:ClientSecret` is valid and **has not expired** — check expiry in Azure AD → Certificates & secrets
- [ ] The Azure AD app has been granted **Power BI Service** application permissions: `Dataset.Read.All`, `Report.Read.All`, `Workspace.Read.All`
- [ ] **Admin consent has been granted** for those permissions in Azure AD
- [ ] The service principal (app registration) has been added to the Power BI workspace as **Member** or **Admin**
- [ ] Power BI tenant setting **"Allow service principals to use Power BI APIs"** is enabled

---

## 2. Web.config

- [ ] All four `PowerBi:*` credential keys contain real values — no `YOUR-*` placeholders remain
- [ ] `PowerBi:DefaultPeriod` value exists in the Period dropdown options
- [ ] `PowerBi:DefaultRegion` value is either `All` or exists in the Region dropdown options
- [ ] Client secret is **not committed to source control**

---

## 3. Report Configuration

- [ ] `reportConfigs` array in `Views\Report\ReportViewer.cshtml` contains at least one entry with valid workspace and report GUIDs
- [ ] Each workspace GUID matches a workspace the service principal can access
- [ ] Each report GUID exists inside the corresponding workspace

---

## 4. Mapping Verification

- [ ] `ddlRegion` allowed values in `MappingService.cs` match the actual `DimRegion[RegionName]` values in the Power BI dataset
- [ ] `ddlPeriod` allowed values match the actual `FactDate[ReportingPeriod]` values in the dataset
- [ ] `chkConvertCurrency` values (`0`, `1`) match `FactTable[ConvertCurrency]` in the dataset
- [ ] Table and column names in `MappingService.cs` **exactly match** the Power BI data model (case-sensitive)
- [ ] Stored procedure parameters (`@Region`, `@ReportingPeriod`, `@ConvertCurrency`) accept the mapped values

---

## 5. Build & Verification

- [ ] Solution builds with **zero errors** in Release configuration
- [ ] `GET /api/PowerBI/GetPowerBIReport?workspaceGuid=...&reportGuid=...` returns HTTP 200 with a valid embed token
- [ ] The ReportViewer page loads without JavaScript console errors
- [ ] Changing the Region dropdown updates the embedded report without a page reload
- [ ] Changing the Period dropdown updates the embedded report without a page reload

---

## 6. Security (before production deployment)

- [ ] Add authentication to the web application (Windows Auth, ASP.NET Identity, or Azure AD sign-in)
- [ ] Move `PowerBi:ClientSecret` out of `Web.config` into Azure Key Vault or an environment variable
- [ ] Enable HTTPS and configure an SSL certificate
- [ ] Review IIS application pool identity and restrict file system permissions
