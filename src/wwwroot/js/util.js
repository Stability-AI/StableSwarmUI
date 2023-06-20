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

function createDiv(id, classes) {
    let div = document.createElement('div');
    if (id != null) {
        div.id = id;
    }
    div.className = classes;
    return div;
}

function escapeHtml(text) {
    return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

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

function potToLinear(val, max, min, step) {
    let norm = Math.log2(val);
    let increments = Math.log2(max);
    let discardIncr = min == 0 ? 0 : Math.log2(min);
    let normIncr = (norm - discardIncr) / (increments - discardIncr);
    return Math.round(normIncr * max);
}

function findParentOfClass(elem, className) {
    while (elem != null) {
        if (elem.classList.contains(className)) {
            return elem;
        }
        elem = elem.parentElement;
    }
    return null;
}

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

function isChildOf(node, parentId) {
    while (node != null) {
        if (node.id == parentId) {
            return true;
        }
        node = node.parentNode;
    }
    return false;
}

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

function roundTo(val, step) {
    return Math.round(val / step) * step;
}

function autoS(num) {
    return num == 1 ? "" : "s";
}
