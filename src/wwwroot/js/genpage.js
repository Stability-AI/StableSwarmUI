let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height'];

function load() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
    for (let range of document.getElementsByClassName('image_size_slider')) {
        let number = range.nextElementSibling;
        range.addEventListener('input', () => number.value = range.value);
        number.addEventListener('input', () => range.value = number.value);
    }
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

load();
