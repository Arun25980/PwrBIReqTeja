using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [RoutePrefix("api/PowerBI")]
    public class PowerBIController : ApiController
    {
        private readonly PowerBiService _powerBiService;
        private readonly MappingService _mappingService;

        public PowerBIController()
        {
            _powerBiService = new PowerBiService();
            _mappingService = new MappingService();
        }

        public PowerBIController(PowerBiService powerBiService, MappingService mappingService)
        {
            _powerBiService = powerBiService ?? throw new ArgumentNullException(nameof(powerBiService));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        }

        /// <summary>
        /// Primary endpoint — generates an embed token and returns embed data
        /// with report-level filter definitions.  Dataset M parameters are only
        /// updated when the caller explicitly provides them (e.g. Re-embed).
        /// </summary>
        [HttpPost]
        [Route("EmbedReport")]
        public async Task<IHttpActionResult> EmbedReport(
            [FromBody] EmbedReportRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.WorkspaceGuid))
                return BadRequest("WorkspaceGuid is required.");

            if (string.IsNullOrWhiteSpace(request.ReportGuid))
                return BadRequest("ReportGuid is required.");

            if (!Guid.TryParse(request.WorkspaceGuid, out Guid workspaceId))
                return BadRequest("WorkspaceGuid is not a valid GUID.");

            if (!Guid.TryParse(request.ReportGuid, out Guid reportId))
                return BadRequest("ReportGuid is not a valid GUID.");

            try
            {
                _powerBiService.ValidateConfiguration();
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceError("Configuration error: {0}", ex.Message);
                return Content(HttpStatusCode.InternalServerError, new ApiErrorResponse
                {
                    Error = "ConfigurationError",
                    Details = ex.Message
                });
            }

            try
            {
                // Only update dataset M parameters when caller explicitly provides them
                List<EmbedParameter> datasetParams = null;
                if (request.DatasetParameters != null && request.DatasetParameters.Count > 0)
                {
                    datasetParams = MergeWithDefaults(request.DatasetParameters);
                }

                var embedData = await _powerBiService.GetEmbedDataAsync(
                    workspaceId, reportId, datasetParams, request.RefreshDataset, ct);

                // Return parameterValues for embed-time application via JS SDK.
                // Always provide defaults so the client can build parameterValues even on first load.
                embedData.InitialParameters = datasetParams ?? BuildInitialParameters();

                return Ok(embedData);
            }
            catch (Microsoft.Rest.HttpOperationException httpEx)
            {
                Trace.TraceError("Power BI API error: {0} - {1}",
                    httpEx.Response?.StatusCode, httpEx.Message);
                return Content(HttpStatusCode.BadGateway, new ApiErrorResponse
                {
                    Error = "PowerBIApiError",
                    Details = httpEx.Response?.Content ?? httpEx.Message
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unexpected error in EmbedReport: {0}", ex.Message);
                return InternalServerError(ex);
            }
        }

        /// <summary>
        /// Lightweight GET — embeds with default parameters from Web.config.
        /// </summary>
        [HttpGet]
        [Route("GetPowerBIReport")]
        public async Task<IHttpActionResult> GetPowerBIReport(
            [FromUri] string workspaceGuid,
            [FromUri] string reportGuid,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(workspaceGuid))
                return BadRequest("workspaceGuid is required.");

            if (string.IsNullOrWhiteSpace(reportGuid))
                return BadRequest("reportGuid is required.");

            if (!Guid.TryParse(workspaceGuid, out Guid workspaceId))
                return BadRequest("workspaceGuid is not a valid GUID.");

            if (!Guid.TryParse(reportGuid, out Guid reportId))
                return BadRequest("reportGuid is not a valid GUID.");

            try
            {
                _powerBiService.ValidateConfiguration();
            }
            catch (InvalidOperationException ex)
            {
                Trace.TraceError("Configuration error: {0}", ex.Message);
                return Content(HttpStatusCode.InternalServerError, new ApiErrorResponse
                {
                    Error = "ConfigurationError",
                    Details = ex.Message
                });
            }

            try
            {
                var embedData = await _powerBiService.GetEmbedDataAsync(
                    workspaceId, reportId, null, false, ct);

                embedData.InitialFilters = BuildInitialFilters();
                embedData.InitialParameters = BuildInitialParameters();
                return Ok(embedData);
            }
            catch (Microsoft.Rest.HttpOperationException httpEx)
            {
                Trace.TraceError("Power BI API error: {0} - {1}",
                    httpEx.Response?.StatusCode, httpEx.Message);
                return Content(HttpStatusCode.BadGateway, new ApiErrorResponse
                {
                    Error = "PowerBIApiError",
                    Details = httpEx.Response?.Content ?? httpEx.Message
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unexpected error in GetPowerBIReport: {0}", ex.Message);
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("ResolveMapping")]
        public IHttpActionResult ResolveMapping([FromBody] ResolveMappingRequest request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.ControlName))
                return BadRequest("ControlName is required.");

            try
            {
                object uiValue = request.UiValue;

                if (uiValue is JArray jArray)
                    uiValue = jArray.ToObject<string[]>();
                else if (uiValue is JValue jValue)
                    uiValue = jValue.Value?.ToString();

                var result = _mappingService.ResolveUiValues(request.ControlName, uiValue);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Content(HttpStatusCode.NotFound, new ApiErrorResponse
                {
                    Error = "MappingNotFound",
                    Details = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error in ResolveMapping: {0}", ex.Message);
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("GetMappings")]
        public IHttpActionResult GetMappings()
        {
            var mappings = _mappingService.GetAllMappings();
            return Ok(mappings);
        }

        private List<EmbedParameter> BuildInitialParameters()
        {
            return new List<EmbedParameter>
            {
                new EmbedParameter
                {
                    Name = "UserId",
                    Value = ConfigurationManager.AppSettings["PowerBi:DefaultUserId"] ?? "SystemUser"
                },
                new EmbedParameter
                {
                    Name = "ReportingPeriod",
                    Value = ConfigurationManager.AppSettings["PowerBi:DefaultPeriod"] ?? "2024.FY"
                },
                new EmbedParameter
                {
                    Name = "UnitSegment",
                    Value = ConfigurationManager.AppSettings["PowerBi:DefaultRegion"] ?? "All"
                },
                new EmbedParameter
                {
                    Name = "ConvertCurrency",
                    Value = ConfigurationManager.AppSettings["PowerBi:DefaultConvertCurrency"] ?? "0"
                }
            };
        }

        /// <summary>
        /// Builds report-level BasicFilter definitions from the mapping service and
        /// Web.config default values.  The JS client applies these at embed time via
        /// the Power BI JS SDK 'filters' embed config, which is the correct mechanism
        /// for reports whose slicers/filters drive stored procedure parameters.
        /// </summary>
        private List<EmbedFilter> BuildInitialFilters()
        {
            var filters = new List<EmbedFilter>();

            var periodValue = ConfigurationManager.AppSettings["PowerBi:DefaultPeriod"] ?? "2024.FY";
            var periodMapping = _mappingService.GetMapping("ddlPeriod");
            if (periodMapping != null)
            {
                filters.Add(new EmbedFilter
                {
                    Table = periodMapping.ReportTable,
                    Column = periodMapping.ReportColumn,
                    Values = new[] { periodValue }
                });
            }

            var regionValue = ConfigurationManager.AppSettings["PowerBi:DefaultRegion"] ?? "All";
            var regionMapping = _mappingService.GetMapping("ddlRegion");
            if (regionMapping != null)
            {
                filters.Add(new EmbedFilter
                {
                    Table = regionMapping.ReportTable,
                    Column = regionMapping.ReportColumn,
                    Values = new[] { regionValue }
                });
            }

            var currencyValue = ConfigurationManager.AppSettings["PowerBi:DefaultConvertCurrency"] ?? "0";
            var currencyMapping = _mappingService.GetMapping("chkConvertCurrency");
            if (currencyMapping != null)
            {
                filters.Add(new EmbedFilter
                {
                    Table = currencyMapping.ReportTable,
                    Column = currencyMapping.ReportColumn,
                    Values = new[] { currencyValue }
                });
            }

            return filters;
        }

        /// <summary>
        /// Merges caller-supplied parameters with Web.config defaults.
        /// Caller values win when both supply the same parameter name.
        /// </summary>
        private List<EmbedParameter> MergeWithDefaults(List<EmbedParameter> callerParams)
        {
            var defaults = BuildInitialParameters();

            if (callerParams == null || callerParams.Count == 0)
                return defaults;

            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in defaults)
            {
                if (!string.IsNullOrWhiteSpace(d.Name))
                    merged[d.Name] = d.Value;
            }

            foreach (var p in callerParams)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                    merged[p.Name] = p.Value;
            }

            var result = new List<EmbedParameter>();
            foreach (var kvp in merged)
            {
                result.Add(new EmbedParameter { Name = kvp.Key, Value = kvp.Value });
            }

            return result;
        }
    }
}
