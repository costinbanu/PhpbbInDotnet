class ViewTopic {
    constructor(postId, highlight, scrollToModPanel, moveTopic, moveSelectedPosts, splitSelectedPosts) {
        this.postId = postId;
        this.highlight = highlight;
        this.scrollToModPanel = scrollToModPanel;
        this.moveTopic = moveTopic;
        this.moveSelectedPosts = moveSelectedPosts;
        this.splitSelectedPosts = splitSelectedPosts;
    }

    onLoad() {
        if (this.postId != -1) {
            var element = $("#" + this.postId);
            var elementTop = element.offset().top;
            var headerHeight = $("#topBanner").outerHeight(true);
            window.scrollTo(0, elementTop > headerHeight ? elementTop - headerHeight : elementTop);
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
}