
class ImageBatcherClass {

    doGenerate() {
        resetBatchIfNeeded();
        let inData = {
            'baseParams': getGenInput(),
            'input_folder': getRequiredElementById('ext_image_batcher_inputfolder').value,
            'output_folder': getRequiredElementById('ext_image_batcher_outputfolder').value,
            'init_image': getRequiredElementById('ext_image_batcher_use_as_init').checked,
            'revision': getRequiredElementById('ext_image_batcher_use_as_revision').checked,
            'controlnet': getRequiredElementById('ext_image_batcher_use_as_controlnet').checked,
            'resMode': getRequiredElementById('ext_image_batcher_res_mode').value
        };
        makeWSRequestT2I('ImageBatchRun', inData, data => {
            if (data.image) {
                gotImageResult(data.image, data.metadata);
            }
        });
    }

    register() {
        let doGenWrapper = () => {
            setCurrentModel(() => {
                if (document.getElementById('current_model').value == '') {
                    showError("Cannot run generate batch, no model selected.");
                    return;
                }
                this.doGenerate();
            });
        };
        this.mainDiv = registerNewTool('image_batcher', 'Image Edit Batcher', 'Run Batch', doGenWrapper);
        this.mainDiv.innerHTML = `The Image Batcher tool lets you run a batch of images from an arbitrary local file folder through SD and export to another folder. Use the settings below to pick which folders, and which values the images shall be fed as inputs to, then click the primary Generate button above.<br><b>IMPORTANT:</b> make sure the parameters you're using are enabled. If you're using batched Inits, you need the Init Image parameter group enabled!<br>`
            + makeTextInput(null, 'ext_image_batcher_inputfolder', '', 'Input Folder', 'Folder path for input images.', '', 'normal', 'Folder path for input images.\nThis folder should contain a non-recursive single layer of image files (png/jpg).', false, true, true)
            + makeTextInput(null, 'ext_image_batcher_outputfolder', '', 'Output Folder', 'Folder path for image output.', '', 'normal', 'Folder path for image output.\nIt is highly recommended that this is an empty folder.', false, true, true)
            + makeCheckboxInput(null, 'ext_image_batcher_use_as_init', '', 'Use As Init', 'Whether to use the image as the Init Image parameter.', true, false, true, true)
            + makeCheckboxInput(null, 'ext_image_batcher_use_as_controlnet', '', 'Use As ControlNet Input', 'Whether to use the image as input to ControlNet (only applies if a ControlNet model is enabled).', true, false, true, true)
            + makeCheckboxInput(null, 'ext_image_batcher_use_as_revision', '', 'Use As ReVision', 'Whether to use the image as a ReVision image input.', false, false, true, true)
            + `Resolution: <select id="ext_image_batcher_res_mode"><option>From Parameter</option><option>From Image</option><option>Scale To Model</option><option>Scale To Model Or Above</option></select>`;
        toolSelector.addEventListener('change', () => {
            if (toolSelector.value == 'image_batcher') {
                showRevisionInputs();
            }
            else {
                autoRevealRevision();
            }
        });
    }
}

let extensionImageBatcher = new ImageBatcherClass();
sessionReadyCallbacks.push(() => {
    extensionImageBatcher.register();
});
