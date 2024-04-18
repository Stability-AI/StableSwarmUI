
class InstallerClass {
    parts = ['license', 'skip', 'themes', 'installed_for', 'backends', 'models', 'end'];
    backButton = getRequiredElementById('installer_button_back');
    nextButton = getRequiredElementById('installer_button_next');
    bottomInfo = getRequiredElementById('bottom_info');

    constructor() {
        let amdPart = document.getElementById('installer_section_amd');
        if (amdPart) {
            this.parts.splice(1, 0, 'amd');
        }
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
        getSession(() => {
            language = language || 'en';
            loadAndApplyTranslations();
        });
    }

    themeChanged() {
        let theme = getRadioSelectionInFieldset('theme_selection_field');
        let css_paths = [`/css/themes/${theme}.css`];
        let isDark = !['eyesear_white', 'modern_light'].includes(theme);
        if (theme.startsWith('modern')) {
            css_paths.unshift('/css/themes/modern.css');
        }
        let themeCss = document.head.querySelectorAll('.theme_sheet_header');
        let oldPaths = Array.from(themeCss).map(x => x.href.split('?')[0]);
        if (css_paths.every(x => oldPaths.some(o => o.endsWith(x)))) {
            return;
        }
        let siteHeader = getRequiredElementById('sitecssheader');
        getRequiredElementById('bs_theme_header').href = isDark ? '/css/bootstrap.min.css' : '/css/bootstrap_light.min.css';
        themeCss.forEach(x => x.remove());
        let newTheme = css_paths.map(path => `<link class="theme_sheet_header" rel="stylesheet" href="${path}?${siteHeader.href.split('?')[1]}" />`).join('\n');
        document.head.insertAdjacentHTML('beforeend', newTheme);
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
        if (this.parts[this.cur_part] == 'skip') {
            let skip = getRadioSelectionInFieldset('install_path_selection_field');
            if (skip == 'just_install') {
                this.moveToPage(this.parts.length - 1);
                return;
            }
        }
        this.moveToPage(this.cur_part + 1);
    }

    back() {
        this.moveToPage(this.cur_part - 1);
    }

    isPageComplete() {
        switch (this.parts[this.cur_part]) {
            case 'license':
                return true;
            case 'amd':
                return getRadioSelectionInFieldset('amd_selection_field') != null;
            case 'skip':
                return getRadioSelectionInFieldset('install_path_selection_field') != null;
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
        let amd_section = document.getElementById('amd_selection_field');
        let install_amd = false;
        if (amd_section) {
            install_amd = getRadioSelectionInFieldset('amd_selection_field') == 'yes';
        }
        let models = this.modelsToDownload();
        return {
            theme: getRadioSelectionInFieldset('theme_selection_field'),
            installed_for: getRadioSelectionInFieldset('installed_for_selection_field'),
            backend: getRadioSelectionInFieldset('backend_selection_field'),
            stability_api_key: getRequiredElementById('stability_api_key').value,
            models: models.length == 0 ? 'none' : this.modelsToDownload().join(', '),
            language: document.getElementById('installer_language').value,
            install_amd: install_amd
        };
    }

    submit() {
        let output = getRequiredElementById('install_output');
        let progress = getRequiredElementById('install_progress_spot');
        let bar = getRequiredElementById('install_progress_bar');
        let stepBar = getRequiredElementById('install_progress_step_bar');
        output.innerText = 'Sending...\n';
        let data = this.getSubmittable();
        getRequiredElementById('installer_button_confirm').disabled = true;
        let timeLastRestart = 0;
        makeWSRequest('InstallConfirmWS', data, (response) => {
            if (response.info) {
                output.innerText += response.info + "\n";
            }
            else if ('progress' in response) {
                progress.style.display = 'block';
                if (response.progress == 0) {
                    progress.innerText = `Step ${response.steps} / ${response.total_steps}`;
                    bar.style.width = '0%';
                    timeLastRestart = Date.now();
                }
                else {
                    if (timeLastRestart == 0) {
                        timeLastRestart = Date.now();
                    }
                    let perSecond = response.progress / (Date.now() - timeLastRestart) * 1000;
                    let percent = response.total == 0 ? 0 : (Math.round(response.progress / response.total * 10000) / 100);
                    progress.innerText = `Progress: ${fileSizeStringify(response.progress)} / ${fileSizeStringify(response.total)} (${percent}%) (Step ${response.steps} / ${response.total_steps}) ... ${fileSizeStringify(perSecond)}/s`;
                    bar.style.width = `${percent}%`;
                }
                let stepPercent = Math.round(response.steps / response.total_steps * 10000) / 100;
                stepBar.style.width = `${stepPercent}%`;
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
