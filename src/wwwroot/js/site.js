
let session_id = null;
let user_id = null;
let outputAppendUser = null;

function getImageOutPrefix() {
    return outputAppendUser ? `View/${user_id}` : 'Output';
}

function enableSlidersIn(elem) {
    for (let div of elem.getElementsByClassName('auto-slider-box')) {
        enableSliderForBox(div);
    }
}

function enableSliderForBox(div) {
    let range = div.querySelector('input[type="range"]');
    let number = div.querySelector('input[type="number"]');
    number.addEventListener('input', (event) => {
        let newVal = number.value;
        if (!event.shiftKey) {
            number.dataset.old_value = newVal;
            return;
        }
        let oldVal = parseInt(number.dataset.old_value || number.getAttribute('value'));
        if (newVal > oldVal) {
            number.value = Math.min(parseInt(number.getAttribute('max')), oldVal + 1);
        }
        else if (newVal < oldVal) {
            number.value = Math.max(parseInt(number.getAttribute('min')), oldVal - 1);
        }
        number.dataset.old_value = number.value;
    });
    if (range.dataset.ispot == "true") {
        let max = parseInt(range.getAttribute('max')), min = parseInt(range.getAttribute('min')), step = parseInt(range.getAttribute('step'));
        range.addEventListener('input', () => {
            number.value = linearToPot(range.value, max, min, step);
            range.value = potToLinear(number.value, max, min, step);
            number.dispatchEvent(new Event('change'));
        });
        number.addEventListener('input', () => {
            range.value = potToLinear(number.value, max, min, step);
            range.dispatchEvent(new Event('change'));
        });
        range.step = 1;
    }
    else {
        range.addEventListener('input', () => {
            number.value = range.value;
            number.dispatchEvent(new Event('change'));
        });
        number.addEventListener('input', () => {
            range.value = number.value;
            range.dispatchEvent(new Event('change'));
        });
    }
    number.dispatchEvent(new Event('input'));
}

function showError(message) {
    let container = getRequiredElementById('center_toast');
    let box = getRequiredElementById('error_toast_box');
    getRequiredElementById('error_toast_content').innerText = message;
    if (!box.classList.contains('show')) {
        box.classList.add('show');
        box.classList.remove('hide');
    }
    var new_container = container.cloneNode(true);
    container.parentNode.replaceChild(new_container, container);
}

function genericServerError() {
    showError('Failed to send request to server. Did the server crash?');
}

let failedWSAddr = translatable(`Failed to get WebSocket address. You may be connecting to the server in an unexpected way. Please use "http" or "https" URLs.`);
let failedDepth = translatable(`Failed to get session ID after 3 tries. Your account may have been invalidated. Try refreshing the page, or contact the site owner.`);

function makeWSRequest(url, in_data, callback, depth = 0, errorHandle = null) {
    function fail(e) {
        if (errorHandle) {
            errorHandle(e);
            return;
        }
        console.log(e);
        showError(e);
    }
    let ws_address = getWSAddress();
    if (ws_address == null) {
        console.log(`Tried making WS request ${url} but failed.`);
        fail(failedWSAddr);
        return;
    }
    let socket = new WebSocket(`${ws_address}/API/${url}`);
    socket.onopen = () => {
        in_data['session_id'] = session_id;
        socket.send(JSON.stringify(in_data));
    };
    socket.onmessage = (event) => {
        let data = JSON.parse(event.data);
        if (data.error_id && data.error_id == 'invalid_session_id') {
            if (depth > 3) {
                fail(failedDepth.get());
                return;
            }
            console.log('Session refused, will get new one and try again.');
            getSession(() => {
                makeWSRequest(url, in_data, callback, depth + 1);
            });
            return;
        }
        if (data.error) {
            let error = typeof data.error == 'string' ? data.error : JSON.stringify(data.error);
            console.log(`Tried making WS request ${url} but failed with error: ${error}`);
            fail(error);
            return;
        }
        callback(data);
    }
    socket.onerror = errorHandle || genericServerError;
}

let failedCrash = translatable(`Failed to send request to server. Did the server crash?`);

