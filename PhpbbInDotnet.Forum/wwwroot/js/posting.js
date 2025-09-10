class Posting {

    attachmentOrder;

    constructor(bbtags, imgSizeLimit, imgCountLimit, isMod, isEditAction, userDateFormat, baseUrl, attachmentIds) {
        this.form_name = 'postform';
        this.text_name = 'message';

        // Define the bbcode tags
        this.bbcode = new Array();
        this.bbtags = bbtags;

        // Startup variables
        this.theSelection = false;

        this.baseHeight = 0;

        this.hasConfirmation = false;
        this.isMod = isMod;
        this.isEditAction = isEditAction;

        this.userDateFormat = userDateFormat;

        this.imgSizeLimit = imgSizeLimit;
        this.imgCountLimit = imgCountLimit;

        this.baseUrl = baseUrl;

        this.attachmentOrder = {};
        if (Array.isArray(attachmentIds)) {
            attachmentIds.forEach((elem, index) => {
                this.attachmentOrder[elem] = index;
            });
        }
        console.log(this.attachmentOrder);
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
    }

    /**
    * bbstyle
    */
    bbstyle(code) {
        this.bbfontstyle(this.bbtags[code].openTag, this.bbtags[code].closeTag);
    }

    /**
    * Apply this.bbcodes
    */
    bbfontstyle(bbopen, bbclose) {
        this.theSelection = false;

        var textarea = document.forms[this.form_name].elements[this.text_name];

        textarea.focus();

        if (document.forms[this.form_name].elements[this.text_name].selectionEnd && (document.forms[this.form_name].elements[this.text_name].selectionEnd - document.forms[this.form_name].elements[this.text_name].selectionStart > 0)) {
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
    attach_inline(id, filename) {
        console.log(this.attachmentOrder);
        let index = this.attachmentOrder[id];
        this.insert_text('[attachment=' + index + ']' + filename + '[/attachment]', false, false);
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
    addquote(text, username, postId) {
        this.theSelection = '';

        // Get text selection - not only the post content :(
        if (window.getSelection) {
            this.theSelection = window.getSelection().toString();
        }
        else if (document.getSelection) {
            this.theSelection = document.getSelection();
        }
        else if (document.selection) {
            this.theSelection = document.selection.createRange().text;
        }

        if (this.theSelection == '' || typeof this.theSelection == 'undefined' || this.theSelection == null) {
            this.theSelection = text;
        }

        if (this.theSelection) {
            this.insert_text(`[quote="${username}",${postId}]\n${this.theSelection}\n[/quote]\n`);
        }

        return;
    }

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
                $('#bbpalette').val(dictionary.Posting['FONT_COLOR']);
            },
            function () {
                $('#bbpalette').val(dictionary.Posting['HIDE_FONT_COLOR']);
            }
        );
    }

    show_hidden_formatters() {
        showElement(
            'controls',
            function () {
                $('.PostingControlsButton').text(dictionary.Posting['FORMATTING_OPTIONS']);
            },
            function () {
                $('.PostingControlsButton').text(dictionary.Posting['HIDE_FORMATTING_OPTIONS']);
            }
        );
    }

    censor() {
        var textinput = document.getElementById("message");
        if (textinput) {
            textinput.value = textinput.value.replace(/postimage/gi, "*****");
            textinput.value = textinput.value.replace(/postimg/gi, "*****");
            textinput.value = textinput.value.replace(/photobucket/gi, "*****");
            textinput.value = textinput.value.replace(/imageshack/gi, "*****");
            var imgNo = textinput.value.split("[img]").length - 1;
            if (imgNo > this.imgCountLimit && !this.isMod) {
                alert(formatString(dictionary.Posting['TOO_MANY_IMAGES_ERROR_FORMAT'], imgNo, this.imgCountLimit));
                return false;
            }
            else {
                var matches = textinput.value.match(/\[img\](.*?)\[\/img\]/g);
                if (!matches) {
                    return true;
                }
                var imgs = matches.map((val) => {
                    return val.replace(/\[\/?img\]/g, '');
                });
                $('#imgcheckstatus').toggle();
                var isok = true;
                var badimgs = [];
                for (var i = 0; i < imgs.length; i++) {
                    $('#imgcheckstatus').html(formatString(dictionary.Posting['CHECKING_IMAGES_FORMAT'], Math.round((i * 100) / imgs.length)));
                    if (imgs[i].indexOf(this.baseUrl) != -1) {
                        continue;
                    }
                    var response = $.ajax({
                        type: "GET",
                        async: false,
                        url: imgs[i],
                        success: (msg) => {
                            if (msg.length / 1024 / 1024 > this.imgSizeLimit && !this.isMod) {
                                isok = false;
                                badimgs.push(imgs[i]);
                            }
                        },
                        error: (_, textStatus, errorThrown) => {
                            console.log(textStatus, errorThrown);
                        }
                    });
                }
                if (!isok && !this.isMod) {
                    $('#imgcheckstatus').toggle();
                    alert(formatString(dictionary.Posting['IMAGES_TOO_BIG_ERROR_FORMAT'], this.imgSizeLimit, badimgs.join('\n')));
                    return false;
                }
                else {
                    $('#imgcheckstatus').toggle();
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
            target.innerText = dictionary.Posting['POLL_NEVER_EXPIRES'];
            return;
        }

        var t = new Date();
        t.setSeconds(t.getSeconds() + value * 86400);
        target.innerText = formatString(dictionary.Posting['POLL_EXPIRES_FORMAT'], t.format(this.userDateFormat));
    }

    confirmPollChange(opts) {
        if (!this.hasConfirmation && !opts.value) {
            this.hasConfirmation = true;
            return true;
        }
        if (this.isEditAction && !this.hasConfirmation && opts.value
            && confirm(dictionary.Posting['CONFIRM_POLL_OPTIONS_CHANGE'])) {
            this.hasConfirmation = true;
            return true;
        }
        return this.hasConfirmation;
    }

    toggleAttach() {
        if ($('#pollPanel').is(':visible')) {
            showElement('pollPanel');
        }
        if ($('#emojiPanel').is(':visible')) {
            showElement('emojiPanel');
        }
        showElement('attachPanel');
    }

    toggleAttachOrdering(button) {
        showElement(
            'attachManagementPanel',
            () => { button.value = 'Administreaza fisiere'; },
            () => { button.value = 'Ordoneaza fisiere'; }
        );
        showElement('attachSortingPanel');
    }

    onSortAttachmentList(_evt) {
        let mgmtContainer = $('#attachManagementPanel');
        let mgmtContainerChildren = mgmtContainer.children();
        console.log(this.attachmentOrder);
        let newAttachmentOrder = {};
        $('#attachmentNames').children('.MySortableItem').each((index, elem) => {
            newAttachmentOrder[$(elem).data('attach-id')] = index;
        });
        $('#AttachmentOrderHasChanged').val('true');
        this.attachmentOrder = newAttachmentOrder;

        console.log(this.attachmentOrder);

        mgmtContainerChildren.detach();

        mgmtContainerChildren.sort((a, b) => {
            let an = this.attachmentOrder[$(a).data('attach-id')];
            let bn = this.attachmentOrder[$(b).data('attach-id')];

            if (an > bn) {
                return 1;
            }
            if (an < bn) {
                return -1;
            }
            return 0;
        });

        mgmtContainerChildren.appendTo(mgmtContainer);
    }

    togglePoll() {
        if ($('#attachPanel').is(':visible')) {
            showElement('attachPanel')
        }
        if ($('#emojiPanel').is(':visible')) {
            showElement('emojiPanel');
        }
        showElement('pollPanel');
    }

    toggleEmoji() {
        if ($('#attachPanel').is(':visible')) {
            showElement('attachPanel')
        }
        if ($('#pollPanel').is(':visible')) {
            showElement('pollPanel');
        }
        showElement('emojiPanel');
    }

    submitAttachments() {
        $('#submitAttachmentsButton').trigger('click');
        $('#fileUploadStatus').text(dictionary.Posting['LOADING_FILES']);
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