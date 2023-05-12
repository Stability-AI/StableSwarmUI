function siteLoad() {
    for (let div of document.getElementsByClassName('auto-slider-box')) {
        let range = div.querySelector('input[type="range"]');
        let number = div.querySelector('input[type="number"]');
        range.addEventListener('input', () => number.value = range.value);
        number.addEventListener('input', () => range.value = number.value);
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
            console.log(`Tried making WS request ${url} but failed with error: ${data.error}`);
            showError(data.error);
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

function makeNumberInput(id, name, description, value, min, max, step = 1) {
    js = `${escapeJsString(name)}: ${escapeJsString(description)}`;
    name = escapeHtml(name);
    description = escapeHtml(description);
    return `
    <div class="auto-input auto-number-box" title="${name}: ${description}">
        <div class="auto-input-fade-lock auto-fade-max-contain" onclick="alert('${js}')">
            <span class="auto-input-name">${name}</span> <span class="auto-input-description">${description}</span>
        </div>
        <input class="auto-number" type="number" id="${id}" value="${value}" min="${min}" max="${max}" step="${step}">
    </div>`
}

function makeTextInput(id, name, description, value, rows, placeholder) {
    js = `${escapeJsString(name)}: ${escapeJsString(description)}`;
    name = escapeHtml(name);
    description = escapeHtml(description);
    return `
    <div class="auto-input auto-text-box" title="${name}: ${description}">
        <div class="auto-input-fade-lock auto-fade-max-contain" onclick="alert('${js}')">
            <span class="auto-input-name">${name}</span> <span class="auto-input-description">${description}</span>
        </div>
        <textarea class="auto-text" id="${id}" rows="${rows}" placeholder="${escapeHtml(placeholder)}" data-name="${name}">${escapeHtml(value)}</textarea>
    </div>`;
}

siteLoad();
