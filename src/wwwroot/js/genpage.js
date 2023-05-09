let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

let session_id = null;

const time_started = Date.now();

function appendImage(spot, imageSrc) {
    let div = document.createElement('div');
    div.classList.add('image-block');
    let img = document.createElement('img');
    img.src = imageSrc;
    div.appendChild(img);
    document.getElementById(spot).appendChild(div);
}

function gotImageResult(image) {
    let src = 'data:image/png;base64,' + image;
    appendImage('current_image_batch', src);
    appendImage('image_history', src);
    document.getElementById('current_image').innerHTML = '';
    appendImage('current_image', src);
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
    document.getElementById('current_image_batch').innerHTML = '';
    makeWSRequest('GenerateText2ImageWS', input, data => {
        gotImageResult(data.image);
    });
}

function genpageLoad() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
    getSession();
}

genpageLoad();
