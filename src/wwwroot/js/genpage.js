let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

let session_id = null;

let batches = 0;

const time_started = Date.now();

function clickImageInBatch(div) {
    setCurrentImage(div.getElementsByTagName('img')[0].src);
}

function selectImageInHistory(div) {
    let batchId = div.dataset.batch_id;
    document.getElementById('current_image_batch').innerHTML = '';
    for (let img of document.getElementById('image_history').querySelectorAll(`[data-batch_id="${batchId}"]`)) {
        let batch_div = appendImage('current_image_batch', img.getElementsByTagName('img')[0].src, batchId);
        batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    }
    setCurrentImage(div.getElementsByTagName('img')[0].src);
}

function setCurrentImage(src) {
    let curImg = document.getElementById('current_image');
    curImg.innerHTML = '';
    let img = document.createElement('img');
    img.src = src;
    curImg.appendChild(img);
}

function appendImage(spot, imageSrc, batchId) {
    let div = document.createElement('div');
    div.classList.add('image-block');
    div.classList.add(`image-batch-${batchId % 2}`);
    div.dataset.batch_id = batchId;
    let img = document.createElement('img');
    img.src = imageSrc;
    div.appendChild(img);
    document.getElementById(spot).appendChild(div);
    return div;
}

function gotImageResult(image) {
    let src = 'data:image/png;base64,' + image;
    let batch_div = appendImage('current_image_batch', src, batches);
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    let history_div = appendImage('image_history', src, batches);
    history_div.addEventListener('click', () => selectImageInHistory(history_div));
    setCurrentImage(src);
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
    batches++;
    makeWSRequest('GenerateText2ImageWS', input, data => {
        gotImageResult(data.image);
    });
}

function genpageLoad() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
    getSession();
}

genpageLoad();
