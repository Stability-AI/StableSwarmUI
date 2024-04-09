
class SimpleTab {

    constructor() {
        this.tabButton = getRequiredElementById('simpletabbutton');
        this.browser = new GenPageBrowserClass('simpletabbrowserwrapper', this.browserListEntries.bind(this), 'simpletabbrowser', 'Big Thumbnails', this.browserDescribeEntry.bind(this), this.browserSelectEntry.bind(this));
        this.tabButton.addEventListener('click', () => this.browser.navigate(''));
    }

    init() {
    }

    browserDescribeEntry(entry) {
        return comfyDescribeWorkflowForBrowser(entry);
    }

    browserSelectEntry(entry) {
        // TODO
    }

    browserListEntries(path, isRefresh, callback, depth) {
        return comfyListWorkflowsForBrowser(path, isRefresh, callback, depth);
    }
}

let simpleTab = new SimpleTab();

sessionReadyCallbacks.push(() => {
    simpleTab.init();
});
