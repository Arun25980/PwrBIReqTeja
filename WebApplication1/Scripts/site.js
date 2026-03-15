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
    // Bind change events — each fires a re-embed so parameterValues
    // are applied with the latest UI selections
    // ---------------------------------------------------------------
    function bindFilterEvents() {
        $('#ddlRegion').on('change', function () {
            if (currentWorkspaceGuid && currentReportGuid) {
                loadReport(currentWorkspaceGuid, currentReportGuid);
            }
        });

        $('#ddlPeriod').on('change', function () {
            if (currentWorkspaceGuid && currentReportGuid) {
                loadReport(currentWorkspaceGuid, currentReportGuid);
            }
        });

        $('#chkConvertCurrency').on('change', function () {
            if (currentWorkspaceGuid && currentReportGuid) {
                loadReport(currentWorkspaceGuid, currentReportGuid);
            }
        });
    }

    // ---------------------------------------------------------------
    // Collect dataset M parameters for re-embed (only UserId).
    // Period, Region, and ConvertCurrency are applied as parameterValues
    // at embed time via buildEmbedParamValues().
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
        var paramValues = buildEmbedParamValues(response.InitialParameters);

        var embedConfig = {
            type: 'report',
            id: response.Report.Id,
            embedUrl: response.Report.EmbedUrl,
            accessToken: response.EmbedToken.Token,
            tokenType: models.TokenType.Embed,
            permissions: models.Permissions.All,
            parameterValues: paramValues,
            settings: {
                filterPaneEnabled: true,
                navContentPaneEnabled: true
            }
        };

        report = powerbi.embed(powerbiContainer, embedConfig);

        report.off('loaded');
        report.on('loaded', function () {
            hideSpinner();
            // Enable parameter controls — only live once a report is embedded
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
    // Build Power BI parameterValues for embed-time application.
    // Prefers current UI state + cached mapping dataset param names.
    // Falls back to server-provided InitialParameters on first load.
    // ---------------------------------------------------------------
    function buildEmbedParamValues(serverParameters) {
        var params = [];

        // Prefer current UI state + cached mapping dataset param names
        if (cachedMappings) {
            addDropdownParam(params, cachedMappings['ddlPeriod'], '#ddlPeriod');
            addDropdownParam(params, cachedMappings['ddlRegion'], '#ddlRegion');
            addCheckboxParam(params, cachedMappings['chkConvertCurrency'], '#chkConvertCurrency');
            return params;
        }

        // Fallback: use server-provided initial parameters
        if (serverParameters && serverParameters.length > 0) {
            $.each(serverParameters, function (i, p) {
                if (!p.Name || p.Value === null || p.Value === undefined) return;
                params.push({ name: p.Name, value: p.Value });
            });
        }

        return params;
    }

    function addDropdownParam(params, mapping, selector) {
        if (!mapping || !mapping.DatasetParam) return;
        var val = $.trim($(selector).val());
        if (val === '') return;
        params.push({ name: mapping.DatasetParam, value: val });
    }

    function addCheckboxParam(params, mapping, selector) {
        if (!mapping || !mapping.DatasetParam) return;
        var val = $(selector).is(':checked') ? 1 : 0;
        params.push({ name: mapping.DatasetParam, value: val });
    }

    $(document).ready(function () { init(); });

    return { loadReport: loadReport };

})(jQuery);
