
let session_id = null;

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
        if (!shiftMonitor) {
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
        fail('Failed to get WebSocket address. You may be connecting to the server in an unexpected way. Please use "http" or "https" URLs.');
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
                fail('Failed to get session ID after 3 tries. Your account may have been invalidated. Try refreshing the page, or contact the site owner.');
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
            fail('Failed to send request to server. Did the server crash?');
            return;
        }
        if (data.error_id && data.error_id == 'invalid_session_id') {
            if (depth > 3) {
                fail('Failed to get session ID after 3 tries. Your account may have been invalidated. Try refreshing the page, or contact the site owner.');
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
            fail(data.error);
            return;
        }
        callback(data);
    }, errorHandle || genericServerError);
}

let lastServerVersion = null;
let versionIsWrong = false;

function getSession(callback) {
    genericRequest('GetNewSession', {}, data => {
        console.log("Session started.");
        session_id = data.session_id;
        if (lastServerVersion == null) {
            lastServerVersion = data.version;
        }
        else if (lastServerVersion != data.version) {
            versionIsWrong = true;
            showError(`The server has updated since you opened the page, please refresh.`);
            if (typeof reviseStatusBar != 'undefined') {
                reviseStatusBar();
            }
        }
        if (callback) {
            callback();
        }
    });
}

function textInputSize(elem) {
    elem.style.height = '0px';
    elem.style.height = `max(3.4rem, ${elem.scrollHeight + 5}px)`;
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
    elem.disabled = !toggler.checked;
    let elem2 = document.getElementById(id + '_rangeslider');
    if (elem2) {
        elem2.disabled = !toggler.checked;
    }
    let div = elem.parentElement.querySelector('.toggler-overlay-part');
    if (div) {
        div.remove();
    }
    if (elem.disabled) {
        let overlay = elem.parentElement.querySelector('.toggler-overlay');
        if (overlay.querySelector('.toggler-overlay-part')) {
            overlay.firstChild.remove();
        }
        let width = Math.max(elem.clientWidth, 200);
        if (elem2) {
            width = Math.max(width, elem2.clientWidth);
        }
        let height = Math.max(elem.clientHeight, 16);
        overlay.innerHTML = `<div class="toggler-overlay-part" style="width: ${width}px; height: ${height}px"></div>`;
        overlay.firstChild.addEventListener('click', () => {
            toggler.checked = true;
            elem.disabled = false;
            if (elem2) {
                elem2.disabled = false;
            }
            overlay.firstChild.remove();
            elem.focus();
        });
    }
}

function getToggleHtml(toggles, id, name, extraClass = '', func = 'doToggleEnable') {
    return toggles ? `<span class="form-check form-switch display-inline-block${extraClass}"><input class="auto-slider-toggle form-check-input" type="checkbox" id="${id}_toggle" title="Enable/disable ${name}" onclick="javascript:${func}('${id}')" autocomplete="false"></span>` : '';
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
                e.dataset.filedata = null;
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

function makeSliderInput(featureid, id, name, description, value, min, max, view_max = 0, step = 1, isPot = false, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let rangeVal = isPot ? potToLinear(value, max, min, step) : value;
    return `
    <div class="slider-auto-container">
    <div class="auto-input auto-slider-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-slider-fade-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <input class="auto-slider-number" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-ispot="${isPot}" autocomplete="false">
        <br>
        <div class="toggler-overlay"></div>
        <input class="auto-slider-range" type="range" id="${id}_rangeslider" value="${rangeVal}" min="${min}" max="${view_max}" step="${step}" data-ispot="${isPot}" autocomplete="false">
    </div></div>`;
}

function makeNumberInput(featureid, id, name, description, value, min, max, step = 1, small = false, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    if (small == 'seed') {
        return `
            <div class="auto-input auto-number-box" title="${name}: ${description}"${featureid}>
                <div class="auto-input-fade-lock auto-fade-max-contain">
                    <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
                </div>
                <div class="toggler-overlay"></div>
                <input class="auto-number auto-number-seedbox" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-name="${name}" autocomplete="false">
                <button class="basic-button" title="Random (Set to -1)" onclick="javascript:getRequiredElementById('${id}').value = -1;">&#x1F3B2;</button>
                <button class="basic-button" title="Reuse (from currently selected image)" onclick="javascript:reuseLastParamVal('${id}');">&#128257;</button>
            </div>`
    }
    if (small) {
        return `
        <div class="auto-input auto-number-box" title="${name}: ${description}"${featureid}>
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span>
            <div class="toggler-overlay"></div>
            <input class="auto-number-small" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" autocomplete="false">
        </div>`;
    }
    else {
    return `
        <div class="auto-input auto-number-box" title="${name}: ${description}"${featureid}>
            <div class="auto-input-fade-lock auto-fade-max-contain">
                <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
            </div>
            <div class="toggler-overlay"></div>
            <input class="auto-number" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-name="${name}" autocomplete="false">
        </div>`
    }
}

function makeTextInput(featureid, id, name, description, value, rows, placeholder, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let onInp = rows == 1 ? '' : ' oninput="javascript:textInputSize(this)"';
    return `
    <div class="auto-input auto-text-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-fade-max-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <div class="toggler-overlay"></div>
        <textarea class="auto-text" id="${id}" rows="${rows}"${onInp} placeholder="${escapeHtml(placeholder)}" data-name="${name}" autocomplete="false">${escapeHtml(value)}</textarea>
        <button class="interrupt-button image-clear-button" style="display: none;">Clear Images</button>
        <div class="added-image-area"></div>
    </div>`;
}

function makeCheckboxInput(featureid, id, name, description, value, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let checked = `${value}` == "true" ? ' checked="true"' : '';
    return `
    <div class="auto-input auto-checkbox-box" title="${name}: ${description}"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span>
        <br>
        <div class="toggler-overlay"></div>
        <input class="auto-checkbox" type="checkbox" data-name="${name}" id="${id}"${checked}> <span class="auto-input-description" autocomplete="false">${description}</span>
    </div>`;
}

function makeDropdownInput(featureid, id, name, description, values, defaultVal, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let html = `
    <div class="auto-input auto-dropdown-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-fade-max-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <div class="toggler-overlay"></div>
        <select class="auto-dropdown" id="${id}" autocomplete="false">`;
    for (let value of values) {
        let selected = value == defaultVal ? ' selected="true"' : '';
        html += `<option value="${escapeHtml(value)}"${selected}>${escapeHtml(value)}</option>`;
    }
    html += `
        </select>
    </div>`;
    return html;
}

function makeMultiselectInput(featureid, id, name, description, values, defaultVal, placeholder, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let html = `
    <div class="auto-input auto-dropdown-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-fade-max-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <div class="toggler-overlay"></div>
        <select class="form-select" id="${id}" autocomplete="false" data-placeholder="${escapeHtml(placeholder)}" multiple>`;
    for (let value of values) {
        let selected = value == defaultVal ? ' selected="true"' : '';
        html += `<option value="${escapeHtml(value)}"${selected}>${escapeHtml(value)}</option>`;
    }
    html += `
        </select>
    </div>`;
    return html;
}

function makeImageInput(featureid, id, name, description, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let html = `
    <div class="auto-input auto-file-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-fade-max-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton info-popover-button" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <div class="toggler-overlay"></div>
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
        return `${roundTo(wh, 0.01)}:1`;
    }
    return `1:${roundTo(hw, 0.01)}`;
}

function quickAppendButton(div, name, func) {
    let button = document.createElement('button');
    button.className = 'basic-button';
    button.innerText = name;
    button.onclick = func;
    div.appendChild(button);
}
