class ViewTopic {
    constructor(postId, highlight, scrollToModPanel, moveTopic, moveSelectedPosts, splitSelectedPosts, otherReportReasonId) {
        this.postId = postId;
        this.highlight = highlight;
        this.scrollToModPanel = scrollToModPanel;
        this.moveTopic = moveTopic;
        this.moveSelectedPosts = moveSelectedPosts;
        this.splitSelectedPosts = splitSelectedPosts;
        this.otherReportReasonId = otherReportReasonId;
    }

    onLoad() {
        if (this.postId != -1) {
            var element = $("#" + this.postId);
            var elementTop = element.offset().top;
            window.scrollTo(0, elementTop - 20);
            var container = element.parent().parent();
            if (container && this.highlight) {
                container.addClass("Highlight");
            }
        }
        if (this.scrollToModPanel) {
            document.getElementById('moderatorForm').scrollIntoView();
        }
    }

    switchPollPanels(id1, id2, button) {
        showElement(
            id1,
            function () {
                button.value = "Arată rezultatele";
            },
            function () {
                button.value = "Votează";
            }
        );
        showElement(id2);
    }

    showTopicExtraInput(source) {
        if ($(source).val() == this.moveTopic) {
            $('#extraInputPostForum').hide("fast");
            $('#extraInputPostTopic').hide("fast");
            $('#extraInputTopic').show("fast");
        }
        $('#topicAction').val($(source).val());
        $('#showTopicSelector').val('false');
    }

    showPostExtraInput(source) {
        if ($(source).val() == this.moveSelectedPosts) {
            $('#extraInputPostForum').hide("fast");
            $('#extraInputTopic').hide("fast");
            $('#extraInputPostTopic').show("fast");
            $('#showTopicSelector').val('true');
        } else if ($(source).val() == this.splitSelectedPosts) {
            $('#extraInputTopic').hide("fast");
            $('#extraInputPostTopic').hide("fast");
            $('#extraInputPostForum').show("fast");
            $('#showTopicSelector').val('false');
        }
        $('#postAction').val($(source).val());
    }

    appendPostId(checkbox, postId) {
        var cur = $('#selectedPostIds').val().split(',');
        if (checkbox.checked) {
            cur.push(postId.toString());
        } else {
            var index = cur.indexOf(postId.toString());
            if (index > -1) {
                cur.splice(index, 1);
            }
        }
        $('#selectedPostIds').val(cur.join(','));
    }

    confirmAction(actionSelect) {
        var action = $(actionSelect).val();
        if (action.startsWith('Delete')) {
            return confirm("Am primit comanda '" + action + "'. Continuați?");
        }
        return true;
    }

    showMessageDetails(ip) {
        $('#postInfoContent').html('<b>IP:</b> ' + ip);
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
        var reason = $('#reportReason').val();
        var details = $('#reportDetails').val();
        var validation = $('#reportValidation');
        if (reason) {
            if (reason == this.otherReportReasonId && (!details || details.length < 3)) {
                validation.text('Completează detaliile raportului (min. 3 caractere).');
                validation.show();
                return false;
            }
            return true;
        }
        else {
            validation.text('Alege un motiv pentru raport.');
            validation.show();
            return false;
        }
    }

    showReportViewer(postId, reportId, reportReasonTitle, reportReasonDescription, reportDetails, reportUsername) {
        $('#reportViewerReportPostId').val(postId);
        $('#reportViewerReportReasonTitle').text(reportReasonTitle);
        $('#reportViewerReportReasonDescription').text(reportReasonDescription);
        $('#reportViewerReportId').val(reportId);
        $('#reportViewerReportDetails').text(reportDetails);
        $('#reportViewerReporter').text(reportUsername);
        $('#reportViewerEditMessage').prop("checked", false);
        $('#reportViewerDeleteMessage').prop("checked", false);
        showElement('reportViewer', null, null, true);
    }

    confirmDeleteReportedPost() {
        if ($('#reportViewerDeleteMessage').is(':checked')) {
            return confirm('Ai ales să ștergi mesajul raportat. Ești sigur?');
        }
        return true;
    }
}