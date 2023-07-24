
class InstallerClass {
    parts = ['themes', 'installed_for', 'backends', 'models', 'end'];
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
    }

    themeChanged() {
        let theme = getRadioSelectionInFieldset('theme_selection_field');
        let path = `/css/themes/${theme}.css`;
        let isDark = theme != 'eyesear_white';
        getRequiredElementById('theme_sheet_header').href = path;
        getRequiredElementById('bs_theme_header').href = isDark ? '/css/bootstrap.min.css' : '/css/bootstrap_light.min.css';
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
                return true; // TODO
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
        this.nextButton.disabled = this.cur_part == this.parts.length - 1 || !isComplete;
        this.bottomInfo.innerText = `Step ${this.cur_part + 1} of ${this.parts.length}`;
        if (!isComplete) {
            this.bottomInfo.innerText += ' (Must choose an option)';
        }
    }
}

let installer = new InstallerClass();
