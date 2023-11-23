

/**
 * Hack to attempt to prevent callback recursion.
 * In practice this seems to not work.
 * Either JavaScript itself or Firefox seems to really love tracking the stack and refusing to let go.
 * TODO: Guarantee it actually works so we can't stack overflow from file browsing ffs.
 */
class BrowserCallHelper {
    constructor(path, loadCaller) {
        this.path = path;
        this.loadCaller = loadCaller;
    }
    call() {
        this.loadCaller(this.path);
    }
}

/**
 * Part of a browser tree.
 */
class BrowserTreePart {
    constructor(name, children, hasOpened, isOpen) {
        this.name = name;
        this.children = children;
        this.hasOpened = hasOpened;
        this.isOpen = isOpen;
    }
}

/**
 * Class that handles browsable content sections (eg models list, presets list, etc).
 */
class GenPageBrowserClass {

    constructor(container, listFoldersAndFiles, id, defaultFormat, describe, select) {
        this.container = getRequiredElementById(container);
        this.listFoldersAndFiles = listFoldersAndFiles;
        this.id = id;
        this.format = localStorage.getItem(`browser_${this.id}_format`) || getCookie(`${id}_format`) || defaultFormat; // TODO: Remove the old cookie
        this.describe = describe;
        this.select = select;
        this.folder = '';
        this.navCaller = this.navigate.bind(this);
        this.tree = new BrowserTreePart('', {}, false, true);
        this.depth = localStorage.getItem(`browser_${id}_depth`) || 2;
        this.filter = localStorage.getItem(`browser_${id}_filter`) || '';
    }

    /**
     * Navigates the browser to a given folder path.
     */
    navigate(folder) {
        this.folder = folder;
        this.update();
    }

    /**
     * Refreshes the browser view from source.
     */
    refresh() {
        refreshParameterValues(() => {
            this.update(true);
        });
    }

    /**
     * Updates/refreshes the browser view.
     */
    update(isRefresh = false) {
        if (isRefresh) {
            this.tree = new BrowserTreePart('', {}, false);
        }
        let folder = this.folder;
        this.listFoldersAndFiles(folder, isRefresh, (folders, files) => {
            this.build(folder, folders, files);
        }, this.depth);
    }

    /**
     * Generates the path list span for the current path view, and returns it.
     */
    genPath(path, upButton) {
        let pathGen = createSpan(`${this.id}-path`, 'browser-path');
        if (path == '') {
            upButton.disabled = true;
            return pathGen;
        }
        let partial = '';
        for (let part of ("../" + path).split('/')) {
            partial += part + '/';
            let span = document.createElement('span');
            span.className = 'path-list-part';
            span.innerText = part;
            let route = partial.substring(3, partial.length - 1);
            if (route == '/') {
                route = '';
            }
            let helper = new BrowserCallHelper(route, this.navCaller);
            span.onclick = helper.call.bind(helper);
            pathGen.appendChild(span);
            pathGen.appendChild(document.createTextNode('/'));
        }
        upButton.disabled = false;
        let above = path.split('/').slice(0, -1).join('/');
        let helper = new BrowserCallHelper(above, this.navCaller);
        upButton.onclick = helper.call.bind(helper);
        return pathGen;
    }

