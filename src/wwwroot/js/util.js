/**
 * Dirt-simple direct POST request sender.
 */
function sendJsonToServer(url, json_input, callback, error_callback) {
    var xhr = new XMLHttpRequest();
    xhr.open('POST', url, true);
    xhr.responseType = 'json';
    xhr.onload = function() {
        callback(xhr.status, xhr.response);
    };
    xhr.onerror = error_callback;
    xhr.setRequestHeader('Content-Type', 'application/json');
    xhr.send(JSON.stringify(json_input));
};

/**
 * Gets the appropriate current WebSocket address for the server.
 */
function getWSAddress() {
    let url = document.URL;
    let wsPrefix = null;
    if (url.startsWith("http://")) {
        wsPrefix = "ws://";
        url = url.substring("http://".length);
    }
    else if (url.startsWith("https://")) {
        wsPrefix = "wss://";
        url = url.substring("https://".length);
    }
    else {
        console.log("URL is not HTTP or HTTPS, cannot determine WebSocket path.");
        return null;
    }
    let slashIndex = url.indexOf("/");
    if (slashIndex != -1) {
        url = url.substring(0, slashIndex);
    }
    return wsPrefix + url;
}

/**
 * Creates a new HTML span with the given ID and classnames.
 */
function createSpan(id, classes, html = null) {
    let span = document.createElement('span');
    if (id != null) {
        span.id = id;
    }
    span.className = classes;
    if (html) {
        span.innerHTML = html;
    }
    return span;
}

/**
 * Creates a new HTML div with the given ID and classnames.
 */
function createDiv(id, classes, html = null) {
    let div = document.createElement('div');
    if (id != null) {
        div.id = id;
    }
    div.className = classes;
    if (html) {
        div.innerHTML = html;
    }
    return div;
}

/**
 * Escapes a string for use in HTML.
 */
