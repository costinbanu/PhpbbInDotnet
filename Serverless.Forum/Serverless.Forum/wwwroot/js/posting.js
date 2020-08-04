class Posting {
    constructor(bbtags, bbHelpLines, isAdmin, isEditAction, userDateFormat) {
        this.form_name = 'postform';
        this.text_name = 'message';

        // Define the bbcode tags
        this.bbcode = new Array();
        this.bbtags = bbtags;

        // Helpline messages
        this.help_line = bbHelpLines;

        // Startup variables
        this.theSelection = false;

        // Check for Browser & Platform for PC & IE specific bits
        // More details from: http://www.mozilla.org/docs/web-developer/sniffer/browser_type.html
        var clientPC = navigator.userAgent.toLowerCase(); // Get client info

        this.clientVer = parseInt(navigator.appVersion); // Get browser version
        this.is_ie = ((clientPC.indexOf('msie') != -1) && (clientPC.indexOf('opera') == -1));
        this.is_win = ((clientPC.indexOf('win') != -1) || (clientPC.indexOf('16bit') != -1));

        this.baseHeight = 0;

        this.hasConfirmation = false;
        this.isAdmin = isAdmin;
        this.isEditAction = isEditAction;

        this.userDateFormat = userDateFormat;

        this.showPollText = 'Arată opțiuni chestionar';
        this.hidePollText = 'Ascunde opțiuni chestionar';
        this.showAttachText = 'Arată opțiuni fișiere atașate';
        this.hideAttachText = 'Ascunde opțiuni fișiere atașate';

    }
    /**
    * Shows the help messages in the helpline window
    */
    helpline(help) {
        document.forms[this.form_name].helpbox.value = this.help_line[help];
    }

    /**
    * Fix a bug involving the TextRange object. From
    * http://www.frostjedi.com/terra/scripts/demo/caretBug.html
    */
    initInsertions() {
        var doc;

        if (document.forms[this.form_name]) {
            doc = document;
        }
        else {
            doc = opener.document;
        }

        var textarea = doc.forms[this.form_name].elements[this.text_name];

        if (this.is_ie && typeof (this.baseHeight) != 'number') {
            textarea.focus();
            this.baseHeight = doc.selection.createRange().duplicate().boundingHeight;

            if (!document.forms[this.form_name]) {
                document.body.focus();
            }
        }
    }

    /**
    * bbstyle
    */
    bbstyle(bbnumber) {
        if (bbnumber != -1) {
            this.bbfontstyle(this.bbtags[bbnumber], this.bbtags[bbnumber + 1]);
        }
        else {
            this.insert_text('[*]');
            document.forms[this.form_name].elements[this.text_name].focus();
        }
    }

    /**
    * Apply this.bbcodes
    */
    bbfontstyle(bbopen, bbclose) {
        this.theSelection = false;

        var textarea = document.forms[this.form_name].elements[this.text_name];

        textarea.focus();

        if ((this.clientVer >= 4) && this.is_ie && this.is_win) {
            // Get text selection
            this.theSelection = document.selection.createRange().text;

            if (this.theSelection) {
                // Add tags around selection
                document.selection.createRange().text = bbopen + this.theSelection + bbclose;
                document.forms[this.form_name].elements[this.text_name].focus();
                this.theSelection = '';
                return;
            }
        }
        else if (document.forms[this.form_name].elements[this.text_name].selectionEnd && (document.forms[this.form_name].elements[this.text_name].selectionEnd - document.forms[this.form_name].elements[this.text_name].selectionStart > 0)) {
            this.mozWrap(document.forms[this.form_name].elements[this.text_name], bbopen, bbclose);
            document.forms[this.form_name].elements[this.text_name].focus();
            this.theSelection = '';
            return;
        }

        //The new position for the cursor after adding the this.bbcode
        var caret_pos = this.getCaretPosition(textarea).start;
        var new_pos = caret_pos + bbopen.length;

        // Open tag
        this.insert_text(bbopen + bbclose);

        // Center the cursor when we don't have a selection
        // Gecko and proper browsers
        if (!isNaN(textarea.selectionStart)) {
            textarea.selectionStart = new_pos;
            textarea.selectionEnd = new_pos;
        }
        // IE
        else if (document.selection) {
            var range = textarea.createTextRange();
            range.move("character", new_pos);
            range.select();
            this.storeCaret(textarea);
        }

        textarea.focus();
        return;
    }

    /**
    * Insert text at position
    */
    insert_text(text, spaces, popup) {
        var textarea;

        if (!popup) {
            textarea = document.forms[this.form_name].elements[this.text_name];
        }
        else {
            textarea = opener.document.forms[this.form_name].elements[this.text_name];
        }
        if (spaces) {
            text = ' ' + text + ' ';
        }

        if (!isNaN(textarea.selectionStart)) {
            var sel_start = textarea.selectionStart;
            var sel_end = textarea.selectionEnd;

            this.mozWrap(textarea, text, '')
            textarea.selectionStart = sel_start + text.length;
            textarea.selectionEnd = sel_end + text.length;
        }
        else if (textarea.createTextRange && textarea.caretPos) {
            if (this.baseHeight != textarea.caretPos.boundingHeight) {
                textarea.focus();
                this.storeCaret(textarea);
            }

            var caret_pos = textarea.caretPos;
            caret_pos.text = caret_pos.text.charAt(caret_pos.text.length - 1) == ' ' ? caret_pos.text + text + ' ' : caret_pos.text + text;
        }
        else {
            textarea.value = textarea.value + text;
        }
        if (!popup) {
            textarea.focus();
        }
    }

    /**
    * Add inline attachment at position
    */
    attach_inline(index, filename) {
        this.insert_text('[attachment=' + index + ']' + filename + '[/attachment]');
        document.forms[this.form_name].elements[this.text_name].focus();
    }

    /**
    * Get the caret position in an textarea
    */
    getCaretPosition(txtarea) {
        var caretPos = new CaretPosition();

        // simple Gecko/Opera way
        if (txtarea.selectionStart || txtarea.selectionStart == 0) {
            caretPos.start = txtarea.selectionStart;
            caretPos.end = txtarea.selectionEnd;
        }
        // dirty and slow IE way
        else if (document.selection) {

            // get current selection
            var range = document.selection.createRange();

            // a new selection of the whole textarea
            var range_all = document.body.createTextRange();
            range_all.moveToElementText(txtarea);

            // calculate selection start point by moving beginning of range_all to beginning of range
            var sel_start;
            for (sel_start = 0; range_all.compareEndPoints('StartToStart', range) < 0; sel_start++) {
                range_all.moveStart('character', 1);
            }

            txtarea.sel_start = sel_start;

            // we ignore the end value for IE, this is already dirty enough and we don't need it
            caretPos.start = txtarea.sel_start;
            caretPos.end = txtarea.sel_start;
        }

        return caretPos;
    }

    /**
    * Add quote text to message
    */
    //addquote(post_id, username) {
    //    var message_name = 'message_' + post_id;
    //    this.theSelection = '';
    //    var divarea = false;

    //    if (document.all) {
    //        divarea = document.all[message_name];
    //    }
    //    else {
    //        divarea = document.getElementById(message_name);
    //    }

    //    // Get text selection - not only the post content :(
    //    if (window.getSelection) {
    //        this.theSelection = window.getSelection().toString();
    //    }
    //    else if (document.getSelection) {
    //        this.theSelection = document.getSelection();
    //    }
    //    else if (document.selection) {
    //        this.theSelection = document.selection.createRange().text;
    //    }

    //    if (this.theSelection == '' || typeof this.theSelection == 'undefined' || this.theSelection == null) {
    //        if (divarea.innerHTML) {
    //            this.theSelection = divarea.innerHTML.replace(/<br>/ig, '\n');
    //            this.theSelection = this.theSelection.replace(/<br\/>/ig, '\n');
    //            this.theSelection = this.theSelection.replace(/&lt\;/ig, '<');
    //            this.theSelection = this.theSelection.replace(/&gt\;/ig, '>');
    //            this.theSelection = this.theSelection.replace(/&amp\;/ig, '&');
    //            this.theSelection = this.theSelection.replace(/&nbsp\;/ig, ' ');
    //        }
    //        else if (document.all) {
    //            this.theSelection = divarea.innerText;
    //        }
    //        else if (divarea.textContent) {
    //            this.theSelection = divarea.textContent;
    //        }
    //        else if (divarea.firstChild.nodeValue) {
    //            this.theSelection = divarea.firstChild.nodeValue;
    //        }
    //    }

    //    if (this.theSelection) {
    //        this.insert_text('[quote="' + username + '"]' + this.theSelection + '[/quote]');
    //    }

    //    return;
    //}

    /**
    * From http://www.massless.org/mozedit/
    */
    mozWrap(txtarea, open, close) {
        var selLength = txtarea.textLength;
        var selStart = txtarea.selectionStart;
        var selEnd = txtarea.selectionEnd;
        var scrollTop = txtarea.scrollTop;

        if (selEnd == 1 || selEnd == 2) {
            selEnd = selLength;
        }

        var s1 = (txtarea.value).substring(0, selStart);
        var s2 = (txtarea.value).substring(selStart, selEnd)
        var s3 = (txtarea.value).substring(selEnd, selLength);

        txtarea.value = s1 + open + s2 + close + s3;
        txtarea.selectionStart = selEnd + open.length + close.length;
        txtarea.selectionEnd = txtarea.selectionStart;
        txtarea.focus();
        txtarea.scrollTop = scrollTop;

        return;
    }

    /**
    * Insert at Caret position. Code from
    * http://www.faqts.com/knowledge_base/view.phtml/aid/1052/fid/130
    */
    storeCaret(textEl) {
        if (textEl.createTextRange) {
            textEl.caretPos = document.selection.createRange().duplicate();
        }
    }

    change_palette() {
        showElement(
            'colour_palette',
            function () {
                $('#bbpalette').val('Culoare font');
            },
            function () {
                $('#bbpalette').val('Ascunde culoarea fontului');
            }
        );
    }

    show_hidden_formatters() {
        showElement(
            'controls',
            function () {
                $('.PostingControlsButton').text('Opțiuni formatare');
            },
            function () {
                $('.PostingControlsButton').text('Ascunde opțiunile de formatare');
            }
        );
    }

    show_hidden_smilies() {
        showElement(
            "hiddenSmilies",
            function () {
                $("#showHiddenSmiliesButton").text("Arată mai multe");
                //$('#mainContainer').height($('#mainContainer').height() - $('#hiddenSmilies').height());
            },
            function () {
                $('#hiddenSmilies').find('img').each((index, element) => {
                    resizeImage(element, $('#hiddenSmilies').parent()[0].offsetWidth, $('#hiddenSmilies').parent()[0].offsetHeight);
                });
                //$('#mainContainer').height($('#mainContainer').height() + $('#hiddenSmilies').height());
                $("#showHiddenSmiliesButton").text("Arată mai puține");
            },
            false
        )
    }

    censor() {
        var textinput = document.getElementById("message");
        if (textinput) {
            textinput.value = textinput.value.replace(/postimage/gi, "*****");
            textinput.value = textinput.value.replace(/postimg/gi, "*****");
            textinput.value = textinput.value.replace(/photobucket/gi, "*****");
            textinput.value = textinput.value.replace(/imageshack/gi, "*****");
            var imgNo = textinput.value.split("[img]").length - 1;
            if (imgNo > 10 && !this.isAdmin) {
                alert("Ati inclus " + imgNo + " imagini in mesaj. Numarul maxim permis: 10.")
                return false;
            }
            else {
                var imgs = textinput.value.match(/\[img\](.*?)\[\/img\]/g).map((val) => {
                    return val.replace(/\[\/?img\]/g, '');
                });
                document.getElementById('imgcheckstatus').style.visibility = 'visible';
                var isok = true;
                var badimgs = [];
                for (var i = 0; i < imgs.length; i++) {
                    if (imgs[i].indexOf("metrouusor") != -1) {
                        continue;
                    }
                    document.getElementById('imgcheckstatus').innerHTML = 'Sunt verificate imaginile incluse in mesaj.<br/>Va rugam asteptati...<br/>' + Math.round((i * 100) / imgs.length) + '%';
                    var xhr = $.ajax({
                        type: "GET",
                        async: false,
                        url: imgs[i],
                        success: (msg) => {
                            if (msg.length / 1024 / 1024 > 2 && !this.isAdmin) {
                                isok = false;
                                badimgs.push(imgs[i]);
                            }
                        },
                        error: (jqXHR, textStatus, errorThrown) => {
                            console.log(textStatus, errorThrown);
                        }
                    });
                }
                if (!isok && !this.isAdmin) {
                    document.getElementById('imgcheckstatus').style.visibility = 'hidden';
                    alert('Nu este permisa includerea unor imagini avand peste 2MB dimensiune. Urmatoarele imagini sunt mai mari:\n' + badimgs.join('\n'));
                    return false;
                }
                else {
                    document.getElementById('imgcheckstatus').style.visibility = 'hidden';
                    return true;
                }
            }
        }
        else {
            return true;
        }
    }

    showExpirationDate(value) {
        var target = document.getElementById("pollExpirationHelper");
        if (value == 0) {
            target.innerText = "Chestionarul nu va expira niciodată";
            return;
        }

        var t = new Date();
        t.setSeconds(t.getSeconds() + value * 86400);
        target.innerText = "Chestionarul expiră " + t.format(this.userDateFormat);
    }

    confirmPollChange() {
        if (this.isEditAction && !this.hasConfirmation && confirm("Dacă modificați opțiunile, toate voturile înregistrate până acum vor fi șterse. Continuați?")) {
            this.hasConfirmation = true;
        }
    }

    togglePanel(panelId, buttonId, textWhenVisible, textWhenHidden) {
        showElement(
            panelId,
            function () {
                $('#' + buttonId).text(textWhenHidden);
            },
            function () {
                $('#' + buttonId).text(textWhenVisible)
            }
        );
    }

    toggleAttach() {
        if ($('#pollPanel').is(':visible')) {
            this.togglePanel('pollPanel', 'pollButton', this.hidePollText, this.showPollText);
        }
        this.togglePanel('attachPanel', 'attachButton', this.hideAttachText, this.showAttachText);
    }

    togglePoll() {
        if ($('#attachPanel').is(':visible')) {
            posting.togglePanel('attachPanel', 'attachButton', this.hideAttachText, this.showAttachText);
        }
        this.togglePanel('pollPanel', 'pollButton', this.hidePollText, this.showPollText);
    }

    submitAttachments() {
        $('#submitAttachmentsButton').trigger('click');
        $('#fileUploadStatus').text('Se încarcă fișierele...');
    }
}

/**
* Caret Position object
*/
class CaretPosition {
    constructor() {
        this.start = null;
        this.end = null;
    }
}