    /**
     * Updates tree tracker for the given path.
     */
    refillTree(path, folders) {
        if (path.endsWith('/')) {
            path = path.substring(0, path.length - 1);
        }
        if (path.startsWith('/')) {
            path = path.substring(1);
        }
        let otherFolders = folders.filter(f => f.includes('/'));
        if (otherFolders.length > 0) {
            let baseFolders = folders.filter(f => !f.includes('/'));
            this.refillTree(path, baseFolders);
            while (otherFolders.length > 0) {
                let folder = otherFolders[0];
                let slash = folder.indexOf('/');
                let base = folder.substring(0, slash + 1);
                let same = otherFolders.filter(f => f.startsWith(base)).map(f => f.substring(base.length));
                this.refillTree(`${path}/${base}`, same);
                otherFolders = otherFolders.filter(f => !f.startsWith(base));
            }
            return;
        }
        if (path == '') {
            let copy = Object.assign({}, this.tree.children);
            this.tree.children = {};
            for (let folder of folders) {
                this.tree.children[folder] = copy[folder] || new BrowserTreePart(folder, {}, false, false);
            }
            this.tree.hasOpened = true;
            return;
        }
        let tree = this.tree, parent = this.tree;
        let parts = path.split('/');
        for (let part of parts) {
            parent = tree;
            if (!(part in parent.children)) {
                parent.children[part] = new BrowserTreePart(part, {}, false, false);
            }
            tree = parent.children[part];
        }
        let lastName = parts[parts.length - 1];
        let copy = Object.assign({}, tree.children);
        tree = new BrowserTreePart(lastName, {}, true, tree.isOpen);
        parent.children[lastName] = tree;
        for (let folder of folders) {
            tree.children[folder] = copy[folder] || new BrowserTreePart(folder, {}, false, false);
        }
    }

    /**
     * Builds the element view of the folder tree.
     */
    buildTreeElements(container, path, tree, offset = 16) {
        let span = createSpan(`${this.id}-foldertree-${tree.name}`, 'browser-folder-tree-part');
        span.style.left = `${offset}px`;
        span.innerHTML = `<span class="browser-folder-tree-part-symbol" data-issymbol="true"></span> ${escapeHtml(tree.name || '..')}`;
        span.dataset.path = path;
        container.appendChild(span);
        if (Object.keys(tree.children).length == 0 && tree.hasOpened) {
            // Default: no class
        }
        else if (tree.isOpen) {
            span.classList.add('browser-folder-tree-part-open');
            let subContainer = createDiv(`${this.id}-foldertree-${tree.name}-container`, 'browser-folder-tree-part-container');
            for (let subTree of Object.values(tree.children)) {
                this.buildTreeElements(subContainer, `${path}${subTree.name}/`, subTree, offset + 16);
            }
            container.appendChild(subContainer);
        }
        else {
            span.classList.add('browser-folder-tree-part-closed');
        }
        if (this.folder == path) {
            span.classList.add('browser-folder-tree-part-selected');
        }
        span.onclick = (e) => {
            tree.hasOpened = true;
            if (e.target.dataset.issymbol) {
                tree.isOpen = !tree.isOpen;
            }
            else {
                tree.isOpen = true;
            }
            this.navigate(path);
        };
    }