function genericRequest(url, in_data, callback, depth = 0, errorHandle = null) {
    in_data['session_id'] = session_id;
    function fail(e) {
        if (errorHandle) {
            errorHandle(e);
            return;
        }
        console.log(e);
        showError(e);
    }
    sendJsonToServer(`/API/${url}`, in_data, (status, data) => {
        if (!data) {
            console.log(`Tried making generic request ${url} but failed.`);
            fail(failedCrash.get());
            return;
        }
        if (data.error_id && data.error_id == 'invalid_session_id') {
            if (depth > 3) {
                fail(failedDepth.get());
                return;
            }
            console.log('Session refused, will get new one and try again.');
            getSession(() => {
                genericRequest(url, in_data, callback, depth + 1);
            });
            return;
        }
        if (data.error) {
            console.log(`Tried making generic request ${url} but failed with error: ${data.error}`);
            console.log(`Input was ${JSON.stringify(in_data)}`);
            fail(data.error.get());
            return;
        }
        callback(data);
    }, errorHandle || genericServerError);
}

let lastServerVersion = null;
let versionIsWrong = false;

let serverHasUpdated = translatable(`The server has updated since you opened the page, please refresh.`);

function getSession(callback) {
    genericRequest('GetNewSession', {}, data => {
        console.log("Session started.");
        session_id = data.session_id;
        user_id = data.user_id;
        outputAppendUser = data.output_append_user;
        if (lastServerVersion == null) {
            lastServerVersion = data.version;
        }
        else if (lastServerVersion != data.version) {
            if (!versionIsWrong) {
                versionIsWrong = true;
                showError(serverHasUpdated.get());
            }
            if (typeof reviseStatusBar != 'undefined') {
                reviseStatusBar();
            }
        }
        if (callback) {
            callback();
        }
    });
}

function triggerChangeFor(elem) {
    elem.dispatchEvent(new Event('input'));
    if (elem.oninput) {
        elem.oninput();
    }
    elem.dispatchEvent(new Event('change'));
    if (elem.onchange) {
        elem.onchange();
    }
}

function textPromptDoCount(elem) {
    let tokenCount = elem.parentElement.querySelector('.auto-input-prompt-tokencount');
    function countTokens() {
        elem.dataset.has_token_count_running = true;
        genericRequest('CountTokens', { text: elem.value }, data => {
            let chunks = Math.max(75, Math.ceil(data.count / 75) * 75);
            tokenCount.innerText = `${data.count}/${chunks}`;
            delete elem.dataset.has_token_count_running;
            if (elem.dataset.needs_token_recount) {
                delete elem.dataset.needs_token_recount;
                countTokens();
            }
        });
    }
    if (elem.dataset.has_token_count_running) {
        elem.dataset.needs_token_recount = true;
    }
    else {
        countTokens();
    }
}

function textPromptInputHandle(elem) {
    elem.style.height = '0px';
    elem.style.height = `max(3.4rem, ${elem.scrollHeight + 5}px)`;
    textPromptDoCount(elem);
}

function textPromptAddKeydownHandler(elem) {
    let shiftText = (up) => {
        let selStart = elem.selectionStart;
        let selEnd = elem.selectionEnd;
        let before = elem.value.substring(0, selStart);
        let after = elem.value.substring(selEnd);
        let mid = elem.value.substring(selStart, selEnd);
        let strength = 1;
        while (mid.startsWith(" ")) {
            mid = mid.substring(1);
            before = before + " ";
        }
        while (mid.endsWith(" ")) {
            mid = mid.substring(0, mid.length - 1);
            after = " " + after;
        }
        if (mid.startsWith("(")) {
            before += mid.substring(0, 1);
            mid = mid.substring(1);
        }
        // Sorry for the regex. Matches ends with ":1.5)" or just ")". Or Just ":1.5". Also empty, so that needs a check after.
        let matched = mid.trim().match(/(?:\:[0-9.-]*)?\)?$/);
        if (matched && matched[0]) {
            after = mid.substring(mid.length - matched[0].length) + after;
            mid = mid.substring(0, mid.length - matched[0].length);
        }
        if (before.trimEnd().endsWith("(") && after.trimStart().startsWith(":")) {
            let postColon = after.trimStart().substring(1);
            let paren = postColon.indexOf(')');
            if (paren != -1) {
                before = before.trimEnd();
                before = before.substring(0, before.length - 1);
                strength = parseFloat(postColon.substring(0, paren).trim());
                after = postColon.substring(paren + 1);
            }
        }
        else if (before.trimEnd().endsWith("(") && after.trimStart().startsWith(")")) {
            before = before.trimEnd();
            before = before.substring(0, before.length - 1);
            strength = 1.1;
            after = after.trimStart().substring(1);
        }
        strength += up ? 0.1 : -0.1;
        strength = `${formatNumberClean(strength, 5)}`;
        if (strength == "1") {
            elem.value = `${before}${mid}${after}`;
            elem.selectionStart = before.length;
            elem.selectionEnd = before.length + mid.length;
        }
        else {
            elem.value = `${before}(${mid}:${strength})${after}`;
            elem.selectionStart = before.length + 1;
            elem.selectionEnd = before.length + mid.length + 1;
        }
        triggerChangeFor(elem);
    }
    elem.addEventListener('keydown', (e) => {
        if (e.ctrlKey && (e.key == 'ArrowUp' || e.key == 'ArrowDown')) {
            shiftText(e.key == 'ArrowUp');
            e.preventDefault();
            e.stopPropagation();
            return false;
        }
    });
    if (typeof promptTabComplete != 'undefined') {
        promptTabComplete.enableFor(elem);
    }
}

