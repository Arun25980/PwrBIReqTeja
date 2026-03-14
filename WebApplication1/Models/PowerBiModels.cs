using System;
using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class EmbedReport
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string EmbedUrl { get; set; }
    }

    public class EmbedToken
    {
        public string Token { get; set; }
        public DateTime Expiration { get; set; }
    }

    public class EmbedParameter
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class EmbedFilter
    {
        public string Table { get; set; }
        public string Column { get; set; }
        public string[] Values { get; set; }
    }

    public class EmbedResponse
    {
        public EmbedReport Report { get; set; }
        public EmbedToken EmbedToken { get; set; }
        public List<EmbedParameter> InitialParameters { get; set; }
        public List<EmbedFilter> InitialFilters { get; set; }
    }

    public class ResolveMappingRequest
    {
        public string ControlName { get; set; }
        public object UiValue { get; set; }
    }

    public class MappingResult
    {
        public string[] DatasetValues { get; set; }
        public string DatasetParamName { get; set; }
        public string StoredProcParamName { get; set; }
        public string ReportTable { get; set; }
        public string ReportColumn { get; set; }
    }

    public class MappingEntry
    {
        public string UIControl { get; set; }
        public string ReportTable { get; set; }
        public string ReportColumn { get; set; }
        public string DatasetParam { get; set; }
        public string StoredProcParam { get; set; }
        public string[] AllowedValues { get; set; }
    }

    public class EmbedReportRequest
    {
        public string WorkspaceGuid { get; set; }
        public string ReportGuid { get; set; }
        public List<EmbedParameter> DatasetParameters { get; set; }
        public bool RefreshDataset { get; set; }
    }

    public class ApiErrorResponse
    {
        public string Error { get; set; }
        public string Details { get; set; }
    }
}
