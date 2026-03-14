using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class PowerBiService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _tenantId;
        private readonly string _scope;
        private readonly string _apiRoot;

        public PowerBiService()
        {
            _clientId = ConfigurationManager.AppSettings["PowerBi:ClientId"] ?? string.Empty;
            _clientSecret = ConfigurationManager.AppSettings["PowerBi:ClientSecret"] ?? string.Empty;
            _tenantId = ConfigurationManager.AppSettings["PowerBi:TenantId"] ?? string.Empty;
            _scope = ConfigurationManager.AppSettings["PowerBi:Scope"] ?? "https://analysis.windows.net/powerbi/api/.default";
            _apiRoot = ConfigurationManager.AppSettings["PowerBi:ApiRoot"] ?? "https://api.powerbi.com";
        }

        public PowerBiService(string clientId, string clientSecret, string tenantId, string scope, string apiRoot)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _tenantId = tenantId;
            _scope = scope;
            _apiRoot = apiRoot;
        }

        public void ValidateConfiguration()
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(_clientId)) missing.Add("PowerBi:ClientId");
            if (string.IsNullOrWhiteSpace(_clientSecret)) missing.Add("PowerBi:ClientSecret");
            if (string.IsNullOrWhiteSpace(_tenantId)) missing.Add("PowerBi:TenantId");

            if (missing.Any())
            {
                throw new InvalidOperationException(
                    "Missing Power BI configuration keys in Web.config appSettings: " +
                    string.Join(", ", missing));
            }
        }

        public async Task<string> AcquireTokenAsync(CancellationToken ct = default)
        {
            ValidateConfiguration();

            string authority = $"https://login.microsoftonline.com/{_tenantId}";

            var app = ConfidentialClientApplicationBuilder
                .Create(_clientId)
                .WithClientSecret(_clientSecret)
                .WithAuthority(new Uri(authority))
                .Build();

            string[] scopes = { _scope };
            try
            {
                var result = await app.AcquireTokenForClient(scopes).ExecuteAsync(ct);
                Trace.TraceInformation("Power BI token acquired. Expires: {0}", result.ExpiresOn);
                return result.AccessToken;
            }
            catch (MsalException ex)
            {
                Trace.TraceError("MSAL token acquisition failed: {0}", ex.Message);
                throw;
            }
        }

        public async Task<EmbedResponse> GetEmbedDataAsync(
            Guid workspaceId, Guid reportId,
            List<EmbedParameter> datasetParameters = null,
            bool refreshDataset = false,
            CancellationToken ct = default)
        {
            string accessToken = await AcquireTokenAsync(ct);

            var tokenCredentials = new TokenCredentials(accessToken, "Bearer");
            using (var client = new PowerBIClient(new Uri(_apiRoot), tokenCredentials))
            {
                var report = await client.Reports.GetReportInGroupAsync(workspaceId, reportId, ct);
                Trace.TraceInformation("Retrieved report '{0}' (Id: {1})", report.Name, report.Id);

                var datasetId = report.DatasetId;

                // Update dataset parameters if provided (feeds stored procedure params)
                if (datasetParameters != null && datasetParameters.Count > 0)
                {
                    var updateDetails = datasetParameters
                        .Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Value != null)
                        .Select(p => new UpdateMashupParameterDetails(p.Name, p.Value))
                        .ToList();

                    if (updateDetails.Count > 0)
                    {
                        var updateRequest = new UpdateMashupParametersRequest(updateDetails);
                        await client.Datasets.UpdateParametersInGroupAsync(
                            workspaceId, datasetId, updateRequest, ct);
                        Trace.TraceInformation(
                            "Updated {0} dataset parameter(s) for dataset {1}",
                            updateDetails.Count, datasetId);
                    }
                }

                // Optionally refresh dataset (required for Import mode after parameter update)
                if (refreshDataset)
                {
                    await client.Datasets.RefreshDatasetInGroupAsync(
                        workspaceId, datasetId, cancellationToken: ct);
                    Trace.TraceInformation("Triggered dataset refresh for {0}", datasetId);
                }

                var generateTokenRequest = new GenerateTokenRequestV2
                {
                    Reports = new List<GenerateTokenRequestV2Report>
                    {
                        new GenerateTokenRequestV2Report(reportId)
                    },
                    Datasets = new List<GenerateTokenRequestV2Dataset>
                    {
                        new GenerateTokenRequestV2Dataset(datasetId)
                    }
                };

                var embedToken = await client.EmbedToken.GenerateTokenAsync(generateTokenRequest, ct);
                Trace.TraceInformation("Embed token generated. Expiry: {0}", embedToken.Expiration);

                return new EmbedResponse
                {
                    Report = new Models.EmbedReport
                    {
                        Id = report.Id.ToString(),
                        Name = report.Name,
                        EmbedUrl = report.EmbedUrl
                    },
                    EmbedToken = new Models.EmbedToken
                    {
                        Token = embedToken.Token,
                        Expiration = embedToken.Expiration
                    }
                };
            }
        }
    }
}
