var maxWidth, maxHeight;
var quoteWidthBleed, quoteHeightBleed;

function resizeIFrame(frame, customMaxWidth, customMaxHeight) {
    lazyInit(customMaxWidth, customMaxHeight);
    let quotes = $(frame).parents("blockquote").length;
    let actualParentWidth = maxWidth - quotes * quoteWidthBleed,
        actualParentHeight = maxHeight - quotes * quoteHeightBleed;
    $(frame).attr({ 'width': roundToNextEvenNumber(actualParentWidth), 'height': Math.max(roundToNextEvenNumber(actualParentHeight / 1.8), roundToNextEvenNumber((actualParentWidth) * 9 / 16)) });
    let src = $(frame).attr('src');
    if (src.indexOf('imgur') !== -1) {
        $(frame).attr('src', src + '?w=' + actualParentWidth)
    }
}

function openImageInNewWindowOnClick(img) {
    if (!$(img).parent().is('a')) {
        $(img).attr({ 'onclick': 'window.open(this.src);', 'title': clickToEnlarge });
        $(img).css('cursor', 'pointer');
    }
}

function lazyInit(customMaxWidth, customMaxHeight) {
    quoteWidthBleed = quoteWidthBleed || $('.PostQuote').outerWidth(true) - $('.PostQuote').width() || 42;
    quoteHeightBleed = quoteHeightBleed || $('.PostQuote').outerHeight(true) - $('.PostQuote').height() || 62;
    maxWidth = maxWidth || customMaxWidth || roundToNextEvenNumber($('.FlexRow').width() - ($('.Summary').is(':visible') ? $('.Summary').outerWidth() * 1.1 : 2));
    maxHeight = maxHeight || customMaxHeight || roundToNextEvenNumber($(window).innerHeight() - 15);
}

//Expand collapsed menus
function expandCollapsedMenu(menuId, buttonId, left = false) {
    let summary = $('#' + menuId);
    let button = $('#' + buttonId);

    if (!summary.is(':visible')) {
        let top = button.offset().top + button.height() + 10;
        if (left) {
            let left = button.offset().left;
            summary.css({
                'left': left + 'px',
                'top': top + 'px'
            });
        }
        else {
            let right = $(window).innerWidth() - button.offset().left - button.outerWidth();
            summary.css({
                'right': right + 'px',
                'top': top + 'px'
            });
        }
        summary.show('fast', function () { });
    }
    else {
        summary.hide('fast', function () { });
    }
}


function showElement(id, whenHiding, whenShowing, roundSize = false) {
    let elem = $('#' + id);

    if (!whenHiding) {
        whenHiding = function () { }
    }
    if (!whenShowing) {
        whenShowing = function () { }
    }

    if (elem.is(':visible')) {
        elem.hide('fast', whenHiding);
    }
    else {
        if (roundSize) {
            elem.height(roundToNextEvenNumber(elem.height()));
            elem.width(roundToNextEvenNumber(elem.width()));
        }
        elem.show('fast', whenShowing);
    }
}

function roundToNextEvenNumber(value) {
    value = Math.round(value);
    if (value % 2 == 1) {
        value = Math.round(value / 2) * 2;
    }
    return value;
}

function showForumTree(caller) {
    showElement('forumTree', null, () => {
        $('html, body').scrollTop($(caller).offset().top - $('#topBanner').outerHeight() - 10);
        $('#treeContainer').scrollTop($('.selectedTreeNode').offset().top - $('#treeContainer').offset().top - 50);
    });
}

function toggleHeaderLinks(curId, otherId) {
    let other = $('#' + otherId);
    let cur = $('#' + curId);
    let curButton = $('#' + curId + 'Button');
    let otherButton = $('#' + otherId + 'Button');
    if (other.length && other.is(':visible') && !cur.is(':visible')) {
        showElement(
            otherId,
            function () {
                otherButton.css('font-weight', 'normal');
            },
            null
        );
    }
    showElement(
        curId,
        function () {
            if (otherButton.length) {
                curButton.css('font-weight', 'normal');
            }
        },
        function () {
            if (otherButton.length) {
                curButton.css('font-weight', 'bold');
            }
        }
    );
}

function selectAllCheckboxes(parentId = null) {
    let checkboxes = parentId ? $(`#${parentId}`).find('input[type=checkbox]') : $('input[type=checkbox]');
    checkboxes.prop('checked', !checkboxes.prop('checked'));
}

function formatString(format, ...args) {
    if (!format) {
        return '';
    }
    if (args === undefined || args.length == 0) {
        return format;
    }
    for (i = 0; i < args.length; i++) {
        format = format.replace('{' + i + '}', args[i]);
    }
    return format;
}

function getDayNames(lang) {
    let toReturn = [];
    let baseDate = new Date(Date.UTC(2021, 0, 10, 0, 0, 0)); //must be a sunday
    for (i = 0; i < 7; i++, baseDate.setDate(baseDate.getDate() + 1)) {
        toReturn.push(baseDate.toLocaleString(lang, { weekday: "long" }));
    }
    return toReturn;
}

function getMonthNames(lang) {
    let toReturn = [];
    let baseDate = new Date(Date.UTC(2021, 0, 1, 0, 0, 0));
    for (i = 0; i < 12; i++, baseDate.setMonth(baseDate.getMonth() + 1)) {
        toReturn.push(baseDate.toLocaleString(lang, { month: "long" }));
    }
    return toReturn;
}

function expandSection(sectionId, charHolderId) {
    showElement(
        sectionId,
        () => {
            $('#' + charHolderId).html('&#x1F53D;');
        },
        () => {
            $('#' + charHolderId).html('&#x1F53C;');
        }
    )
}

function enableCollapsibles() {
    let coll = document.getElementsByClassName("collapsible");
    let i;

    for (i = 0; i < coll.length; i++) {
        coll[i].addEventListener("click", function () {
            this.classList.toggle("active");
            var content = this.nextElementSibling;
            if (content.style.display === "block") {
                content.style.display = "none";
            } else {
                content.style.display = "block";
            }
        });
    }
}

function appendToStringList(checkbox, targetId) {
    let target = $(`#${targetId}`);
    let cur = target.val().split(',');
    let value = $(checkbox).val();
    if (checkbox.checked) {
        cur.push(value.toString());
    } else {
        let index = cur.indexOf(value.toString());
        if (index > -1) {
            cur.splice(index, 1);
        }
    }
    target.val(cur.join(','));
}