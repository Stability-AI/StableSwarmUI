
class ApiKeyType {
    constructor(apiName) {
        this.apiName = apiName.toLowerCase();
    }

    /** Gets the name in a 'lowercase' format */
    get nameForForm() {
        return this.apiName;
    }

    /** Gets the name in a 'Capitalized' format */
    get nameForEndpoint() {
        return this.apiName.charAt(0).toUpperCase() + this.apiName.slice(1);
    }
}

class APIKeyHelper {
    constructor(apiKeyType) {
        this.apiKeyType = apiKeyType;
        this.keyInput = getRequiredElementById(this.apiKeyType.nameForForm + '_api_key');
        this.keySubmit = getRequiredElementById(this.apiKeyType.nameForForm + '_key_submit');
        this.keyRemove = getRequiredElementById(this.apiKeyType.nameForForm + '_key_remove');
        this.keyStatus = getRequiredElementById(this.apiKeyType.nameForForm + '_key_status');
    }

    onKeyInput() {
        let key = this.keyInput.value;
        this.keySubmit.disabled = !key;
    }

    onSaveButton() {
        let newApikey = this.keyInput.value;
        if (!newApikey) {
            alert('Please enter a key');
            return;
        }
        this.keySubmit.disabled = true;
        genericRequest('Set' + this.apiKeyType.nameForEndpoint + 'APIKey', { key: newApikey }, data => {
            // If we successfully wrote the API key, remove the value from the form immediately to hide it.
            if ("success" in data && data.success === true) {
                this.keyInput.value = "";
                this.keyStatus.innerText = "Updated!";
                this.keyRemove.disabled = false;
            } else {
                this.updateStatus();
            }
        });
    }

    onRemoveButton() {
        genericRequest('Set' + this.apiKeyType.nameForEndpoint + 'APIKey', { key: null }, data => {
            // If we successfully cleared our API key, disable the remove button
            if ("success" in data && data.success === true) {
                this.keyStatus.innerText = "(Unset)";
                this.keyRemove.disabled = true;
            } else {
                this.updateStatus();
            }
        });
    }

    updateStatus() {
        genericRequest('Get' + this.apiKeyType.nameForEndpoint + 'APIKeyStatus', {}, data => {
            this.keyStatus.innerText = data.status;
            this.keyRemove.disabled = data.status == '(Unset)' || data.status == '(Unknown)';
        });
    }
}

stabilityAPIHelper = new APIKeyHelper(new ApiKeyType('stability'));
civitaiAPIHelper = new APIKeyHelper(new ApiKeyType('civitai'));

getRequiredElementById('usersettingstabbutton').addEventListener('click', () => {
    stabilityAPIHelper.updateStatus();
    civitaiAPIHelper.updateStatus();
});
