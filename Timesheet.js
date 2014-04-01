timesheet_reports = {
    supressTimeTypes: false,
    supressResources: false,
    currentListValues: null,
    init: function (opts) {

        timesheet_reports.settings = opts;

        $("#Reports").change(function (e) {
            timesheet_reports.reportChanged(e);
        });
        gemini_ui.datePicker(".datepicker", timesheet_reports.reportChanged);

        $(".datepicker").change(function (e) {
            timesheet_reports.reportChanged(e);
        });

        $("#SubChart").change(function (e) {
            timesheet_reports.reportChanged(e);
        });

        //var options = jQuery.parseJSON(gemini_commons.htmlDecode(gemini_appnav.pageCard.Options['A95F5FEC-1477-4909-AF13-8E600383B03A']));

        //gemini_ui.setDropdownValue("#Reports", options.SelectedReport);
        //$("#Reports").val(options.SelectedReport); This seems to work?

        /*$("#FieldId").change(function (e) {
            timesheet_reports.reportChanged(e);
        });
        $("#FilterId").change(function (e) {
            timesheet_reports.reportChanged(e);
        });*/
        $("#ProjectIds").change(function (e) {
            timesheet_reports.projectChanged();
            timesheet_reports.reportChanged(e);
        });
        $("#TimeTypeIds").change(function (e) {
            timesheet_reports.supressTimeTypes = true;
            timesheet_reports.reportChanged(e);
        });
        $("#ResourceIds").change(function (e) {
            timesheet_reports.supressResources = true;
            timesheet_reports.reportChanged(e);
        });
        $("#GroupBy").change(function (e) {
            timesheet_reports.reportChanged(e);
        });

        /*$(".report-summary-table-button", "#report-menu").click(function () {
            $("#ProjectSummaryChart").val("false");
            $(".report-summary-chart").fadeOut('fast', function () {
                $(".report-summary-table").fadeIn('fast');
            });
        });
        $(".report-summary-chart-button", "#report-menu").click(function () {
            $("#ProjectSummaryChart").val("true");
            $(".report-summary-table").fadeOut('fast', function () {
                $(".report-summary-chart").fadeIn('fast');
            });
        });*/
        //timesheet_reports.options = options;
        //timesheet_reports.setupControls();
        timesheet_reports.reportChanged();

        $("#Reports_chosen input[type='text']:first").focus();
        $('.control-icon', '#report-menu').click(function () {
            var _this = $(this);
            var options = _this.find('+ .options');

            if (options.is(":visible")) {
                options.hide();
                gemini_keyboard.unbindEscape("#page-options-box .options");
            }
            else {
                _this.parent().parent().find(".options").each(function () {
                    if ($(this).is(":visible")) {
                        $(this).hide();
                    }
                });

                options.show();
                gemini_keyboard.bindEscape("#page-options-box .options", timesheet_reports.escapeDropdowns);
                $("input[type='text']:first", options).focus();
            }

            options.position({
                "of": _this,
                "my": "right top",
                "at": "right bottom",
                "offset": "0 0",
                "collision": "none"
            });
        });

    },
    escapeDropdowns: function (guid, selector) {
        $(selector).hide();
        gemini_keyboard.unbindEscape(guid);
    },
    settings: null,
    options: null,
    reportChanged: function (e) {

        gemini_ui.cursorWait();
        $("#report-content").html("<div id='cs-template'><div id='no-data'>" + timesheet_reports.settings.Loading + "</div></div>");

        gemini_ajax.postCall("apps/Timesheet", "get", timesheet_reports.displayReport, null, $("#ReportParams").serialize(), null, true);

    },
    displayReport: function (response) {
        gemini_ui.cursorDefault();
        if (response == "Not Found") {
            $("#report-content").empty();
            
            return;
        }
        if (response.Result.SavedCard != null) {
            gemini_appnav.pageCard.Options['A95F5FEC-1477-4909-AF13-8E600383B03A'] = response.Result.SavedCard.Options['A95F5FEC-1477-4909-AF13-8E600383B03A'];
            timesheet_reports.options = jQuery.parseJSON(response.Result.SavedCard.Options['A95F5FEC-1477-4909-AF13-8E600383B03A']);
        }
        timesheet_reports.setupControls();
        $("#report-content").html(response.Result.Html);

        // Time types
        timesheet_reports.setTimeTypes(response.Result.TimeTypes);
        timesheet_reports.setResources(response.Result.Resources);

        $(window).trigger("resize");
    },
    setTimeTypes: function (data) {
        if (timesheet_reports.supressTimeTypes) {
            timesheet_reports.supressTimeTypes = false;
            return;
        }
        timesheet_reports.supressTimeTypes = false;
        //get preselected list
        var presel = "|";
        $("#TimeTypeIds").find("option:selected").each(function () {
            presel += $(this).attr('value') + "|";
        });
        $("#TimeTypeIds").empty();

        for (var i = 0; i < data.length; i++) {
            var item = data[i];

            var selected = item.Selected ? "selected" : ""; //presel.indexOf(item.Value) != -1 ? "selected" : "";
            $("#TimeTypeIds").append("<option " + selected + " value='" + item.Value + "'>" + item.Text + "</option>");
        }

        gemini_ui.chosenUpdate('#TimeTypeIds');
    },

    setResources: function (data) {
        if (timesheet_reports.supressResources) {
            timesheet_reports.supressResources = false;
            return;
        }
        timesheet_reports.supressResources = false;
        //get preselected list
        var presel = "";
        $("#ResourceIds").find("option:selected").each(function () {
            presel += $(this).attr('value');
        });
        $("#ResourceIds").empty();

        for (var i = 0; i < data.length; i++) {
            var item = data[i];

            var selected = item.Selected ? "selected" : "";//presel.indexOf(item.Value) != -1 ? "selected" : "";
            $("#ResourceIds").append("<option " + selected + " value='" + item.Value + "'>" + item.Text + "</option>");
        }

        gemini_ui.chosenUpdate('#ResourceIds');
    },

    setupControls: function () {
        var options = timesheet_reports.options;
        if (options.StartDate) {
            $("#StartDate").show().val(options.StartDateDisplay);
        } else {
            $("#StartDate").hide();
        }
        if (options.EndDate) {
            $("#EndDate").show().val(options.EndDateDisplay);
        } else {
            $("#EndDate").hide();
        }
        
        //console.info("pdf url", options.PdfUrl);
        //$("#pdf-action").data("pdf", options.PdfUrl);
        gemini_ui.cursorDefault();

        //$("#ProjectIds").change(function (e) {
        timesheet_reports.currentListValues = $('#ProjectIds').val();
    },
    projectChanged: function () {
        var value = $('#ProjectIds').val();
        var any = $('option:first', $('#ProjectIds')).val();
        if (value != null && value != undefined && value.length > 1 && value.indexOf(any) != -1) {
            if (timesheet_reports.currentListValues != null && timesheet_reports.currentListValues != undefined && timesheet_reports.currentListValues.indexOf(any) != -1) {
                var newSelected = $('#ProjectIds').val();
                newSelected.splice(0, 1);
                $('#ProjectIds').val(newSelected);
            }
            else {
                $('#ProjectIds').val(any);
            }
        }
        else if (value == null || value == undefined) {
            $('#ProjectIds').val(any);
        }

        gemini_ui.chosenUpdate($('#ProjectIds'));
        timesheet_reports.currentListValues = $('#ProjectIds').val();
    }
};