function escapeHtml(text) {
    return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

/**
 * Escapes a string for use in a JavaScript string literal.
 */
function escapeJsString(text) {
    return text.replaceAll('\\', '\\\\').replaceAll('"', '\\"').replaceAll("'", "\\'").replaceAll('\n', '\\n').replaceAll('\r', '\\r').replaceAll('\t', '\\t');
}

let shiftMonitor = false;
document.addEventListener('keydown', (event) => {
    shiftMonitor = event.shiftKey;
});
document.addEventListener('keyup', (event) => {
    shiftMonitor = event.shiftKey;
});

/**
 * This function has the goal of never being noticed until it's missing. A thankless mathematical hero to the end-user.
 * Used for width/height sliders, this shifts the range of the slider into exponential Power-of-Two (POT) range.
 * That is to say, it naturally sections the values in even 256, 512, 1024, etc. increments, with sub-increments like 768 accessible in-between.
 * This makes the slider an absolute pleasure to use, even with a very large potential range of values.
 * (This is as opposed to a normal linear slider, which would have very small steps that are hard to land on exactly the number you want if the range is too high.)
 */
function linearToPot(val, max, min, step) {
    let norm = val / max;
    let increments = Math.log2(max);
    let discardIncr = min == 0 ? 0 : Math.log2(min);
    let normIncr = norm * (increments - discardIncr) + discardIncr;
    if (shiftMonitor) {
        return Math.round(2 ** normIncr);
    }
    let incrLow = Math.floor(normIncr);
    let incrHigh = Math.ceil(normIncr);
    let realLow = Math.round(2 ** incrLow); // Note: round to prevent floating point errors
    let realHigh = Math.round(2 ** incrHigh);
    if (realLow == realHigh) {
        return realLow;
    }
    let stepCount = 9999;
    step /= 2;
    while (stepCount > 4) {
        step *= 2;
        stepCount = Math.round((realHigh - realLow) / step);
        if (stepCount <= 1) {
            return 2 ** Math.round(normIncr);
        }
    }
    let subProg = (normIncr - incrLow) / (incrHigh - incrLow);
    let subStep = Math.round(subProg * stepCount);
    return realLow + subStep * step;
}

/**
 * Power-of-two to linear conversion. See linearToPot for more info.
 */
function potToLinear(val, max, min, step) {
    let norm = Math.log2(val);
    let increments = Math.log2(max);
    let discardIncr = min == 0 ? 0 : Math.log2(min);
    let normIncr = (norm - discardIncr) / (increments - discardIncr);
    return Math.round(normIncr * max);
}

/**
 * Returns the first parent element of the given element that has the given class, or null if  none.
 */
function findParentOfClass(elem, className) {
    while (elem != null) {
        if (elem.classList && elem.classList.contains(className)) {
            return elem;
        }
        elem = elem.parentElement;
    }
    return null;
}

/**
 * Returns all of the text nodes within an element.
 */
function getTextNodesIn(node) {
    var textNodes = [];
    if (node.nodeType == 3) {
        textNodes.push(node);
    }
    else {
        for (let child of node.childNodes) {
            textNodes.push.apply(textNodes, getTextNodesIn(child));
        }
    }
    return textNodes;
}

/**
 * Sets the selection range of the given element to the given start and end character indices.
 * This is for fixing contenteditable elements.
 */
function setSelectionRange(el, start, end) {
    let range = document.createRange();
    range.selectNodeContents(el);
    let textNodes = getTextNodesIn(el);
    let foundStart = false;
    let charCount = 0
    let endCharCount;
    for (let textNode of textNodes) {
        endCharCount = charCount + textNode.length;
        if (!foundStart && start >= charCount && start <= endCharCount) {
            range.setStart(textNode, start - charCount);
            foundStart = true;
        }
        if (foundStart && end <= endCharCount) {
            range.setEnd(textNode, end - charCount);
            break;
        }
        charCount = endCharCount;
    }
    let sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(range);
}

/**
 * Returns true if the given node is a child of the given parent.
 */
function isChildOf(node, parentId) {
    while (node != null) {
        if (node.id == parentId) {
            return true;
        }
        node = node.parentNode;
    }
    return false;
}

/**
 * Returns the current cursor position in the given contenteditable span, in a way that compensates for sub-spans.
 */
function getCurrentCursorPosition(parentId) {
    let selection = window.getSelection();
    let charCount = -1;
    let node;
    if (selection.focusNode && isChildOf(selection.focusNode, parentId)) {
        node = selection.focusNode;
        charCount = selection.focusOffset;
        if (node.id == parentId) {
            let i = 0;
            let altCount = 0;
            for (let child of node.childNodes) {
                if (i++ < charCount) {
                    altCount += child.textContent.length;
                }
            }
            return altCount;
        }
        while (node) {
            if (node.id == parentId) {
                break;
            }
            else if (node.previousSibling) {
                node = node.previousSibling;
                charCount += node.textContent.length;
            }
            else {
                node = node.parentNode;
            }
        }
    }
    return charCount;
}

/**
 * Downloads the data at the given URL and returns a 'data:whatever,base64:...' URL.
 */
function toDataURL(url, callback) {
    var xhr = new XMLHttpRequest();
    xhr.onload = function() {
        var reader = new FileReader();
        reader.onloadend = function() {
            callback(reader.result);
        }
        reader.readAsDataURL(xhr.response);
    };
    xhr.open('GET', url);
    xhr.responseType = 'blob';
    xhr.send();
}

/**
 * Returns the given value rounded to the nearest multiple of the given step.
 */
function roundTo(val, step) {
    return Math.round(val / step) * step;
}

/**
 * Mini-helper for English text gen, returns "s" if num is not 1, "" otherwise.
 */
function autoS(num) {
    return num == 1 ? "" : "s";
}

/**
 * Sets a cookie with the given name and value, which will expire after the given number of days.
 */
function setCookie(name, value, expirationDays, sameSite = 'Lax') {
    const d = new Date();
    d.setTime(d.getTime() + (expirationDays * 24 * 60 * 60 * 1000));
    document.cookie = `${name}=${value};expires=${d.toUTCString()};path=/;SameSite=${sameSite}`;
}

/**
 * Returns the value of the cookie with the given name, or an empty string if it doesn't exist.
 */
function getCookie(name) {
    name = name + "=";
    let decodedCookie = decodeURIComponent(document.cookie);
    let ca = decodedCookie.split(';');
    for(let i = 0; i < ca.length; i++) {
        let c = ca[i].trim();
        if (c.indexOf(name) == 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
}

/**
 * Lists all cookies that start with the given prefix.
 */
function listCookies(prefix) {
    let decodedCookie = decodeURIComponent(document.cookie);
    let ca = decodedCookie.split(';');
    let result = [];
    for(let i = 0; i < ca.length; i++) {
        let c = ca[i].trim();
        let equal = c.indexOf('=');
        let name = c.substring(0, equal);
        if (name.startsWith(prefix)) {
            result.push(name);
        }
    }
    return result;
}

/**
 * Deletes the cookie with the given name.
 */
function deleteCookie(name) {
    document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;SameSite=Lax`;
}

/**
 * Returns the element with the given ID, or throws an error if it doesn't exist.
 * Equivalent to document.getElementById(id), but with a more helpful error message.
 */
function getRequiredElementById(id) {
    let elem = document.getElementById(id);
    if (!elem) {
        throw new Error(`Required element '${id}' not found.`);
    }
    return elem;
}

/**
 * Gives the user a download for simple plaintext file content.
 */
function downloadPlainText(filename, text) {
    var element = document.createElement('a');
    element.setAttribute('href', 'data:text/plain;charset=utf-8,' + encodeURIComponent(text));
    element.setAttribute('download', filename);
    element.style.display = 'none';
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
}

/**
 * Tiny parser for simple YAML files.
 */
function microYamlParse(text) {
    text = text.replaceAll('\r', '\n');
    let lines = text.split('\n');
    return microYamlParseBlock(lines, 0, 0).result;
}

/**
 * Internal function for parsing YAML files. Use microYamlParse instead.
 */
function microYamlParseBlock(lines, start, minSpace) {
    let result = {};
    let i;
    let buildKey = null;
    for (i = start; i < lines.length; i++) {
        let line = lines[i];
        let trimStart = line.trimStart();
        let content = trimStart.trimEnd();
        let commentIndex = content.indexOf('#');
        if (commentIndex >= 0) {
            content = content.substring(0, commentIndex).trim();
        }
        if (content.length == 0) {
            continue;
        }
        let spaceCount = line.length - trimStart.length;
        if (spaceCount < minSpace) {
            break;
        }
        if (spaceCount > minSpace) {
            if (buildKey) {
                let subResult = microYamlParseBlock(lines, i, spaceCount);
                result[buildKey] = subResult.result;
                i = subResult.i - 1;
                continue;
            }
            else {
                throw new Error(`Invalid micro yaml line: ${line}, expected key:value or sub block`);
            }
        }
        buildKey = null;
        let colon = content.indexOf(':');
        if (colon < 0) {
            throw new Error(`Invalid micro yaml line: ${line}`);
        }
        let key = content.substring(0, colon).trim();
        let value = content.substring(colon + 1).trim();
        if (value == '') {
            buildKey = key;
            result[key] = {};
            continue;
        }
        if (value.startsWith('"') && value.endsWith('"')) {
            value = value.substring(1, value.length - 1);
        }
        else if (value.startsWith("'") && value.endsWith("'")) {
            value = value.substring(1, value.length - 1);
        }
        result[key] = value;
    }
    return { result, i };
}

/**
 * Tiny CSV line parser.
 */
function parseCsvLine(text) {
    let result = [];
    let inQuotes = false;
    let current = '';
    for (let i = 0; i < text.length; i++) {
        let c = text.charAt(i);
        if (c == '"') {
            if (inQuotes) {
                if (i + 1 < text.length && text.charAt(i + 1) == '"') {
                    current += '"';
                    i++;
                }
                else {
                    inQuotes = false;
                }
            }
            else {
                inQuotes = true;
            }
        }
        else if (c == ',') {
            if (inQuotes) {
                current += ',';
            }
            else {
                result.push(current);
                current = '';
            }
        }
        else {
            current += c;
        }
    }
    result.push(current);
    return result;
}

/**
 * Reads the given file as text and passes the result to the given handler.
 * Ignores null file inputs.
 */
function readFileText(file, handler) {
    if (!file) {
        return;
    }
    let reader = new FileReader();
    reader.onload = (e) => {
        handler(e.target.result);
    };
    reader.readAsText(file);
}

/**
 * Converts a number to a string of letters, where 1=a, 2=b, 3=c, ..., 26=aa, 27=ab, etc.
 */
function numberToLetters(id) {
    if (id > 26) {
        let rem = id % 26;
        id /= 26;
        return inputIdToName(id) + inputIdToName(rem);
    }
    return String.fromCharCode(id + 'a'.charCodeAt(0));
}

/**
 * Converts eg the 1 in '1rem' for a CSS style to pixels (eg 16px).
 */
function convertRemToPixels(rem) {
    return rem * parseFloat(getComputedStyle(document.documentElement).fontSize);
}
