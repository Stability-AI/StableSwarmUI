
class StabilityAPIHelper {
    onKeyInput() {
        let key = getRequiredElementById('stability_api_key').value;
        getRequiredElementById('stability_key_submit').disabled = !key;
    }

    onSaveButton() {
        let key = getRequiredElementById('stability_api_key').value;
        if (!key) {
            alert('Please enter a key');
            return;
        }
        getRequiredElementById('stability_key_submit').disabled = true;
        genericRequest('SetStabilityAPIKey', { key: key }, data => {
            this.updateStatus();
        });
    }

    updateStatus() {
        genericRequest('GetStabilityAPIKeyStatus', {}, data => {
            getRequiredElementById('stability_key_status').innerText = data.status;
        });
    }
}

stabilityAPIHelper = new StabilityAPIHelper();


getRequiredElementById('usersettingstabbutton').addEventListener('click', () => stabilityAPIHelper.updateStatus());