    /**
     * Fills the container with the content list.
     */
    buildContentList(container, files) {
        let id = 0;
        for (let file of files) {
            id++;
            let desc = this.describe(file);
            if (this.filter && !desc.searchable.toLowerCase().includes(this.filter)) {
                continue;
            }
            let div = createDiv(null, `${desc.className}`);
            let popoverId = `${this.id}-${id}`;
            if (desc.buttons.length > 0) {
                let menuDiv = createDiv(`popover_${popoverId}`, 'sui-popover sui_popover_model');
                for (let button of desc.buttons) {
                    let buttonElem;
                    if (button.href) {
                        buttonElem = document.createElement('a');
                        buttonElem.href = button.href;
                        if (button.is_download) {
                            buttonElem.download = '';
                        }
                    }
                    else {
                        buttonElem = document.createElement('div');
                    }
                    buttonElem.className = 'sui_popover_model_button';
                    buttonElem.innerText = button.label;
                    if (button.onclick) {
                        buttonElem.onclick = () => button.onclick(div);
                    }
                    menuDiv.appendChild(buttonElem);
                }
                container.appendChild(menuDiv);
            }
            let img = document.createElement('img');
            img.addEventListener('click', () => {
                this.select(file);
            });
            div.appendChild(img);
            if (this.format.includes('Cards')) {
                div.className += ' model-block model-block-hoverable';
                if (this.format.startsWith('Small')) { div.classList.add('model-block-small'); }
                else if (this.format.startsWith('Big')) { div.classList.add('model-block-big'); }
                let textBlock = createDiv(null, 'model-descblock');
                textBlock.innerHTML = desc.description;
                div.appendChild(textBlock);
            }
            else if (this.format.includes('Thumbnails')) {
                div.className += ' image-block image-block-legacy';
                let factor = 8;
                if (this.format.startsWith('Big')) { factor = 15; div.classList.add('image-block-big'); }
                else if (this.format.startsWith('Giant')) { factor = 25; div.classList.add('image-block-giant'); }
                else if (this.format.startsWith('Small')) { factor = 5; div.classList.add('image-block-small'); }
                div.style.width = `${factor + 1}rem`;
                img.addEventListener('load', () => {
                    let ratio = img.width / img.height;
                    div.style.width = `${(ratio * factor) + 1}rem`;
                });
                let textBlock = createDiv(null, 'image-preview-text');
                textBlock.innerText = desc.name;
                div.appendChild(textBlock);
            }
            else if (this.format == 'List') {
                div.className += ' browser-list-entry';
                let textBlock = createSpan(null, 'browser-list-entry-text');
                textBlock.innerText = desc.name;
                textBlock.addEventListener('click', () => {
                    this.select(file);
                });
                div.appendChild(textBlock);
            }
            if (desc.buttons.length > 0) {
                let menu = createDiv(null, 'model-block-menu-button');
                menu.innerHTML = '&#x2630;';
                menu.addEventListener('click', () => {
                    doPopover(popoverId);
                });
                div.appendChild(menu);
            }
            div.title = stripHtmlToText(desc.description);
            img.dataset.src = desc.image;
            container.appendChild(div);
        }
    }

    /**
     * Make any visible images within a container actually load now.
     */
    makeVisible(elem) {
        for (let img of elem.getElementsByTagName('img')) {
            if (img.dataset.src && img.getBoundingClientRect().top < window.innerHeight + 256) {
                img.src = img.dataset.src;
                delete img.dataset.src;
            }
        }
    }

    /**
     * Triggers an immediate in-place rerender of the current browser view.
     */
    rerender() {
        if (this.lastPath != null) {
            this.build(this.lastPath, null, this.lastFiles);
        }
    }

