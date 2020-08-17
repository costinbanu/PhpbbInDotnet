var maxWidth, maxHeight;
var quoteWidthBleed, quoteHeightBleed;

function resizeImage(img, customMaxWidth, customMaxHeight) {
    lazyInit(customMaxWidth, customMaxHeight);
    var quotes = $(img).parents("blockquote").length;
    var actualParentWidth = maxWidth - quotes * quoteWidthBleed,
        actualParentHeight = maxHeight - quotes * quoteHeightBleed;
    var originalWidth = img.naturalWidth,
        originalHeight = img.naturalHeight,
        ratio = Math.min(actualParentHeight / originalHeight, actualParentWidth/ originalWidth);
    if (ratio < 1) {
        $(img).css({ 'width': roundToNextEvenNumber(originalWidth * ratio) + 'px', 'height': roundToNextEvenNumber(originalHeight * ratio) + 'px' });
        if (!$(img).parent().is('a')) {
            $(img).attr({ 'onclick': 'window.open(this.src);', 'title': 'Click pentru imaginea mărită.' });
            $(img).css('cursor', 'pointer');
        }
    }
}

function resizeIFrame(frame, customMaxWidth, customMaxHeight) {
    lazyInit(customMaxWidth, customMaxHeight);
    var quotes = $(frame).parents("blockquote").length;
    var actualParentWidth = maxWidth - quotes * quoteWidthBleed,
        actualParentHeight = maxHeight - quotes * quoteHeightBleed;
    $(frame).attr({ 'width': roundToNextEvenNumber(actualParentWidth), 'height': Math.max(roundToNextEvenNumber(actualParentHeight / 1.8), roundToNextEvenNumber((actualParentWidth) * 9 / 16)) });
    var src = $(frame).attr('src');
    if (src.indexOf('imgur') !== -1) {
        $(frame).attr('src', src + '?w=' + actualParentWidth)
    }
}

function lazyInit(customMaxWidth, customMaxHeight) {
    quoteWidthBleed = quoteWidthBleed || $('.PostQuote').outerWidth(true) - $('.PostQuote').width() || 42;
    quoteHeightBleed = quoteHeightBleed || $('.PostQuote').outerHeight(true) - $('.PostQuote').height() || 62;
    maxWidth = maxWidth || customMaxWidth || roundToNextEvenNumber($('.FlexRow').width() - ($('.Summary').is(':visible') ? $('.Summary').outerWidth() * 1.1 : 2));
    maxHeight = maxHeight || customMaxHeight || roundToNextEvenNumber($(window).innerHeight() - $('#topBanner').outerHeight());
}

//Expand collapsed menus
function expandCollapsedMenu(summaryId, buttonId, containerIsFixed = false) {
    var summary = $('#' + summaryId);
    var button = $('#' + buttonId);

    if (!summary.is(':visible')) {
        var top = containerIsFixed ? button.position().top + 5 : button.offset().top;
        var right = $(window).innerWidth() - (containerIsFixed ? button.position().left + button.outerWidth() : button.offset().left) - button.outerWidth();
        summary.css({
            'right': right + 'px', //containerIsFixed ? '20px' : '40px',
            'top': top + button.height() + 10 + 'px',
        });
        summary.show('fast', function () { });
    }
    else {
        summary.hide('fast', function () { });
    }
}


function showElement(id, whenHiding, whenShowing, roundSize = false) {
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
        if (roundSize) {
            elem.height(roundToNextEvenNumber(elem.height()));
            elem.width(roundToNextEvenNumber(elem.width()));
        }
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
