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
