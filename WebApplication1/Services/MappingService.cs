using System;
using System.Collections.Generic;
using System.Linq;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class MappingService
    {
        private readonly Dictionary<string, MappingEntry> _mappings;

        public MappingService()
        {
            _mappings = BuildDefaultMappings();
        }

        public MappingService(Dictionary<string, MappingEntry> customMappings)
        {
            _mappings = customMappings ?? throw new ArgumentNullException(nameof(customMappings));
        }

        private static Dictionary<string, MappingEntry> BuildDefaultMappings()
        {
            return new Dictionary<string, MappingEntry>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "ddlRegion", new MappingEntry
                    {
                        UIControl = "ddlRegion",
                        ReportTable = "DimRegion",
                        ReportColumn = "RegionName",
                        DatasetParam = "RegionParam",
                        StoredProcParam = "@Region",
                        AllowedValues = new[] { "North", "South", "East", "West", "Central", "All" }
                    }
                },
                {
                    "ddlPeriod", new MappingEntry
                    {
                        UIControl = "ddlPeriod",
                        ReportTable = "FactDate",
                        ReportColumn = "ReportingPeriod",
                        DatasetParam = "PeriodParam",
                        StoredProcParam = "@ReportingPeriod",
                        AllowedValues = new[]
                        {
                            "2020.FY", "2021.FY", "2022.FY", "2023.FY", "2024.FY",
                            "2020.Q1", "2020.Q2", "2020.Q3", "2020.Q4",
                            "2021.Q1", "2021.Q2", "2021.Q3", "2021.Q4",
                            "2022.Q1", "2022.Q2", "2022.Q3", "2022.Q4",
                            "2023.Q1", "2023.Q2", "2023.Q3", "2023.Q4",
                            "2024.Q1", "2024.Q2", "2024.Q3", "2024.Q4"
                        }
                    }
                },
                {
                    "chkConvertCurrency", new MappingEntry
                    {
                        UIControl = "chkConvertCurrency",
                        ReportTable = "FactTable",
                        ReportColumn = "ConvertCurrency",
                        DatasetParam = "ConvertCurrency",
                        StoredProcParam = "@ConvertCurrency",
                        AllowedValues = new[] { "0", "1" }
                    }
                }
            };
        }

        public MappingResult ResolveUiValues(string controlName, object uiValue)
        {
            if (string.IsNullOrWhiteSpace(controlName))
                throw new ArgumentException("Control name cannot be null or empty.", nameof(controlName));

            string trimmedName = controlName.Trim();

            if (!_mappings.TryGetValue(trimmedName, out MappingEntry entry))
                throw new KeyNotFoundException($"No mapping found for control '{trimmedName}'.");

            string[] rawValues = NormalizeInput(uiValue);

            if (rawValues == null || rawValues.Length == 0)
                throw new ArgumentException($"No values provided for control '{trimmedName}'.");

            string[] normalized = rawValues
                .Select(v => NormalizeValue(v))
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();

            if (normalized.Length == 0)
                throw new ArgumentException($"All values for control '{trimmedName}' were empty after normalization.");

            ValidateAgainstWhitelist(entry, normalized);

            return new MappingResult
            {
                DatasetValues = normalized,
                DatasetParamName = entry.DatasetParam,
                StoredProcParamName = entry.StoredProcParam,
                ReportTable = entry.ReportTable,
                ReportColumn = entry.ReportColumn
            };
        }

        public bool HasMapping(string controlName)
        {
            return !string.IsNullOrWhiteSpace(controlName) &&
                   _mappings.ContainsKey(controlName.Trim());
        }

        public MappingEntry GetMapping(string controlName)
        {
            if (string.IsNullOrWhiteSpace(controlName))
                return null;

            _mappings.TryGetValue(controlName.Trim(), out MappingEntry entry);
            return entry;
        }

        public IReadOnlyDictionary<string, MappingEntry> GetAllMappings()
        {
            return _mappings;
        }

        private static string[] NormalizeInput(object uiValue)
        {
            if (uiValue == null)
                return new string[0];

            if (uiValue is string str)
                return new[] { str };

            if (uiValue is string[] arr)
                return arr;

            if (uiValue is IEnumerable<object> enumerable)
                return enumerable.Select(o => o?.ToString()).ToArray();

            if (uiValue is Newtonsoft.Json.Linq.JArray jArray)
                return jArray.Select(t => t.ToString()).ToArray();

            return new[] { uiValue.ToString() };
        }

        private static string NormalizeValue(string value)
        {
            if (value == null) return null;
            return value.Trim();
        }

        private static void ValidateAgainstWhitelist(MappingEntry entry, string[] values)
        {
            if (entry.AllowedValues == null || entry.AllowedValues.Length == 0)
                return;

            var allowedSet = new HashSet<string>(entry.AllowedValues, StringComparer.OrdinalIgnoreCase);
            var invalid = values.Where(v => !allowedSet.Contains(v)).ToArray();

            if (invalid.Length > 0)
            {
                throw new ArgumentException(
                    $"Invalid value(s) for '{entry.UIControl}': {string.Join(", ", invalid)}. " +
                    $"Allowed values: {string.Join(", ", entry.AllowedValues)}");
            }
        }
    }
}
