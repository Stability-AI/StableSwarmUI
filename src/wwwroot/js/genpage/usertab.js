
class StabilityAPIHelper {
    constructor() {
        this.keyInput = getRequiredElementById('stability_api_key');
        this.keySubmit = getRequiredElementById('stability_key_submit');
        this.keyRemove = getRequiredElementById('stability_key_remove');
        this.keyStatus = getRequiredElementById('stability_key_status');
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
        genericRequest('SetStabilityAPIKey', { key: key }, data => {
            this.updateStatus();
        });
    }

    onRemoveButton() {
        genericRequest('SetStabilityAPIKey', { key: 'none' }, data => {
            this.updateStatus();
        });
    }

    updateStatus() {
        genericRequest('GetStabilityAPIKeyStatus', {}, data => {
            this.keyStatus.innerText = data.status;
            this.keyRemove.disabled = data.status == 'not set';
        });
    }
}

stabilityAPIHelper = new StabilityAPIHelper();


getRequiredElementById('usersettingstabbutton').addEventListener('click', () => stabilityAPIHelper.updateStatus());
