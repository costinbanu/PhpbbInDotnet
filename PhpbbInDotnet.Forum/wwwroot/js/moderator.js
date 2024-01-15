class Moderator {
    constructor(moveTopic, moveSelectedPosts, splitSelectedPosts, createShortcut) {
        this.moveTopic = moveTopic;
        this.moveSelectedPosts = moveSelectedPosts;
        this.splitSelectedPosts = splitSelectedPosts;
        this.createShortcut = createShortcut;
    }

    showTopicExtraInput(source) {
        if ($(source).val() == this.moveTopic || $(source).val() == this.createShortcut) {
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

    appendToStringList(checkbox, targetId) {
        let target = $(`#${targetId}`);
        let cur = target.val().split(',');
        let value = $(checkbox).val();
        if (checkbox.checked) {
            cur.push(value.toString());
        } else {
            let index = cur.indexOf(postId.toString());
            if (index > -1) {
                cur.splice(index, 1);
            }
        }
        target.val(cur.join(','));
    }

    confirmAction(actionSelect) {
        let action = $(actionSelect).val();
        if (action.startsWith('Delete')) {
            return confirm(dictionary.ViewTopic['RECEIVED_COMMAND'] + ' "' + $(actionSelect).find(':selected').text() + '". ' + dictionary.ViewTopic['CONTINUE']);
        }
        return true;
    }

    deletePost(postId, closestPostId) {
        $("input[name='PostIdsForModerator']").each(function (_, elem) {
            if ($(elem).val() == postId) {
                $(elem).prop('checked', true);
            }
            else {
                $(elem).prop('checked', false);
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

    duplicatePost(postId) {
        if (confirm(dictionary.ViewTopic['RECEIVED_COMMAND'] + ' "' + dictionary.Moderator['DUPLICATE_POST'] + '". ' + dictionary.ViewTopic['CONTINUE'])) {
            $('#PostIdForDuplication').val(postId);
            $('#moderatorDuplicateMessageForm').submit();
        }
    }
}