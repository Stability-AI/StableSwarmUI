
class InstallerClass {
    parts = ['license', 'themes', 'installed_for', 'backends', 'models', 'end'];
    backButton = getRequiredElementById('installer_button_back');
    nextButton = getRequiredElementById('installer_button_next');
    bottomInfo = getRequiredElementById('bottom_info');

    constructor() {
        this.cur_part = 0;
        this.backButton.addEventListener('click', this.back.bind(this));
        this.nextButton.addEventListener('click', this.next.bind(this));
        this.moveToPage(0);
        for (let elem of getRequiredElementById('theme_selection_field').getElementsByClassName('form-check')) {
            let radio = elem.getElementsByTagName('input')[0];
            elem.addEventListener('click', () => {
                radio.click();
            });
            radio.addEventListener('change', this.themeChanged.bind(this));
        }
        for (let elem of document.getElementsByTagName('fieldset')) {
            elem.addEventListener('change', this.check.bind(this));
        }
        getRequiredElementById('stability_api_key').addEventListener('input', this.check.bind(this));
        getRequiredElementById('installer_button_confirm').addEventListener('click', this.submit.bind(this));
        getSession();
    }

    themeChanged() {
        let theme = getRadioSelectionInFieldset('theme_selection_field');
        let path = `/css/themes/${theme}.css`;
        let isDark = theme != 'eyesear_white';
        getRequiredElementById('theme_sheet_header').href = path;
        getRequiredElementById('bs_theme_header').href = isDark ? '/css/bootstrap.min.css' : '/css/bootstrap_light.min.css';
    }

    modelsToDownload() {
        let models = [];
        for (let elem of getRequiredElementById('models_fieldset').getElementsByTagName('input')) {
            if (elem.checked && elem.id.startsWith('downloadmodel_')) {
                models.push(elem.id.substring('downloadmodel_'.length));
            }
        }
        return models;
    }

    getPageElem() {
        return getRequiredElementById(`installer_section_${this.parts[this.cur_part]}`);
    }

    next() {
        this.moveToPage(this.cur_part + 1);
    }

    back() {
        this.moveToPage(this.cur_part - 1);
    }

    isPageComplete() {
        switch (this.parts[this.cur_part]) {
            case 'license':
                return true;
            case 'themes':
                return getRadioSelectionInFieldset('theme_selection_field') != null;
            case 'installed_for':
                return getRadioSelectionInFieldset('installed_for_selection_field') != null;
            case 'backends':
                let backend = getRadioSelectionInFieldset('backend_selection_field');
                if (backend == null) {
                    return false;
                }
                if (backend == 'stabilityapi') {
                    return getRequiredElementById('stability_api_key').value != '';
                }
                return true;
            case 'models':
                return true;
            case 'end':
                return true;
        }
    }

    moveToPage(page) {
        this.getPageElem().style.display = 'none';
        this.cur_part = Math.min(this.parts.length - 1, Math.max(0, page));
        this.getPageElem().style.display = 'block';
        this.check();
    }

    check() {
        let isComplete = this.isPageComplete();
        this.backButton.disabled = this.cur_part == 0;
        this.nextButton.innerText = this.cur_part == 0 ? "Agree" : "Next";
        this.nextButton.disabled = this.cur_part == this.parts.length - 1 || !isComplete;
        this.bottomInfo.innerText = `Step ${this.cur_part + 1} of ${this.parts.length}`;
        if (!isComplete) {
            this.bottomInfo.innerText += ' (Must choose an option)';
        }
        let data = this.getSubmittable();
        getRequiredElementById('theme_val_info').innerText = data.theme;
        getRequiredElementById('installed_for_val_info').innerText = data.installed_for;
        getRequiredElementById('backend_val_info').innerText = data.backend;
        getRequiredElementById('model_val_info').innerText = data.models;
    }

    getSubmittable() {
        let models = this.modelsToDownload();
        return {
            theme: getRadioSelectionInFieldset('theme_selection_field'),
            installed_for: getRadioSelectionInFieldset('installed_for_selection_field'),
            backend: getRadioSelectionInFieldset('backend_selection_field'),
            stability_api_key: getRequiredElementById('stability_api_key').value,
            models: models.length == 0 ? 'none' : this.modelsToDownload().join(', ')
        };
    }

    submit() {
        let output = getRequiredElementById('install_output');
        let progress = getRequiredElementById('install_progress_spot');
        output.innerText = 'Sending...\n';
        let data = this.getSubmittable();
        getRequiredElementById('installer_button_confirm').disabled = true;
        makeWSRequest('InstallConfirmWS', data, (response) => {
            if (response.info) {
                output.innerText += response.info + "\n";
            }
            else if (response.progress) {
                if (response.progress == 0) {
                    progress.style.display = 'none';
                }
                else {
                    progress.style.display = 'block';
                    progress.innerText = `Progress: ${fileSizeStringify(response.progress)}`;
                }
            }
            else if (response.success) {
                window.location.href = '/Text2Image';
            }
        }, 0, (e) => {
            getRequiredElementById('installer_button_confirm').disabled = false;
            showError(e);
        });
    }
}

let installer = new InstallerClass();