function setSeedToRandom(elemId) {
    let elem = getRequiredElementById(elemId);
    elem.value = -1;
    triggerChangeFor(elem);
}

function doToggleEnable(id) {
    let elem = document.getElementById(id);
    if (!elem) {
        console.log(`Tried to toggle ${id} but it doesn't exist.`);
        return;
    }
    let toggler = document.getElementById(id + '_toggle');
    if (!toggler) {
        console.log(`Tried to toggle ${id} but the toggler doesn't exist.`);
        return;
    }
    let elem2 = document.getElementById(id + '_rangeslider');
    if (!toggler.checked) {
        if (elem.classList.contains('disabled-input')) {
            return;
        }
        elem.classList.add('disabled-input');
        if (elem2) {
            elem2.classList.add('disabled-input');
        }
        if (!elem.dataset.has_toggle_handler) {
            function autoActivate() {
                toggler.checked = true;
                doToggleEnable(id);
            };
            elem.addEventListener('focus', autoActivate);
            elem.addEventListener('change', autoActivate);
            if (elem2) {
                elem2.addEventListener('focus', autoActivate);
                elem2.addEventListener('change', autoActivate);
            }
            elem.dataset.has_toggle_handler = true;
        }
    }
    else {
        if (!elem.classList.contains('disabled-input')) {
            return;
        }
        elem.classList.remove('disabled-input');
        if (elem2) {
            elem2.classList.remove('disabled-input');
        }
    }
}

function getToggleHtml(toggles, id, name, extraClass = '', func = 'doToggleEnable') {
    return toggles ? `<span class="form-check form-switch display-inline-block${extraClass}"><input class="auto-slider-toggle form-check-input" type="checkbox" id="${id}_toggle" title="Enable/disable ${name}" onclick="${func}('${id}')" onchange="${func}('${id}')" autocomplete="false"></span>` : '';
}

function load_image_file(e) {
    let file = e.files[0];
    let preview = e.parentElement.querySelector('.auto-input-image-preview');
    if (file) {
        let reader = new FileReader();
        reader.addEventListener("load", () => {
            e.dataset.filedata = reader.result;
            preview.innerHTML = `<button class="interrupt-button auto-input-image-remove-button" title="Remove image">&times;</button><img src="${reader.result}" alt="Image preview" />`;
            preview.firstChild.addEventListener('click', () => {
                delete e.dataset.filedata;
                preview.innerHTML = '';
                e.value = '';
            });
        }, false);
        reader.readAsDataURL(file);
    }
    else {
        e.dataset.filedata = null;
        preview.innerHTML = '';
    }
}

function autoSelectWidth(elem) {
    let span = document.createElement('span');
    span.innerText = elem.value;
    document.body.appendChild(span);
    let width = Math.max(50, span.offsetWidth + 30);
    elem.style.width = `${width}px`;
    span.remove();
}

function makeGenericPopover(id, name, type, description, example) {
    return `<div class="sui-popover" id="popover_${id}"><b>${escapeHtmlNoBr(name)}</b> (${type}):<br>&emsp;${escapeHtmlNoBr(description)}${example}</div>`;
}

