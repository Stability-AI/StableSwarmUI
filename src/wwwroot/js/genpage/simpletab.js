
class SimpleTab {

    constructor() {
        this.hasLoaded = false;
        this.hasBuilt = false;
        this.containerDiv = null;
        this.tabButton = getRequiredElementById('simpletabbutton');
        this.wrapperDiv = getRequiredElementById('simpletabbrowserwrapper');
        this.browser = new GenPageBrowserClass('simpletabbrowserwrapper', this.browserListEntries.bind(this), 'simpletabbrowser', 'Big Thumbnails', this.browserDescribeEntry.bind(this), this.browserSelectEntry.bind(this), '', 10);
        this.browser.depth = 10;
        this.browser.showDepth = false;
        this.browser.showRefresh = false;
        this.browser.showUpFolder = false;
        this.browser.folderTreeShowFiles = true;
        this.browser.folderSelectedEvent = this.onFolderSelected.bind(this);
        this.browser.builtEvent = this.onBrowserBuilt.bind(this);
        this.tabButton.addEventListener('click', this.onTabClicked.bind(this));
    }

    onFolderSelected() {
        this.browser.fullContentDiv.style.display = 'inline-block';
        this.containerDiv.style.display = 'none';
    }

    onBrowserBuilt() {
        if (this.hasBuilt) {
            return;
        }
        this.containerDiv = createDiv('simplebrowsercontainerdiv', 'browser-content-container');
        this.containerDiv.style.display = 'none';
        this.wrapperDiv.appendChild(this.containerDiv);
        this.hasBuilt = true;
    }

    onTabClicked() {
        if (this.hasLoaded) {
            return;
        }
        this.browser.navigate('');
        this.hasLoaded = true;
    }

    browserDescribeEntry(workflow) {
        let buttons = [];
        return { name: workflow.name, description: `<b>${escapeHtmlNoBr(workflow.name)}</b><br>${escapeHtmlNoBr(workflow.data.description ?? "")}`, image: workflow.data.image, buttons: buttons, className: '', searchable: `${workflow.name}\n${workflow.description}` };
    }

    browserSelectEntry(workflow) {
        this.browser.fullContentDiv.style.display = 'none';
        this.containerDiv.style.display = 'inline-block';
        // TODO
        this.containerDiv.innerHTML = `Wowsa, you selected ${workflow.name}!`;
    }

    browserListEntries(path, isRefresh, callback, depth) {
        genericRequest('ComfyListWorkflows', {}, (data) => {
            let relevant = data.workflows.filter(w => w.enable_in_simple && w.name.startsWith(path));
            let workflowsWithSlashes = relevant.map(w => w.name.substring(path.length)).map(w => w.startsWith('/') ? w.substring(1) : w).filter(w => w.includes('/'));
            let preSlashes = workflowsWithSlashes.map(w => w.substring(0, w.lastIndexOf('/')));
            let fixedFolders = preSlashes.map(w => w.split('/').map((_, i, a) => a.slice(0, i + 1).join('/'))).flat();
            let deduped = [...new Set(fixedFolders)];
            let folders = deduped.sort((a, b) => b.toLowerCase().localeCompare(a.toLowerCase()));
            let mapped = relevant.map(f => {
                return { 'name': f.name, 'data': f };
            });
            callback(folders, mapped);
        });
    }
}

let simpleTab = new SimpleTab();
