// Resize media in posts
function onPostLoad() {
    var posts = $(".ForumContent");
    for (var i = 0; i < posts.length; i++) {
        var summary = $(posts[i]).parent().find(".Summary");
        var w = summary.parent().innerWidth();
        if (summary.is(":visible")) {
            w -= summary.outerWidth();
        }
        var imgs = posts[i].getElementsByTagName("img");
        //var w = posts[i].offsetWidth;
        var h = window.innerHeight;
        for (var j = 0; j < imgs.length; j++) {
            var oh = imgs[j].naturalHeight,
                ow = imgs[j].naturalWidth,
                x = h / oh,
                y = w / ow;
            imgs[j].style.maxWidth = w + "px";
            imgs[j].style.maxHeight = h + "px";
            if (x < 1 || y < 1) {
                imgs[j].style.width = Math.round(0.9 * ow * Math.min(x, y)) + "px";
                imgs[j].style.height = Math.round(0.9 * oh * Math.min(x, y)) + "px";
                if (imgs[j].parentNode.nodeName !== "A") {
                    imgs[j].setAttribute("onclick", "window.open(this.src);");
                    imgs[j].setAttribute("title", "Click pentru imaginea marita.");
                    imgs[j].style.cursor = "pointer";
                }
            }
        }
        var frames = posts[i].getElementsByTagName("iframe");
        for (var k = 0; k < frames.length; k++) {
            frames[k].width = w - 20;
            frames[k].height = Math.max(Math.round(h / 1.8), Math.round((w - 20) * 9 / 16));
            frames[k].style.width = w - 20 + "px";
            frames[k].style.height = Math.max(Math.round(h / 1.8), Math.round((w - 20) * 9 / 16)) + "px";
            if (frames[k].src.indexOf("imgur") !== -1)
                frames[k].src += "?w=" + (w - 20);
        }
    }
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
