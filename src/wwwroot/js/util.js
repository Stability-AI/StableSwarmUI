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