function doPopoverHover(id) {
    let input = getRequiredElementById(id);
    let parent = findParentOfClass(input, 'auto-input');
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible != "true") {
        let targetX = parent.getBoundingClientRect().right;
        let targetY = parent.getBoundingClientRect().top;
        pop.classList.add('sui-popover-visible');
        pop.style.width = '200px';
        pop.dataset.visible = "true";
        let x = Math.min(targetX, window.innerWidth - pop.offsetWidth - 10);
        let y = Math.min(targetY, window.innerHeight - pop.offsetHeight);
        pop.style.left = `${x}px`;
        pop.style.top = `${y}px`;
        pop.style.width = '';
    }
}

function hidePopoverHover(id) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        pop.classList.remove('sui-popover-visible');
        pop.dataset.visible = "false";
    }
}

function getPopoverElemsFor(id, popover_button) {
    if (!popover_button) {
        return ['', ''];
    }
    let settingElem = document.getElementById('usersettings_hintformat');
    let format = 'BUTTON';
    if (settingElem) {
        format = settingElem.value;
    }
    if (format == 'BUTTON') {
        return [`<span class="auto-input-qbutton info-popover-button" onclick="doPopover('${id}')">?</span>`, ''];
    }
    else if (format == 'HOVER') {
        return ['', ` onmouseover="doPopoverHover('${id}')" onmouseout="hidePopoverHover('${id}')"`];
    }
    return ['', ''];
}

function makeSliderInput(featureid, id, name, description, value, min, max, view_max = 0, step = 1, isPot = false, toggles = false, popover_button = true) {
    name = escapeHtml(name);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let rangeVal = isPot ? potToLinear(value, max, min, step) : value;
    let [popover, featureid2] = getPopoverElemsFor(id, popover_button);
    featureid += featureid2;
    return `
    <div class="slider-auto-container">
    <div class="auto-input auto-slider-box"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
        <input class="auto-slider-number" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-ispot="${isPot}" autocomplete="false">
        <br>
        <input class="auto-slider-range" type="range" id="${id}_rangeslider" value="${rangeVal}" min="${min}" max="${view_max}" step="${step}" data-ispot="${isPot}" autocomplete="false">
    </div></div>`;
}

function makeNumberInput(featureid, id, name, description, value, min, max, step = 1, format = 'big', toggles = false, popover_button = true) {
    name = escapeHtml(name);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let [popover, featureid2] = getPopoverElemsFor(id, popover_button);
    featureid += featureid2;
    if (format == 'seed') {
        return `
            <div class="auto-input auto-number-box auto-input-flex"${featureid}>
                <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
                <input class="auto-number auto-number-seedbox" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-name="${name}" autocomplete="false">
                <button class="basic-button" title="Random (Set to -1)" onclick="setSeedToRandom('${id}')">&#x1F3B2;</button>
                <button class="basic-button" title="Reuse (from currently selected image)" onclick="reuseLastParamVal('${id}');">&#128257;</button>
            </div>`;
    }
    return `
        <div class="auto-input auto-number-box auto-input-flex"${featureid}>
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
            <input class="auto-number" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-name="${name}" autocomplete="false">
        </div>`;
}

function makeTextInput(featureid, id, name, description, value, format, placeholder, toggles = false, genPopover = false, popover_button = true) {
    name = escapeHtml(name);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let onInp = format == "prompt" ? ' oninput="textPromptInputHandle(this)"' : '';
    let tokenCounter = format == "prompt" ? '<span class="auto-input-prompt-tokencount" title="Text-Encoder token count / chunk-size">0/75</span>' : '';
    let [popover, featureid2] = getPopoverElemsFor(id, popover_button);
    featureid += featureid2;
    let isBig = format == "prompt" || format == "big";
    return `
    ${genPopover ? makeGenericPopover(id, name, 'Boolean', description, '') : ''}
    <div class="auto-input auto-text-box${(isBig ? "" : " auto-input-flex")}"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
        ${tokenCounter}
        <textarea class="auto-text${(isBig ? " auto-text-block" : "")} translate translate-no-text" id="${id}" rows="${isBig ? 2 : 1}"${onInp} placeholder="${escapeHtmlNoBr(placeholder)}" data-name="${name}" autocomplete="false">${escapeHtml(value)}</textarea>
        <button class="interrupt-button image-clear-button" style="display: none;">${translateableHtml("Clear Images")}</button>
        <div class="added-image-area"></div>
    </div>`;
}

