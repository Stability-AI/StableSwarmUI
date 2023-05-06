let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

function genpageLoad() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
}

function showError(message) {
    console.log(`Error: ${message}`);
    // TODO: Popup box
}

function genericServerError() {
    showError('Failed to send generate request to server. Did the server crash?');
}

function gotImageResults(images) {
    for (let image of images) {
        let img = document.createElement('img');
        img.src = 'data:image/png;base64,' + image;
        document.getElementById('image_spot').appendChild(img);
    }
}

function doGenerate() {
    let input = {};
    for (let id of core_inputs) {
        input[id] = document.getElementById('input_' + id).value;
    }
    sendJsonToServer('/API/GenerateText2Image', input, (status, data) => {
        if (data.error) {
            showError(data.error);
            return;
        }
        gotImageResults(data.images);
    }, genericServerError);
}

genpageLoad();
