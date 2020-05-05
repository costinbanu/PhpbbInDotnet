// Resize media in posts
function onPostLoad() {
    $(".ForumContent").each(function (_, post) {
        var maxWidth = ($(".FlexRow").width() - ($(".Summary").is(":visible") ? $(".Summary").outerWidth() : 0)) * 0.95;
        var maxHeight = $(window).innerHeight() - $("#topBanner").outerHeight() - 20;
        //console.log("max size: w=" + maxWidth + ", h=" + maxHeight + ". parent width = " + $(post).parent().outerWidth());
        $(post).find("img").each(function (_, img) {
            var originalWidth = img.naturalWidth,
                originalHeight = img.naturalHeight,
                ratio = Math.min(maxHeight / originalHeight, maxWidth / originalWidth);
            if (ratio < 1) {
                $(img).css({ "width": Math.round(originalWidth * ratio) + "px", "height": Math.round(originalHeight * ratio) + "px" });
                if (!$(img).parent().is("a")) {
                    $(img).attr({ "onclick": "window.open(this.src);", "title": "Click pentru imaginea mărită." });
                    $(img).css("cursor", "pointer");
                }
            }
        });
        $(post).find("iframe").each(function (_, frame) {
            $(frame).attr({ "width": maxWidth, "height": Math.max(Math.round(maxHeight / 1.8), Math.round((maxWidth) * 9 / 16)) });
            var src = $(frame).attr("src");
            if (src.indexOf("imgur") !== -1) {
                $(frame).attr("src", src + "?w=" + maxWidth)
            }
        });
    });
}

//Expand collapsed menus
function expandCollapsedMenu(summaryId, buttonId) {
    var summary = $("#" + summaryId);
    var button = $("#" + buttonId);

    if (!summary.is(":visible")) {
        var position = button.offset();
        summary.css({
            "right": "40px",
            "top": position.top + button.height() + 10 + "px"
        });
        summary.show("fast", function () { });
    }
    else {
        summary.hide("fast", function () { });
    }
}


function showElement(id, whenHiding, whenShowing) {
    var elem = $("#" + id);
    if (!whenHiding) {
        whenHiding = function () { }
    }
    if (!whenShowing) {
        whenShowing = function () { }
    }

    if (elem.is(":visible")) {
        elem.hide("fast", whenHiding);
    }
    else {
        elem.show("fast", whenShowing);
    }
}

function writeDate(dateString, format) {
    var date = new Date(dateString);
    if (!format) {
        format = "dddd, dd.MM.yyyy HH: mm";
    }
    document.write(date.format(format));
}
