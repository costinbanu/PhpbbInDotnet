// Resize media in posts
function onPostLoad() {
    $('.ForumContent').each(function (_, post) {
        var maxWidth = ($('.FlexRow').width() - ($('.Summary').is(':visible') ? $('.Summary').outerWidth() : 0)) - 20;
        var maxHeight = $(window).innerHeight() - $('#topBanner').outerHeight() - 40;
        $(post).find('img').each(function (_, img) {
            var originalWidth = img.naturalWidth,
                originalHeight = img.naturalHeight,
                ratio = Math.min(maxHeight / originalHeight, maxWidth / originalWidth);
            if (ratio < 1) {
                $(img).css({ 'width': Math.round(originalWidth * ratio) + 'px', 'height': Math.round(originalHeight * ratio) + 'px' });
                if (!$(img).parent().is('a')) {
                    $(img).attr({ 'onclick': 'window.open(this.src);', 'title': 'Click pentru imaginea mărită.' });
                    $(img).css('cursor', 'pointer');
                }
            }
        });
        $(post).find('iframe').each(function (_, frame) {
            $(frame).attr({ 'width': maxWidth, 'height': Math.max(Math.round(maxHeight / 1.8), Math.round((maxWidth) * 9 / 16)) });
            var src = $(frame).attr('src');
            if (src.indexOf('imgur') !== -1) {
                $(frame).attr('src', src + '?w=' + maxWidth)
            }
        });
    });
}

//Expand collapsed menus
function expandCollapsedMenu(summaryId, buttonId, containerIsFixed = false) {
    var summary = $('#' + summaryId);
    var button = $('#' + buttonId);

    if (!summary.is(':visible')) {
        var top = containerIsFixed ? button.position().top + 10 : button.offset().top;
        summary.css({
            'right': containerIsFixed ? '20px' : '40px',
            'top': top + button.height() + 10 + 'px',
        });
        summary.show('fast', function () { });
    }
    else {
        summary.hide('fast', function () { });
    }
}


function showElement(id, whenHiding, whenShowing) {
    var elem = $('#' + id);
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
        elem.height(roundToNextEvenNumber(elem.height()));
        elem.show('fast', whenShowing);
    }
}

function writeDate(dateString, format) {
    var date = new Date(dateString);
    if (!format) {
        format = 'dddd, dd.MM.yyyy HH: mm';
    }
    document.write(date.format(format));
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
