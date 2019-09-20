﻿window.onload = function () {
    var posts = document.getElementsByClassName("ForumListLeft");
    for (var i = 0; i < posts.length; i++) {
        var imgs = posts[i].getElementsByTagName("img");
        var w = posts[i].offsetWidth;
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
                if (imgs[j].parentNode.nodeName != "A") {
                    imgs[j].setAttribute("onclick", "window.open(this.src);");
                    imgs[j].setAttribute("title", "Click pentru imaginea marita.");
                    imgs[j].style.cursor = "pointer";
                }
            }
        }
        var frames = posts[i].getElementsByTagName("iframe");
        for (var j = 0; j < frames.length; j++) {
            frames[j].width = (w - 20);
            frames[j].height = Math.max(Math.round(h / 1.8), Math.round((w - 20) * 9 / 16));
            frames[j].style.width = (w - 20) + "px";
            frames[j].style.height = Math.max(Math.round(h / 1.8), Math.round((w - 20) * 9 / 16)) + "px";
            if (frames[j].src.indexOf("imgur") != -1)
                frames[j].src += "?w=" + (w - 20);
        }
    }
    var postId = @(Model.PostId ?? -1);
    if (postId != -1) {
        element = document.getElementById(postId);
        elementRect = element.getBoundingClientRect();
        absoluteElementTop = elementRect.top + window.pageYOffset;
        middle = absoluteElementTop - (window.innerHeight / 2);
        if (element.parentNode) {
            element.parentNode.style.borderWidth = "5px"
            element.parentNode.style.borderColor = "#33beff";
        }
        window.scrollTo(0, middle);
    }
}