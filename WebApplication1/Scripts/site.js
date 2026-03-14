var PowerBIApp = (function ($) {
    'use strict';

    var report = null;
    var models = window['powerbi-client'].models;
    var powerbiContainer = null;
    var currentWorkspaceGuid = null;
    var currentReportGuid = null;
    var cachedMappings = null;

    // ---------------------------------------------------------------
    // Init
    // ---------------------------------------------------------------
    function init() {
        powerbiContainer = document.getElementById('reportContainer');
        loadMappingsAndPopulateControls();
        bindFilterEvents();
    }

    // ---------------------------------------------------------------
    // Fetch mappings from server and populate filter dropdowns
    // ---------------------------------------------------------------
    function loadMappingsAndPopulateControls() {
        $.ajax({
            url: '/api/PowerBI/GetMappings',
            method: 'GET',
            dataType: 'json',
            success: function (mappings) {
                cachedMappings = mappings;
                populateSelect('#ddlRegion', mappings['ddlRegion']);
                populateSelect('#ddlPeriod', mappings['ddlPeriod']);
            }
        });
    }

    function populateSelect(selector, mappingEntry) {
        if (!mappingEntry || !mappingEntry.AllowedValues) return;
        var $sel = $(selector);
        var current = $sel.val();
        $sel.empty();
        $.each(mappingEntry.AllowedValues, function (i, val) {
            $sel.append($('<option>', { value: val, text: val }));
        });
        if (current) $sel.val(current);
    }

    // ---------------------------------------------------------------
    // Bind change events — each fires resolveAndApplyFilter
    // ---------------------------------------------------------------
    function bindFilterEvents() {
        $('#ddlRegion').on('change', function () {
            resolveAndApplyFilter('ddlRegion', $(this).val());
        });

        $('#ddlPeriod').on('change', function () {
            resolveAndApplyFilter('ddlPeriod', $(this).val());
        });

        $('#chkConvertCurrency').on('change', function () {
            resolveAndApplyFilter('chkConvertCurrency', $(this).is(':checked') ? '1' : '0');
        });
    }

    // ---------------------------------------------------------------
    // Call ResolveMapping to get Power BI table/column targets, then
    // apply (or clear) a BasicFilter via the JS filter API
    // ---------------------------------------------------------------
    function resolveAndApplyFilter(controlName, uiValue) {
        if (!report) return;

        $.ajax({
            url: '/api/PowerBI/ResolveMapping',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ ControlName: controlName, UiValue: uiValue }),
            dataType: 'json',
            success: function (result) {
                applyReportFilter(result);
            },
            error: function (xhr) {
                var msg = 'Filter update failed.';
                if (xhr.responseJSON && xhr.responseJSON.Details) {
                    msg += ' ' + xhr.responseJSON.Details;
                }
                showError(msg);
            }
        });
    }

    function applyReportFilter(mappingResult) {
        var values = mappingResult.DatasetValues;
        var isClear = !values || values.length === 0 ||
            (values.length === 1 && values[0].toLowerCase() === 'all');

        report.getFilters().then(function (current) {
            // Remove any existing filter for this exact table + column
            var remaining = current.filter(function (f) {
                return !(f.target &&
                    f.target.table === mappingResult.ReportTable &&
                    f.target.column === mappingResult.ReportColumn);
            });

            if (!isClear) {
                remaining.push({
                    $schema: 'http://powerbi.com/product/schema#basic',
                    target: {
                        table: mappingResult.ReportTable,
                        column: mappingResult.ReportColumn
                    },
                    operator: 'In',
                    values: values,
                    filterType: models.FilterType.Basic
                });
            }

            return report.setFilters(remaining);
        }).catch(function (err) {
            showError('Failed to apply filter: ' + (err && err.message ? err.message : err));
        });
    }

    // ---------------------------------------------------------------
    // Collect dataset M parameters for re-embed (only UserId).
    // Period, Region, and ConvertCurrency are applied as report-level
    // filters at embed time via buildEmbedFilters().
    // ---------------------------------------------------------------
    function collectDatasetParameters() {
        var params = [];

        var userId = $.trim($('#paramUserId').val());
        if (userId) params.push({ Name: 'UserId', Value: userId });

        return params;
    }

    // ---------------------------------------------------------------
    // Full embed / re-embed — used on first load and UserId changes
    // ---------------------------------------------------------------
    function loadReport(workspaceGuid, reportGuid, updateDatasetParams) {
        clearError();
        showSpinner();
        currentWorkspaceGuid = workspaceGuid;
        currentReportGuid = reportGuid;

        var payload = {
            WorkspaceGuid: workspaceGuid,
            ReportGuid: reportGuid,
            DatasetParameters: updateDatasetParams ? collectDatasetParameters() : [],
            RefreshDataset: updateDatasetParams ? $('#chkRefresh').is(':checked') : false
        };

        $.ajax({
            url: '/api/PowerBI/EmbedReport',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            dataType: 'json',
            success: function (response) {
                embedReport(response);
            },
            error: function (xhr) {
                hideSpinner();
                var msg = 'Failed to load report.';
                if (xhr.responseJSON && xhr.responseJSON.Details) {
                    msg += ' ' + xhr.responseJSON.Details;
                } else if (xhr.responseJSON && xhr.responseJSON.Message) {
                    msg += ' ' + xhr.responseJSON.Message;
                }
                showError(msg);
            }
        });
    }

    // ---------------------------------------------------------------
    // Embed using the Power BI JS SDK
    // ---------------------------------------------------------------
    function embedReport(response) {
        var initialFilters = buildEmbedFilters(response.InitialFilters);

        var embedConfig = {
            type: 'report',
            id: response.Report.Id,
            embedUrl: response.Report.EmbedUrl,
            accessToken: response.EmbedToken.Token,
            tokenType: models.TokenType.Embed,
            permissions: models.Permissions.All,
            filters: initialFilters,
            settings: {
                panes: {
                    filters: { expanded: false, visible: true },
                    pageNavigation: { visible: true }
                },
                background: models.BackgroundType.Transparent
            }
        };

        report = powerbi.embed(powerbiContainer, embedConfig);

        report.off('loaded');
        report.on('loaded', function () {
            hideSpinner();
            // Enable filter controls — only live once a report is embedded
            $('#ddlRegion, #ddlPeriod, #chkConvertCurrency').prop('disabled', false);
        });

        report.off('error');
        report.on('error', function (event) {
            hideSpinner();
            var errorDetail = event.detail || {};
            if (isTokenExpiredError(errorDetail)) {
                refreshTokenAndReembed();
            } else {
                showError('Report error: ' + (errorDetail.message || 'Unknown error'));
            }
        });

        report.off('rendered');
        report.on('rendered', function () {
            hideSpinner();
        });
    }

    function isTokenExpiredError(error) {
        if (!error) return false;
        var msg = (error.message || error.detailedMessage || '').toLowerCase();
        return msg.indexOf('token') >= 0 && msg.indexOf('expir') >= 0;
    }

    function refreshTokenAndReembed() {
        if (currentWorkspaceGuid && currentReportGuid) {
            showError('Session expired. Refreshing token...');
            loadReport(currentWorkspaceGuid, currentReportGuid);
        }
    }

    function showSpinner() { $('#spinner').show(); }
    function hideSpinner() { $('#spinner').hide(); }
    function showError(msg) { $('#errorMessage').text(msg); $('#errorBanner').show(); }
    function clearError() { $('#errorBanner').hide(); }

    // ---------------------------------------------------------------
    // Build Power BI BasicFilter objects for embed-time application.
    // Prefers current UI dropdown state + cached mapping targets.
    // Falls back to server-provided InitialFilters on first load.
    // ---------------------------------------------------------------
    function buildEmbedFilters(serverFilters) {
        var filters = [];

        // Prefer current UI state + cached mapping targets
        if (cachedMappings) {
            addDropdownFilter(filters, cachedMappings['ddlPeriod'], '#ddlPeriod');
            addDropdownFilter(filters, cachedMappings['ddlRegion'], '#ddlRegion');
            addCheckboxFilter(filters, cachedMappings['chkConvertCurrency'], '#chkConvertCurrency');
            return filters;
        }

        // Fallback: use server-provided initial filters
        if (serverFilters && serverFilters.length > 0) {
            $.each(serverFilters, function (i, f) {
                if (!f.Table || !f.Column || !f.Values || f.Values.length === 0) return;
                var isClear = f.Values.length === 1 && f.Values[0].toLowerCase() === 'all';
                if (isClear) return;
                filters.push({
                    $schema: 'http://powerbi.com/product/schema#basic',
                    target: { table: f.Table, column: f.Column },
                    operator: 'In',
                    values: f.Values,
                    filterType: models.FilterType.Basic
                });
            });
        }

        return filters;
    }

    function addDropdownFilter(filters, mapping, selector) {
        if (!mapping) return;
        var val = $.trim($(selector).val());
        if (!val || val.toLowerCase() === 'all') return;
        filters.push({
            $schema: 'http://powerbi.com/product/schema#basic',
            target: { table: mapping.ReportTable, column: mapping.ReportColumn },
            operator: 'In',
            values: [val],
            filterType: models.FilterType.Basic
        });
    }

    function addCheckboxFilter(filters, mapping, selector) {
        if (!mapping) return;
        var val = $(selector).is(':checked') ? '1' : '0';
        filters.push({
            $schema: 'http://powerbi.com/product/schema#basic',
            target: { table: mapping.ReportTable, column: mapping.ReportColumn },
            operator: 'In',
            values: [val],
            filterType: models.FilterType.Basic
        });
    }

    $(document).ready(function () { init(); });

    return { loadReport: loadReport };

})(jQuery);
