class ViewTopic {
    constructor(postId, scrollToModPanel, otherReportReasonId) {
        this.postId = postId;
        this.scrollToModPanel = scrollToModPanel;
        this.otherReportReasonId = otherReportReasonId;
    }

    onLoad() {
        if (this.postId != -1) {
            let element = $("#" + this.postId);
            let elementTop = element.offset().top;
            window.scrollTo(0, elementTop - 20);
        }
        if (this.scrollToModPanel) {
            document.getElementById('moderatorForm').scrollIntoView();
        }
    }

    switchPollPanels(id1, id2, button) {
        let show = dictionary.ViewTopic['SHOW_RESULTS'];
        let vote = dictionary.ViewTopic['DO_VOTE'];
        showElement(
            id1,
            function () {
                button.value = show;
            },
            function () {
                button.value = vote;
            }
        );
        showElement(id2);
    }

    

    showMessageDetails(ip, editTime, timeFormat, editCount, editUser, encodedReports) {
        let content = `<b>IP:</b> ${ip} [<a href='./IPLookup?ip=${ip}' target='_blank'>${dictionary.ViewTopic['DO_SEARCH']}</a>]<br/>`;
        if (editCount > 0) {
            content = content +
                `<b>${dictionary.ViewTopic['LAST_CHANGED']}</b> ${new Date(editTime).format(timeFormat)} <b>${dictionary.ViewTopic['CHANGED_BY']}</b> ${editUser}<br />
                 <b>${dictionary.ViewTopic['TOTAL_CHANGES']}</b> ${editCount}<br/>`;
        }
        if (encodedReports) {
            let reports = JSON.parse(atob(encodedReports));
            if (reports.length) {
                content = content + `<hr class='BoxSeparator' /><h4>${dictionary.ViewTopic['REPORTS']}</h4>`
                let first = true;
                for (const report of reports) {
                    if (!first) {
                        content = content + "<hr class='SubtypeSeparator' />";
                    }
                    first = false;

                    let statusKey = report.ReportClosed === 1 ? 'REPORT_CLOSED' : 'REPORT_OPEN';
                    content = content +
                        `<b>${dictionary.ViewTopic['REPORT_REASON']}:</b> ${he.decode(report.ReasonTitle)} (${he.decode(report.ReasonDescription)})<br />
                         <b>${dictionary.ViewTopic['REPORT_TIME']}:</b> ${new Date(report.ReportDateTime).format(timeFormat)}<br />
                         <b>${dictionary.ViewTopic['REPORT_DETAILS']}:</b> ${he.decode(report.Details)}<br />
                         <b>${dictionary.ViewTopic['REPORTING_USER']}:</b> ${he.decode(report.ReporterUsername)}<br />
                         <b>${dictionary.ViewTopic['REPORT_STATUS']}:</b> ${he.decode(dictionary.ViewTopic[statusKey])}`;
                }
            }
        }
        $('#postInfoContent').html(content);
        showElement('postInfo', null, null, true);
    }

    showReportForm(postId, reportId = null, reportReasonId = null, reportDetails = null) {
        $('#reportPostId').val(postId);
        $('#reportReason').val(reportReasonId ? reportReasonId : '-1');
        $('#reportId').val(reportId);
        $('#reportDetails').val(reportDetails);
        $('#reportValidation').hide();
        showElement('report', null, null, true);
    }

    validateReportForm() {
        let reason = $('#reportReason').val();
        let details = $('#reportDetails').val();
        let validation = $('#reportValidation');
        if (reason) {
            if (reason == this.otherReportReasonId && (!details || details.length < 3)) {
                validation.text(dictionary.ViewTopic['FILL_REPORT_DETAILS']);
                validation.show();
                return false;
            }
            return true;
        }
        else {
            validation.text(dictionary.ViewTopic['CHOOSE_REPORT_REASON']);
            validation.show();
            return false;
        }
    }

    showReportViewer(postId, reportId, reportReasonTitle, reportReasonDescription, reportDetails, reportUsername) {
        $('#reportViewerReportPostId').val(postId);
        $('#reportViewerReportReasonTitle').text(he.decode(reportReasonTitle));
        $('#reportViewerReportReasonDescription').text(reportReasonDescription);
        $('#reportViewerReportId').val(reportId);
        $('#reportViewerReportDetails').html(he.decode(reportDetails));
        $('#reportViewerReporter').text(he.decode(reportUsername));
        $('#reportViewerEditMessage').prop("checked", false);
        $('#reportViewerDeleteMessage').prop("checked", false);
        showElement('reportViewer', null, null, true);
    }

    confirmDeleteReportedPost() {
        if ($('#reportViewerDeleteMessage').is(':checked')) {
            return confirm(dictionary.ViewTopic['CONFIRM_DELETE_REPORTED_MESSAGE']);
        }
        return true;
    }   
}