/** Dirt-simple direct POST request sender. */
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

/** Dirt-simple direct GET request sender. */
function getJsonDirect(url, callback, error_callback) {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.responseType = 'json';
    xhr.onload = function() {
        callback(xhr.status, xhr.response);
    };
    xhr.onerror = error_callback;
    xhr.setRequestHeader('Content-Type', 'application/json');
    xhr.send();
};

/** Gets the appropriate current WebSocket address for the server. */
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

/** Creates a new HTML span with the given ID and classnames. */
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

/** Escapes a string for use in HTML. */
function escapeHtml(text) {
    if (text == null) {
        return '';
    }
    return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;').replaceAll('\n', '\n<br>');
}

/** Escapes a string for use in HTML (no line break handling). */
function escapeHtmlNoBr(text) {
    if (text == null) {
        return '';
    }
    return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

/** Escapes a string for use in a JavaScript string literal. */
function escapeJsString(text) {
    if (text == null) {
        return '';
    }
    return text.replaceAll('\\', '\\\\').replaceAll('"', '\\"').replaceAll("'", "\\'").replaceAll('\n', '\\n').replaceAll('\r', '\\r').replaceAll('\t', '\\t');
}

function isHtmlSpanStyleAllowed(text) {
    if (text.startsWith('"') && text.endsWith('"')) {
        text = text.substring(1, text.length - 1);
    }
    let parts = text.split(';');
    for (let part of parts) {
        if (part.trim() == '') {
            continue;
        }
        let split = part.split(':');
        if (split.length != 2) {
            return false;
        }
        let key = split[0].trim();
        let value = split[1].trim();
        if (key == 'color' || key == 'background-color') {
            // #ff00ff or rgb(255, 0, 255)
            if (!value.match(/^#[0-9a-fA-F]{6}$/) && !value.match(/^rgb\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*\)$/)) { return false; }
        }
        else if (key == 'font-weight') {
            if (value != 'bold' && value != 'normal') { return false; }
        }
        else if (key == 'text-decoration') {
            if (value != 'underline' && value != 'line-through' && value != 'none') { return false; }
        }
        else if (key == 'font-style') {
            if (value != 'italic' && value != 'normal') { return false; }
        }
        else if (key == 'font-size') {
            // percentages in limited range
            if (!value.match(/^[0-2]?[0-9][0-9]%$/)) { return false; }
        }
        else {
            return false;
        }
    }
    return true;
}

let allowedHtmlTagNames = ['a', 'abbr', 'acronym', 'b', 'i', 's', 'span', 'u', 'br', 'hr', 'p', 'li', 'ul', 'ol', 'strong', 'h6', 'h5', 'h4', 'em', 'mark', 'aside', 'blockquote', 'q', 'pre', 'code', 'strike'];
let htmlTagSafeRemaps = { 'h3': 'h4', 'h2': 'h4', 'h1': 'h4'};
let allowedHtmlTagAttrs = ['href', 'title', 'style'];
let allowedHtmlFullAttrs = ['target="_blank"', 'target="_new"', 'rel="noopener noreferrer"', 'rel="noopener"', 'rel="noreferrer"', 'rel="ugc"', 'class="translate"'];
let autoExcludeHtmlAttrs = ['id', 'class'];
/** Partially escapes HTML, allowing 'basic format' codes (bold, italic, etc) to remain. */
function safeHtmlOnly(text) {
    if (text == null) {
        return '';
    }
    let tagStart = text.indexOf('<');
    if (tagStart < 0) {
        return text.replaceAll('>', '&gt;').replaceAll('\n', '\n<br>');
    }
    let tagEnd = text.indexOf('>', tagStart);
    if (tagEnd < 0) {
        return escapeHtml(text);
    }
    let prefix = text.substring(0, tagStart).replaceAll('>', '&gt;').replaceAll('\n', '\n<br>');
    let tag = text.substring(tagStart + 1, tagEnd);
    let suffix = safeHtmlOnly(text.substring(tagEnd + 1));
    let tagForSplit = tag.endsWith('/') ? tag.substring(0, tag.length - 1).trim() : tag;
    tagForSplit = tag.startsWith('/') ? tagForSplit.substring(1) : tagForSplit;
    let parts = splitWithQuoting(tagForSplit, ' ');
    let readdParts = parts.filter(part => allowedHtmlFullAttrs.includes(part));
    parts = parts.filter(part => !allowedHtmlFullAttrs.includes(part));
    let tagName = parts[0];
    if (htmlTagSafeRemaps[tagName]) {
        tagName = htmlTagSafeRemaps[tagName];
    }
    parts.shift();
    let splitParts = parts.map(part => splitWithQuoting(part, '=')).filter(part => !autoExcludeHtmlAttrs.includes(part[0]));
    let styleAttr = splitParts.find(part => part[0] == 'style');
    if (allowedHtmlTagNames.includes(tagName)
        && splitParts.every(part => part.length == 2 && part[1].startsWith('"') && part[1].endsWith('"'))
        && splitParts.every(part => allowedHtmlTagAttrs.includes(part[0]))
        && (!styleAttr || isHtmlSpanStyleAllowed(styleAttr[1]))
        && splitParts.filter(part => part[0] == 'style').length <= 1) {
            let result = `${prefix}<`;
            if (tag.startsWith('/')) {
                result += '/';
            }
            else {
                if (!suffix.includes(`</${tagName}>`)) {
                    suffix += `</${tagName}>`;
                }
            }
            result += tagName;
            for (let part of splitParts) {
                result += ` ${part[0]}=${part[1]}`;
            }
            for (let part of readdParts) {
                result += ` ${part}`;
            }
            return `${result}>${suffix}`;
    }
    return `${prefix}&lt;${escapeHtml(tag)}&gt;${suffix}`;
}

/** Splits the text around the splitChar, but allowing for "quoted sections" to be contained. */
function splitWithQuoting(text, splitChar) {
    let parts = [];
    let startInd = 0;
    let inQuote = false;
    for (let i = 0; i < text.length; i++) {
        let c = text.charAt(i);
        if (c == '"') {
            inQuote = !inQuote;
        }
        else if (c == splitChar && !inQuote) {
            parts.push(text.substring(startInd, i));
            startInd = i + 1;
            continue;
        }
    }
    parts.push(text.substring(startInd));
    return parts;
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
    if (val < min + step) {
        return min;
    }
    let norm = val / max;
    let increments = Math.log2(max);
    let discardIncr = min == 0 ? 0 : Math.log2(min);
    let normIncr = norm * (increments - discardIncr) + discardIncr;
    if (shiftMonitor) {
        return roundTo(Math.round(2 ** normIncr), step);
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

/** Power-of-two to linear conversion. See linearToPot for more info. */
function potToLinear(val, max, min, step) {
    let norm = Math.log2(val);
    let increments = Math.log2(max);
    let discardIncr = min == 0 ? 0 : Math.log2(min);
    let normIncr = (norm - discardIncr) / (increments - discardIncr);
    return Math.round(normIncr * max);
}

/** Returns the first parent element of the given element that has the given class, or null if none. */
function findParentOfClass(elem, className) {
    while (elem != null) {
        if (elem.classList && elem.classList.contains(className)) {
            return elem;
        }
        elem = elem.parentElement;
    }
    return null;
}

/** Returns all of the text nodes within an element. */
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

/** Sets the selection range of the given element to the given start and end character indices. This is for fixing contenteditable elements. */
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

/** Returns true if the given node is a child of the given parent. */
function isChildOf(node, parentId) {
    while (node != null) {
        if (node.id == parentId) {
            return true;
        }
        node = node.parentNode;
    }
    return false;
}

/** Returns the current cursor position in the given contenteditable span, in a way that compensates for sub-spans. */
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

/** Downloads the data at the given URL and returns a 'data:whatever,base64:...' URL. */
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

/** Returns the given value rounded to the nearest multiple of the given step. */
function roundTo(val, step) {
    return Math.round(val / step) * step;
}

/** Returns a string of the given value rounded to the nearest multiple of the given step, and fixed to have a reasonable number of digits after the decimal. */
function roundToStrAuto(val, step) {
    let stepStr = `${step}`;
    let dot = stepStr.indexOf('.');
    let decimals = dot < 0 ? 0 : stepStr.length - dot - 1;
    return roundToStr(roundTo(val, step), decimals + 2);
}

/** Returns a string of the given value rounded to have the given max number of digits after the decimal. */
function roundToStr(val, decimals) {
    let frac = 10 ** -decimals;
    let newVal = roundTo(val, frac) + frac * 0.001 * Math.sign(val);
    if (newVal < frac && newVal > -frac) {
        return '0';
    }
    let valStr = `${newVal}`;
    let dot = valStr.indexOf('.');
    if (dot < 0 || valStr.includes('e')) {
        return valStr;
    }
    let subStr = valStr.substring(0, dot + 1 + 2);
    while (subStr.endsWith('0')) {
        subStr = subStr.substring(0, subStr.length - 1);
    }
    if (subStr.endsWith('.')) {
        subStr = subStr.substring(0, subStr.length - 1);
    }
    return subStr;
}

/** Mini-helper for English text gen, returns "s" if num is not 1, "" otherwise. */
function autoS(num) {
    return num == 1 ? "" : "s";
}

/** Sets a cookie with the given name and value, which will expire after the given number of days. */
function setCookie(name, value, expirationDays, sameSite = 'Lax') {
    value = encodeURIComponent(value);
    const d = new Date();
    d.setTime(d.getTime() + (expirationDays * 24 * 60 * 60 * 1000));
    document.cookie = `${name}=${value};expires=${d.toUTCString()};path=/;SameSite=${sameSite}`;
}

/** Returns the value of the cookie with the given name, or an empty string if it doesn't exist. */
function getCookie(name) {
    name = name + "=";
    for(let part of document.cookie.split(';')) {
        let clean = part.trimStart();
        if (clean.startsWith(name)) {
            return decodeURIComponent(clean.substring(name.length));
        }
    }
    return "";
}

/** Lists all cookies that start with the given prefix. */
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

/** Deletes the cookie with the given name. */
function deleteCookie(name) {
    document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;SameSite=Lax`;
}

/** Returns the element with the given ID, or throws an error if it doesn't exist.
 * Equivalent to document.getElementById(id), but with a more helpful error message. */
function getRequiredElementById(id) {
    let elem = document.getElementById(id);
    if (!elem) {
        throw new Error(`Required element '${id}' not found.`);
    }
    return elem;
}

/** Gives the user a download for simple plaintext file content. */
function downloadPlainText(filename, text) {
    var element = document.createElement('a');
    element.setAttribute('href', 'data:text/plain;charset=utf-8,' + encodeURIComponent(text));
    element.setAttribute('download', filename);
    element.style.display = 'none';
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
}

/** Tiny parser for simple YAML files. */
function microYamlParse(text) {
    text = text.replaceAll('\r', '\n');
    let lines = text.split('\n');
    return microYamlParseBlock(lines, 0, 0).result;
}

/** Internal function for parsing YAML files. Use microYamlParse instead. */
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

/** Tiny CSV line parser. */
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

/** Reads the given file as text and passes the result to the given handler. Ignores null file inputs. */
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

/** Converts a number to a string of letters, where 1=a, 2=b, 3=c, ..., 26=aa, 27=ab, etc. */
function numberToLetters(id) {
    if (id > 26) {
        let rem = id % 26;
        id /= 26;
        return numberToLetters(id) + numberToLetters(rem);
    }
    return String.fromCharCode(id + 'a'.charCodeAt(0));
}

/** Converts eg the 1 in '1rem' for a CSS style to pixels (eg 16px). */
function convertRemToPixels(rem) {
    return rem * parseFloat(getComputedStyle(document.documentElement).fontSize);
}

/** Gets the value of the radio button that is selected in the given fieldset. */
function getRadioSelectionInFieldset(fieldset) {
    if (typeof fieldset == 'string') {
        fieldset = getRequiredElementById(fieldset);
    }
    for (let radio of fieldset.getElementsByTagName('input')) {
        if (radio.getAttribute('type') == 'radio' && radio.checked) {
            return radio.value;
        }
    }
    return null;
}

/** Creates a small data URL for the given image. */
function imageToSmallPreviewData(img) {
    let width = 256, height = 256;
    if (img.naturalWidth < img.naturalHeight) {
        width = Math.round(img.naturalWidth / img.naturalHeight * 256);
    }
    else if (img.naturalWidth > img.naturalHeight) {
        height = Math.round(img.naturalHeight / img.naturalWidth * 256);
    }
    let canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    let ctx = canvas.getContext('2d');
    ctx.drawImage(img, 0, 0, width, height);
    let result = canvas.toDataURL('image/jpeg');
    canvas.remove();
    return result;
}

/** Takes raw html input and strips all tags, leaving only the text. */
function stripHtmlToText(raw) {
    let div = document.createElement('div');
    div.innerHTML = raw.replaceAll('\n<br>', '\n').replaceAll('<br>\n', '\n').replaceAll('<br>', '\n');
    return div.textContent || div.innerText || '';
}

/** Forcibly guarantees a dropdown is updated to a given server, adding a new option if needed. */
function forceSetDropdownValue(elem, val) {
    if (typeof elem == 'string') {
        elem = getRequiredElementById(elem);
    }
    elem.value = val;
    if (elem.value != val) {
        let option = document.createElement('option');
        option.value = val;
        option.innerHTML = val;
        elem.appendChild(option);
        elem.value = val;
    }
    elem.dispatchEvent(new Event('change'));
    if (elem.onchange) {
        elem.onchange();
    }
}

/** Returns a string representing the given file size in a human-readable format. For example "1.23 GiB" */
function fileSizeStringify(size) {
    if (size > 1024 * 1024 * 1024) {
        return `${(size / (1024 * 1024 * 1024)).toFixed(2)} GiB`;
    }
    if (size > 1024 * 1024) {
        return `${(size / (1024 * 1024)).toFixed(2)} MiB`;
    }
    if (size > 1024) {
        return `${(size / 1024).toFixed(2)} KiB`;
    }
    return `${size} B`;
}

/** Returns a string representing the given duration in a human-readable format. For example "1h 23m" */
function durationStringify(seconds) {
    let hours = Math.floor(seconds / 3600);
    seconds -= hours * 3600;
    let minutes = Math.floor(seconds / 60);
    seconds -= minutes * 60;
    let result = '';
    if (hours > 0) {
        result += `${hours}h `;
    }
    if (minutes > 0) {
        result += `${minutes}m `;
    }
    if (hours == 0) {
        result += `${Math.floor(seconds)}s`;
    }
    return result;
}

/** Filters the array to only contain values for which the map function returns a distinct (unique) value. */
function filterDistinctBy(array, map) {
    return array.filter((value, index) => {
        let mapped = map(value);
         return array.findIndex(v => map(v) == mapped) == index;
    });
}

/** Gets the current value of an input element (in a checkbox-compatible way). */
function getInputVal(input) {
    if (input.tagName == 'INPUT' && input.type == 'checkbox') {
        return input.checked;
    }
    else if (input.tagName == 'INPUT' && input.type == 'file') {
        if (input.dataset.filedata) {
            return input.dataset.filedata;
        }
        return null;
    }
    else if (input.tagName == 'SELECT' && input.multiple) {
        let valSet = [...input.selectedOptions].map(option => option.value);
        if (valSet.length > 0) {
            return valSet.join(',');
        }
        return '';
    }
    return input.value;
}

/** Sets the current value of an input element (in a checkbox-compatible way). */
function setInputVal(input, val) {
    if (input.type && input.type == 'checkbox') {
        input.checked = `${val}` == "true";
    }
    else {
        input.value = val;
    }
}

/** JavaScript sucks at floating point numerics, so this is a hacky way to format numbers un-stupidly. */
function formatNumberClean(num, maxDigits) {
    let fixed = num.toFixed(maxDigits);
    return parseFloat(fixed);
}

/** Gets a data image URL from an image src. */
function imageToData(src, callback, resize256 = false) {
    if (resize256) {
        var image = new Image();
        image.crossOrigin = 'Anonymous';
        image.onload = () => {
                let canvas = document.createElement('canvas');
                let context = canvas.getContext('2d');
                let targetMp = 256 * 256;
                let mp = image.width * image.height;
                let ratio = Math.sqrt(targetMp / mp);
                let widthFixed = Math.round(image.width * ratio);
                let heightFixed = Math.round(image.height * ratio);
                canvas.width = widthFixed;
                canvas.height = heightFixed;
                context.drawImage(image, 0, 0, widthFixed, heightFixed);
                callback(canvas.toDataURL('image/jpeg'));
        };
        image.src = src;
    }
    else {
        let xhr = new XMLHttpRequest();
        xhr.onload = () => {
            let reader = new FileReader();
            reader.onloadend = () => {
                callback(reader.result);
            };
            reader.readAsDataURL(xhr.response);
        };
        xhr.open('GET', src);
        xhr.responseType = 'blob';
        xhr.send();
    }
}

/** Takes a UTF-16 Uint8Array and returns a string. */
function decodeUtf16(data) {
    let output = [];
    for (let i = 0; i < data.length; i += 2) {
        output.push(String.fromCharCode((data[i + 1] << 8) + data[i]));
    }
    return output.join('');
}

/** Returns whether two arrays are equal. */
function arraysEqual(arr1, arr2) {
    if (arr1.length != arr2.length) {
        return false;
    }
    for (let i = 0; i < arr1.length; i++) {
        if (arr1[i] != arr2[i]) {
            return false;
        }
    }
    return true;
}

/** Returns an integer hashcode for a string. */
function hashCode(s) {
    let h;
    for(let i = 0; i < s.length; i++) {
          h = Math.imul(31, h) + s.charCodeAt(i) | 0;
    }
    return h;
}

/** Returns a human-readable string formatting of the Date object's date-time value (yyyy-MM-dd HH:mm:ss) */
function formatDateTime(date) {
    function pad(n) {
        return n < 10 ? '0' + n : n;
    }
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

/** Escapes a string for use in a regex. */
function regexEscape(text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