function makeCheckboxInput(featureid, id, name, description, value, toggles = false, genPopover = false, popover_button = true) {
    name = escapeHtml(name);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let checked = `${value}` == "true" ? ' checked="true"' : '';
    let [popover, featureid2] = getPopoverElemsFor(id, popover_button);
    featureid += featureid2;
    return `
    ${genPopover ? makeGenericPopover(id, name, 'Boolean', description, '') : ''}
    <div class="auto-input auto-checkbox-box auto-input-flex"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
        <input class="auto-checkbox" type="checkbox" data-name="${name}" id="${id}"${checked}>
    </div>`;
}

function makeDropdownInput(featureid, id, name, description, values, defaultVal, toggles = false, popover_button = true) {
    name = escapeHtml(name);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let [popover, featureid2] = getPopoverElemsFor(id, popover_button);
    featureid += featureid2;
    let html = `
    <div class="auto-input auto-dropdown-box auto-input-flex"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
        <select class="auto-dropdown" id="${id}" autocomplete="false" onchange="autoSelectWidth(this)">`;
    for (let value of values) {
        let selected = value == defaultVal ? ' selected="true"' : '';
        html += `<option value="${escapeHtmlNoBr(value)}"${selected}>${escapeHtml(value)}</option>`;
    }
    html += `
        </select>
    </div>`;
    return html;
}

function makeMultiselectInput(featureid, id, name, description, values, defaultVal, placeholder, toggles = false, popover_button = true) {
    name = escapeHtml(name);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let [popover, featureid2] = getPopoverElemsFor(id, popover_button);
    featureid += featureid2;
    let html = `
    <div class="auto-input auto-dropdown-box"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
        <select class="form-select" id="${id}" autocomplete="false" data-placeholder="${escapeHtmlNoBr(placeholder)}" multiple>`;
    for (let value of values) {
        let selected = value == defaultVal ? ' selected="true"' : '';
        html += `<option value="${escapeHtmlNoBr(value)}"${selected}>${escapeHtml(value)}</option>`;
    }
    html += `
        </select>
    </div>`;
    return html;
}

function makeImageInput(featureid, id, name, description, toggles = false, popover_button = true) {
    name = escapeHtml(name);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let [popover, featureid2] = getPopoverElemsFor(id, popover_button);
    featureid += featureid2;
    let html = `
    <div class="auto-input auto-file-box"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${translateableHtml(name)}${popover}</span>
        <input class="auto-file" type="file" accept="image/png, image/jpeg" id="${id}" onchange="load_image_file(this)" autocomplete="false">
        <div class="auto-input-image-preview"></div>
    </div>`;
    return html;
}

function describeAspectRatio(width, height) {
    let wh = width / height;
    let hw = height / width;
    if (roundTo(wh, 0.01) == 1) {
        return '1:1';
    }
    else if (roundTo(wh, 0.01) % 1 == 0) {
        return `${Math.round(wh)}:1`;
    }
    else if (roundTo(hw, 0.01) % 1 == 0) {
        return `1:${Math.round(hw)}`;
    }
    for (let i = 2; i < 50; i++) {
        if (roundTo(wh * i, 0.01) % 1 == 0) {
            return `${Math.round(wh * i)}:${i}`;
        }
        if (roundTo(hw * i, 0.01) % 1 == 0) {
            return `${i}:${Math.round(hw * i)}`;
        }
    }
    if (wh > 1) {
        return `${roundToStr(wh, 2)}:1`;
    }
    return `1:${roundToStr(hw, 2)}`;
}

function quickAppendButton(div, name, func, classes = '', title = '') {
    let button = document.createElement('button');
    button.className = `basic-button${classes}`;
    button.innerText = name;
    button.title = title;
    button.onclick = (e) => func(e, button);
    div.appendChild(button);
}

function modalHeader(id, title) {
    return `
    <div class="modal" tabindex="-1" role="dialog" id="${id}">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header"><h5 class="modal-title translate">${title}</h5></div>`;
}

function modalFooter() {
    return `</div></div></div>`;
}
