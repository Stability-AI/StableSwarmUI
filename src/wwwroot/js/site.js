function enableSlidersIn(elem) {
    for (let div of elem.getElementsByClassName('auto-slider-box')) {
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
            range.addEventListener('input', () => number.value = linearToPot(range.value, max, min, step));
            number.addEventListener('input', () => range.value = potToLinear(number.value, max, min, step));
            range.step = 1;
        }
        else {
            range.addEventListener('input', () => number.value = range.value);
            number.addEventListener('input', () => range.value = number.value);
        }
    }
}

function showError(message) {
    let container = document.getElementById('center_toast');
    let box = document.getElementById('error_toast_box');
    document.getElementById('error_toast_content').innerText = message;
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

function makeWSRequest(url, in_data, callback, depth = 0) {
    let ws_address = getWSAddress();
    if (ws_address == null) {
        console.log(`Tried making WS request ${url} but failed.`);
        showError('Failed to get WebSocket address. You may be connecting to the server in an unexpected way. Please use "http" or "https" URLs.');
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
                showError('Failed to get session ID after 3 tries. Your account may have been invalidated. Try refreshing the page, or contact the site owner.');
                return;
            }
            console.log('Session refused, will get new one and try again.');
            getSession(() => {
                makeWSRequest(url, in_data, callback, depth + 1);
            });
            return;
        }
        if (data.error) {
            let error = JSON.stringify(data.error);
            console.log(`Tried making WS request ${url} but failed with error: ${error}`);
            showError(error);
            return;
        }
        callback(data);
    }
    socket.onerror = genericServerError;
}

function genericRequest(url, in_data, callback, depth = 0) {
    in_data['session_id'] = session_id;
    sendJsonToServer(`/API/${url}`, in_data, (status, data) => {
        if (!data) {
            console.log(`Tried making generic request ${url} but failed.`);
            showError('Failed to send request to server. Did the server crash?');
            return;
        }
        if (data.error_id && data.error_id == 'invalid_session_id') {
            if (depth > 3) {
                showError('Failed to get session ID after 3 tries. Your account may have been invalidated. Try refreshing the page, or contact the site owner.');
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
            showError(data.error);
            return;
        }
        callback(data);
    }, genericServerError);
}

function getSession(callback) {
    genericRequest('GetNewSession', {}, data => {
        console.log("Session started.");
        session_id = data.session_id;
        if (callback) {
            callback();
        }
    });
}

function doToggleEnable(id) {
    let elem = document.getElementById(id);
    if (!elem) {
        console.log(`Tried to toggle ${id} but it doesn't exist.`);
        return;
    }
    let toggler = document.getElementById(id + "_toggle");
    if (!toggler) {
        console.log(`Tried to toggle ${id} but the toggler doesn't exist.`);
    }
    elem.disabled = !toggler.checked;
}

function getToggleHtml(toggles, id, name) {
    return toggles ? `<span class="form-check form-switch display-inline-block"><input class="auto-slider-toggle form-check-input" type="checkbox" id="${id}_toggle" title="Enable/disable ${name}" onclick="javascript:doToggleEnable('${id}')"></span>` : '';
}

function load_image_file(e) {
    let file = e.files[0];
    if (file) {
        let reader = new FileReader();
        reader.addEventListener("load", () => { e.dataset.filedata = reader.result; }, false);
        reader.readAsDataURL(file);
    }
    else {
        e.dataset.filedata = null;
    }
}

function makeSliderInput(featureid, id, name, description, value, min, max, step = 1, isPot = false, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let rangeVal = isPot ? potToLinear(value, max, min, step) : value;
    return `
    <div class="auto-input auto-slider-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-slider-fade-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <input class="auto-slider-number" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-ispot="${isPot}">
        <br>
        <input class="auto-slider-range" type="range" value="${rangeVal}" min="${min}" max="${max}" step="${step}" data-ispot="${isPot}">
    </div>`;
}

function makeNumberInput(featureid, id, name, description, value, min, max, step = 1, small = false, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    if (small) {
        return `
        <div class="auto-input auto-number-box" title="${name}: ${description}"${featureid}>
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton" onclick="javascript:doPopover('${id}')">?</span></span>
            <input class="auto-number-small" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}">
        </div>`;
    }
    else {
    return `
        <div class="auto-input auto-number-box" title="${name}: ${description}"${featureid}>
            <div class="auto-input-fade-lock auto-fade-max-contain">
                <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
            </div>
            <input class="auto-number" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}" data-name="${name}">
        </div>`
    }
}

function makeTextInput(featureid, id, name, description, value, rows, placeholder, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    return `
    <div class="auto-input auto-text-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-fade-max-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <textarea class="auto-text" id="${id}" rows="${rows}" placeholder="${escapeHtml(placeholder)}" data-name="${name}">${escapeHtml(value)}</textarea>
    </div>`;
}

function makeCheckboxInput(featureid, id, name, description, value, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let checked = `${value}` == "true" ? ' checked="true"' : '';
    return `
    <div class="auto-input auto-checkbox-box" title="${name}: ${description}"${featureid}>
        <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton" onclick="javascript:doPopover('${id}')">?</span></span>
        <br><input class="auto-checkbox" type="checkbox" id="${id}"${checked}> <span class="auto-input-description">${description}</span>
    </div>`;
}

function makeDropdownInput(featureid, id, name, description, values, defaultVal, toggles = false) {
    name = escapeHtml(name);
    description = escapeHtml(description);
    featureid = featureid ? ` data-feature-require="${featureid}"` : '';
    let html = `
    <div class="auto-input auto-dropdown-box" title="${name}: ${description}"${featureid}>
        <div class="auto-input-fade-lock auto-fade-max-contain">
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <select class="auto-dropdown" id="${id}">`;
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
            <span class="auto-input-name">${getToggleHtml(toggles, id, name)}${name}<span class="auto-input-qbutton" onclick="javascript:doPopover('${id}')">?</span></span> <span class="auto-input-description">${description}</span>
        </div>
        <br><input class="auto-file" type="file" accept="image/png, image/jpeg" id="${id}" onchange="load_image_file(this)">
    </div>`;
    return html;
}