    /**
     * Central call to build the browser content area.
     */
    build(path, folders, files) {
        if (path.endsWith('/')) {
            path = path.substring(0, path.length - 1);
        }
        let scrollOffset = 0;
        this.lastPath = path;
        if (folders) {
            this.refillTree(path, folders);
        }
        else if (folders == null && this.contentDiv) {
            scrollOffset = this.contentDiv.scrollTop;
        }
        if (files == null) {
            files = this.lastFiles;
        }
        this.lastFiles = files;
        let folderScroll = this.folderTreeDiv ? this.folderTreeDiv.scrollTop : 0;
        if (!this.hasGenerated) {
            this.hasGenerated = true;
            this.container.innerHTML = '';
            this.folderTreeDiv = createDiv(`${this.id}-foldertree`, 'browser-folder-tree-container');
            let folderTreeSplitter = createDiv(`${this.id}-splitter`, 'browser-folder-tree-splitter splitter-bar');
            this.headerBar = createDiv(`${this.id}-header`, 'browser-header-bar');
            this.fullContentDiv = createDiv(`${this.id}-fullcontent`, 'browser-fullcontent-container');
            this.container.appendChild(this.folderTreeDiv);
            this.container.appendChild(folderTreeSplitter);
            this.container.appendChild(this.fullContentDiv);
            let formatSelector = document.createElement('select');
            formatSelector.id = `${this.id}-format-selector`;
            formatSelector.title = 'Display format';
            formatSelector.className = 'browser-format-selector';
            for (let format of ['Cards', 'Small Cards', 'Big Cards', 'Thumbnails', 'Small Thumbnails', 'Big Thumbnails', 'Giant Thumbnails', 'List']) {
                let option = document.createElement('option');
                option.value = format;
                option.innerText = format;
                if (format == this.format) {
                    option.selected = true;
                }
                formatSelector.appendChild(option);
            }
            formatSelector.addEventListener('change', () => {
                this.format = formatSelector.value;
                localStorage.setItem(`browser_${this.id}_format`, this.format);
                this.update();
            });
            let buttons = createSpan(`${this.id}-button-container`, 'browser-header-buttons', `
                <button id="${this.id}_refresh_button" title="Refresh" class="refresh-button">&#x21BB;</button>
                <button id="${this.id}_up_button" class="refresh-button" disabled autocomplete="off" title="Go back up 1 folder">&#x21d1;</button>
                Depth: <input id="${this.id}_depth_input" class="depth-number-input" type="number" min="1" max="10" value="${this.depth}" title="Depth of subfolders to show" autocomplete="false">
                Filter: <input id="${this.id}_filter_input" type="text" value="${this.filter}" title="Text filter, only show items that contain this text." rows="1" autocomplete="false" placeholder="Filter...">
                `);
            let inputArr = buttons.getElementsByTagName('input');
            let depthInput = inputArr[0];
            depthInput.addEventListener('change', () => {
                this.depth = depthInput.value;
                localStorage.setItem(`browser_${this.id}_depth`, this.depth);
                this.update();
            });
            let filterInput = inputArr[1];
            filterInput.addEventListener('input', () => {
                this.filter = filterInput.value.toLowerCase();
                localStorage.setItem(`browser_${this.id}_filter`, this.filter);
                this.update();
            });
            let buttonArr = buttons.getElementsByTagName('button');
            let refreshButton = buttonArr[0];
            this.upButton = buttonArr[1];
            this.headerBar.appendChild(formatSelector);
            this.headerBar.appendChild(buttons);
            refreshButton.onclick = this.refresh.bind(this);
            this.fullContentDiv.appendChild(this.headerBar);
            this.contentDiv = createDiv(`${this.id}-content`, 'browser-content-container');
            this.contentDiv.addEventListener('scroll', () => {
                this.makeVisible(this.contentDiv);
            });
            this.fullContentDiv.appendChild(this.contentDiv);
            let barSpot;
            let setBar = () => {
                this.folderTreeDiv.style.width = `${barSpot}px`;
                this.fullContentDiv.style.width = `calc(100vw - ${barSpot}px - 2rem)`;
            }
            this.lastReset = () => {
                barSpot = parseInt(localStorage.getItem(`barspot_browser_${this.id}`) || getCookie(`barspot_browser_${this.id}`) || convertRemToPixels(15)); // TODO: Remove the old cookie
                setBar();
            };
            this.lastReset();
            let isDrag = false;
            folderTreeSplitter.addEventListener('mousedown', (e) => {
                isDrag = true;
                e.preventDefault();
            }, true);
            this.lastListen = (e) => {
                let offX = e.pageX;
                offX = Math.min(Math.max(offX, 100), window.innerWidth - 100);
                if (isDrag) {
                    barSpot = offX - 5;
                    localStorage.setItem(`barspot_browser_${this.id}`, barSpot);
                    setBar();
                }
            };
            this.lastListenUp = () => {
                isDrag = false;
            };
            document.addEventListener('mousemove', this.lastListen);
            document.addEventListener('mouseup', this.lastListenUp);
            layoutResets.push(() => {
                localStorage.removeItem(`barspot_browser_${this.id}`);
                this.lastReset()
            });
        }
        else {
            this.folderTreeDiv.innerHTML = '';
            this.contentDiv.innerHTML = '';
            this.headerPath.remove();
        }
        this.headerPath = this.genPath(path, this.upButton);
        this.headerBar.appendChild(this.headerPath);
        this.buildTreeElements(this.folderTreeDiv, '', this.tree);
        this.buildContentList(this.contentDiv, files);
        this.folderTreeDiv.scrollTop = folderScroll;
        this.makeVisible(this.contentDiv);
        if (scrollOffset) {
            this.contentDiv.scrollTop = scrollOffset;
        }
    }
}
