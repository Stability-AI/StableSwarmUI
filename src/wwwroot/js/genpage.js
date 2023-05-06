let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

let session_id = null;

const time_started = Date.now();

function genericRequest(url, data, callback) {
    sendJsonToServer(`/API/${url}`, data, (status, data) => {
        if (data.error) {
            showError(data.error);
            return;
        }
        callback(data);
    }, genericServerError);
}

function getSession() {
    genericRequest('GetNewSession', {}, data => {
        console.log("Session started.");
        session_id = data.session_id;
    });
}

function showError(message) {
    console.log(`Error: ${message}`);
    // TODO: Popup box
}

function genericServerError() {
    showError('Failed to send request to server. Did the server crash?');
}

function gotImageResults(images) {
    for (let image of images) {
        let img = document.createElement('img');
        img.src = 'data:image/png;base64,' + image;
        document.getElementById('image_spot').appendChild(img);
    }
}

function doGenerate() {
    if (session_id == null) {
        if (Date.now() - time_started > 1000 * 60) {
            showError("Cannot generate, session not started. Did the server crash?");
        }
        else {
            showError("Cannot generate, session not started. Please wait a moment for the page to load.");
        }
        return;
    }
    let input = {};
    for (let id of core_inputs) {
        input[id] = document.getElementById('input_' + id).value;
    }
    genericRequest('GenerateText2Image', {}, data => {
        gotImageResults(data.images);
    });
}

function genpageLoad() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
}

genpageLoad();
