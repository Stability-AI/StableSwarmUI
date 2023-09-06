

/** Triggers and process the clip tokenization utility. */
function utilClipTokenize() {
    let elem = getRequiredElementById('clip_tokenization_test_textarea');
    let resultLine = getRequiredElementById('clip_tokenization_result_line');
    function process() {
        elem.dataset.is_running_proc = true;
        genericRequest('TokenizeInDetail', { text: elem.value }, data => {
            console.log(`server says ${JSON.stringify(data)}`)
            let html = '';
            for (let token of data.tokens) {
                let text = token.text;
                let tweak = '';
                let postText = '';
                let title = '';
                if (text.endsWith('</w>')) {
                    text = text.substring(0, text.length - '</w>'.length);
                    tweak += ' clip-tokenization-word-tweak';
                    postText += '<span class="clip-tokenization-wordbreak">&lt;/w&gt;</span>';
                    title = "This token is the end of a word, meaning a word-break appears after (such as a space or punctuation).";
                }
                else {
                    title = "This token is a word-piece (as opposed to a word-end), meaning there is no word-break after it, it directly connects to the next token.";
                }
                html += `<span class="clip-tokenization-block${tweak}" title="${title}">${escapeHtml(text)}${postText}<br>${token.id}</span>`;
            }
            resultLine.innerHTML = html;
            delete elem.dataset.is_running_proc;
            if (elem.dataset.needs_reprocess) {
                delete elem.dataset.needs_reprocess;
                process();
            }
        });
    }
    if (elem.dataset.is_running_proc) {
        elem.dataset.needs_reprocess = true;
    }
    else {
        process();
    }
}
