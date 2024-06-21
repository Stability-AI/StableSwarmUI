class APIKeyHelper {
    constructor(keyType, prefix) {
        this.keyType = keyType;
        this.keyInput = getRequiredElementById(`${prefix}_api_key`);
        this.keySubmit = getRequiredElementById(`${prefix}_key_submit`);
        this.keyRemove = getRequiredElementById(`${prefix}_key_remove`);
        this.keyStatus = getRequiredElementById(`${prefix}_key_status`);
    }

    onKeyInput() {
        let key = this.keyInput.value;
        this.keySubmit.disabled = !key;
    }

    onSaveButton() {
        let key = this.keyInput.value;
        if (!key) {
            alert('Please enter a key');
            return;
        }
        this.keySubmit.disabled = true;
        genericRequest('SetAPIKey', { keyType: this.keyType, key: key }, data => {
            this.updateStatus();
        });
    }

    onRemoveButton() {
        genericRequest('SetAPIKey', { keyType: this.keyType, key: 'none' }, data => {
            this.updateStatus();
        });
    }

    updateStatus() {
        genericRequest('GetAPIKeyStatus', { keyType: this.keyType }, data => {
            this.keyStatus.innerText = data.status;
            this.keyRemove.disabled = data.status == 'not set';
        });
    }
}

stabilityAPIHelper = new APIKeyHelper('stability_api', 'stability');
civitaiAPIHelper = new APIKeyHelper('civitai_api', 'civitai');

getRequiredElementById('usersettingstabbutton').addEventListener('click', () => {
    stabilityAPIHelper.updateStatus();
    civitaiAPIHelper.updateStatus();
});
