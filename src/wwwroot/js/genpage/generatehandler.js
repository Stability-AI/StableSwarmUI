class GenerateHandler {

    constructor() {
        this.batchesEver = 0;
        this.totalGensThisRun = 0;
        this.totalGenRunTime = 0;
    }

    resetBatchIfNeeded() {
        resetBatchIfNeeded();
    }

    getGenInput(input_overrides = {}, input_preoverrides = {}) {
        return getGenInput(input_overrides, input_preoverrides);
    }

    setCurrentImage(src, metadata = '', batchId = '', previewGrow = false, smoothAdd = false) {
        setCurrentImage(src, metadata, batchId, previewGrow, smoothAdd);
    }

    gotImageResult(image, metadata, batchId) {
        return gotImageResult(image, metadata, batchId);
    }

    gotImagePreview(image, metadata, batchId) {
        return gotImagePreview(image, metadata, batchId);
    }
    
    appendGenTimeFrom(time) {
        this.totalGensThisRun++;
        this.totalGenRunTime += time;
    }
    
    doGenerate(input_overrides = {}, input_preoverrides = {}) {
        if (session_id == null) {
            if (Date.now() - time_started > 1000 * 60) {
                showError("Cannot generate, session not started. Did the server crash?");
            }
            else {
                showError("Cannot generate, session not started. Please wait a moment for the page to load.");
            }
            return;
        }
        let isPreview = '_preview' in input_overrides;
        if (isPreview) {
            delete input_overrides['_preview'];
        }
        num_current_gens += parseInt(getRequiredElementById('input_images').value);
        setCurrentModel(() => {
            if (getRequiredElementById('current_model').value == '') {
                showError("Cannot generate, no model selected.");
                return;
            }
            this.resetBatchIfNeeded();
            let images = {};
            let batch_id = this.batchesEver++;
            let discardable = {};
            let timeLastGenHit = Date.now();
            let actualInput = this.getGenInput(input_overrides, input_preoverrides);
            makeWSRequestT2I('GenerateText2ImageWS', actualInput, data => {
                if (isPreview) {
                    if (data.image) {
                        this.setCurrentImage(data.image, data.metadata, `${batch_id}_${data.batch_index}`, false, true);
                    }
                    return;
                }
                if (data.image) {
                    let timeNow = Date.now();
                    let timeDiff = timeNow - timeLastGenHit;
                    timeLastGenHit = timeNow;
                    this.appendGenTimeFrom(timeDiff / 1000);
                    if (!(data.batch_index in images)) {
                        let batch_div = this.gotImageResult(data.image, data.metadata, `${batch_id}_${data.batch_index}`);
                        images[data.batch_index] = {div: batch_div, image: data.image, metadata: data.metadata, overall_percent: 0, current_percent: 0};
                    }
                    else {
                        let imgHolder = images[data.batch_index];
                        if (!document.getElementById('current_image_img') || autoLoadImagesElem.checked || document.getElementById('current_image_img').dataset.batch_id == `${batch_id}_${data.batch_index}`) {
                            this.setCurrentImage(data.image, data.metadata, `${batch_id}_${data.batch_index}`, false, true);
                        }
                        let imgElem = imgHolder.div.querySelector('img');
                        imgElem.src = data.image;
                        delete imgElem.dataset.previewGrow;
                        imgHolder.image = data.image;
                        imgHolder.div.dataset.src = data.image;
                        imgHolder.div.dataset.metadata = data.metadata;
                        let progress_bars = imgHolder.div.querySelector('.image-preview-progress-wrapper');
                        if (progress_bars) {
                            progress_bars.remove();
                        }
                    }
                    images[data.batch_index].image = data.image;
                    images[data.batch_index].metadata = data.metadata;
                    discardable[data.batch_index] = images[data.batch_index];
                    delete images[data.batch_index];
                }
                if (data.gen_progress) {
                    if (!(data.gen_progress.batch_index in images)) {
                        let batch_div = this.gotImagePreview(data.gen_progress.preview ?? 'imgs/model_placeholder.jpg', `{"preview": "${data.gen_progress.current_percent}"}`, `${batch_id}_${data.gen_progress.batch_index}`);
                        images[data.gen_progress.batch_index] = {div: batch_div, image: null, metadata: null, overall_percent: 0, current_percent: 0};
                        let progress_bars_html = `<div class="image-preview-progress-inner"><div class="image-preview-progress-overall"></div><div class="image-preview-progress-current"></div></div>`;
                        let progress_bars = createDiv(null, 'image-preview-progress-wrapper', progress_bars_html);
                        batch_div.prepend(progress_bars);
                    }
                    let imgHolder = images[data.gen_progress.batch_index];
                    let overall = imgHolder.div.querySelector('.image-preview-progress-overall');
                    if (overall && data.gen_progress.overall_percent) {
                        imgHolder.overall_percent = data.gen_progress.overall_percent;
                        imgHolder.current_percent = data.gen_progress.current_percent;
                        overall.style.width = `${imgHolder.overall_percent * 100}%`;
                        imgHolder.div.querySelector('.image-preview-progress-current').style.width = `${imgHolder.current_percent * 100}%`;
                        if (data.gen_progress.preview && autoLoadPreviewsElem.checked && imgHolder.image == null) {
                            this.setCurrentImage(data.gen_progress.preview, `{"preview": "${data.gen_progress.current_percent}"}`, `${batch_id}_${data.gen_progress.batch_index}`, true);
                        }
                        let curImgElem = document.getElementById('current_image_img');
                        if (data.gen_progress.preview && (!imgHolder.image || data.gen_progress.preview != imgHolder.image)) {
                            if (curImgElem && curImgElem.dataset.batch_id == `${batch_id}_${data.gen_progress.batch_index}`) {
                                curImgElem.src = data.gen_progress.preview;
                                let metadata = getRequiredElementById('current_image').querySelector('.current-image-data');
                                if (metadata) {
                                    metadata.remove();
                                }
                            }
                            imgHolder.div.querySelector('img').src = data.gen_progress.preview;
                            imgHolder.image = data.gen_progress.preview;
                        }
                    }
                }
                if (data.discard_indices) {
                    let needsNew = false;
                    for (let index of data.discard_indices) {
                        let img = discardable[index] ?? images[index];
                        if (img) {
                            img.div.remove();
                            let curImgElem = document.getElementById('current_image_img');
                            if (curImgElem && curImgElem.src == img.image) {
                                needsNew = true;
                                delete discardable[index];
                            }
                        }
                    }
                    if (needsNew) {
                        let imgs = Object.values(discardable);
                        if (imgs.length > 0) {
                            this.setCurrentImage(imgs[0].image, imgs[0].metadata);
                        }
                    }
                    if (Object.keys(discardable).length > 0) {
                        // clear any lingering previews
                        for (let img of Object.values(images)) {
                            img.div.remove();
                        }
                    }
                }
            });
        });
    }
}
