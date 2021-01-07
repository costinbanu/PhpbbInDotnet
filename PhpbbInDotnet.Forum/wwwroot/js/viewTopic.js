class ViewTopic {
    constructor(postId, scrollToModPanel, moveTopic, moveSelectedPosts, splitSelectedPosts, otherReportReasonId, dictionary) {
        this.postId = postId;
        this.scrollToModPanel = scrollToModPanel;
        this.moveTopic = moveTopic;
        this.moveSelectedPosts = moveSelectedPosts;
        this.splitSelectedPosts = splitSelectedPosts;
        this.otherReportReasonId = otherReportReasonId;
        this.dictionary = dictionary;
    }

    onLoad() {
        if (this.postId != -1) {
            var element = $("#" + this.postId);
            var elementTop = element.offset().top;
            window.scrollTo(0, elementTop - 20);
        }
        if (this.scrollToModPanel) {
            document.getElementById('moderatorForm').scrollIntoView();
        }
    }

    switchPollPanels(id1, id2, button) {
        var show = this.dictionary['SHOW_RESULTS'];
        var vote = this.dictionary['DO_VOTE'];
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
            return confirm(this.dictionary['RECEIVED_COMMAND'] + ' "' + $(actionSelect).find(':selected').text() + '". ' + this.dictionary['CONTINUE']);
        }
        return true;
    }

    showMessageDetails(ip, editTime, timeFormat, editCount, editUser) {
        var content = '<b>IP:</b> ' + ip + '<br/>';
        if (editCount > 0) {
            content = content + '<b>' + this.dictionary['LAST_CHANGED'] + '</b> ' + new Date(editTime).format(timeFormat) + ', <b>' + this.dictionary['CHANGED_BY'] + '</b> ' + editUser + '<br /> ' +
                '<b>' + this.dictionary['TOTAL_CHANGES'] + '</b> ' + editCount + '<br/>';
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
        var reason = $('#reportReason').val();
        var details = $('#reportDetails').val();
        var validation = $('#reportValidation');
        if (reason) {
            if (reason == this.otherReportReasonId && (!details || details.length < 3)) {
                validation.text(this.dictionary['FILL_REPORT_DETAILS']);
                validation.show();
                return false;
            }
            return true;
        }
        else {
            validation.text(this.dictionary['CHOOSE_REPORT_REASON']);
            validation.show();
            return false;
        }
    }

    showReportViewer(postId, reportId, reportReasonTitle, reportReasonDescription, reportDetails, reportUsername) {
        $('#reportViewerReportPostId').val(postId);
        $('#reportViewerReportReasonTitle').text(reportReasonTitle);
        $('#reportViewerReportReasonDescription').text(reportReasonDescription);
        $('#reportViewerReportId').val(reportId);
        $('#reportViewerReportDetails').html(reportDetails);
        $('#reportViewerReporter').text(reportUsername);
        $('#reportViewerEditMessage').prop("checked", false);
        $('#reportViewerDeleteMessage').prop("checked", false);
        showElement('reportViewer', null, null, true);
    }

    confirmDeleteReportedPost() {
        if ($('#reportViewerDeleteMessage').is(':checked')) {
            return confirm(this.dictionary['CONFIRM_DELETE_REPORTED_MESSAGE']);
        }
        return true;
    }

    deletePost(postId, closestPostId) {
        $("input[name='PostIdsForModerator']").each(function (_, elem) {
            if ($(elem).val() == postId) {
                $(elem).attr('checked', '');
            }
            else {
                $(elem).removeAttr('checked');
            }
        });
        $('#PostAction').val('DeleteSelectedPosts');
        if (this.confirmAction('#PostAction')) {
            if (closestPostId) {
                $('#closestPostId').val(closestPostId);
            }
            $('#moderatorForm').submit();
        }
    }
}