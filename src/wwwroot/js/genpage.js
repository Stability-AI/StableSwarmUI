let core_inputs = ['prompt', 'negative_prompt', 'seed'];

function load() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
}

function showError(message) {
    console.log(`Error: ${message}`);
    // TODO: Popup box
}

function genericServerError() {
    showError('Failed to send generate request to server. Did the server crash?');
}

function doGenerate() {
    let input = {};
    for (let id of core_inputs) {
        input[id] = document.getElementById('input_' + id).value;
    }
    sendJsonToServer('/API/GenerateText2Image', input, (status, data) => {
        console.log(`Status: ${status}, data: ${JSON.stringify(data)}`);
    }, genericServerError);
}

load();
