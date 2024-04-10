
class SimpleTab {

    constructor() {
        this.tabButton = getRequiredElementById('simpletabbutton');
        this.browser = new GenPageBrowserClass('simpletabbrowserwrapper', this.browserListEntries.bind(this), 'simpletabbrowser', 'Big Thumbnails', this.browserDescribeEntry.bind(this), this.browserSelectEntry.bind(this));
        this.tabButton.addEventListener('click', () => this.browser.navigate(''));
    }

    init() {
    }

    browserDescribeEntry(workflow) {
        let buttons = [];
        return { name: workflow.name, description: `<b>${escapeHtmlNoBr(workflow.name)}</b><br>${escapeHtmlNoBr(workflow.data.description ?? "")}`, image: workflow.data.image, buttons: buttons, className: '', searchable: `${workflow.name}\n${workflow.description}` };
    }

    browserSelectEntry(workflow) {
        // TODO
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
