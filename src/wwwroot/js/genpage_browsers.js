

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

    constructor(container, listFoldersAndFiles, searchFor, id, defaultFormat, describe, select) {
        this.container = getRequiredElementById(container);
        this.listFoldersAndFiles = listFoldersAndFiles;
        this.searchFor = searchFor;
        this.id = id;
        this.format = getCookie(`${id}_format`) || defaultFormat;
        this.describe = describe;
        this.select = select;
        this.folder = '';
        this.navCaller = this.navigate.bind(this);
        this.tree = new BrowserTreePart('', {}, false, true);
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
        });
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
        span.innerHTML = `<span class="browser-folder-tree-part-symbol"></span> ${escapeHtml(tree.name || '..')}`;
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
        span.onclick = () => {
            tree.hasOpened = true;
            tree.isOpen = !tree.isOpen;
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
            let popoverId = `${this.id}-${id}`;
            if (desc.buttons.length > 0) {
                let menuDiv = createDiv(`popover_${popoverId}`, 'sui-popover sui_popover_model');
                for (let button of desc.buttons) {
                    let buttonElem = createDiv(null, 'sui_popover_model_button');
                    buttonElem.innerText = button.label;
                    buttonElem.onclick = button.onclick;
                    menuDiv.appendChild(buttonElem);
                }
                container.appendChild(menuDiv);
            }
            let div = createDiv(null, `${desc.className}`);
            let img = document.createElement('img');
            img.addEventListener('click', () => {
                this.select(file);
            });
            div.appendChild(img);
            if (this.format == 'Cards') {
                div.className += ' model-block model-block-hoverable';
                let textBlock = createDiv(null, 'model-descblock');
                textBlock.innerHTML = desc.description;
                div.appendChild(textBlock);
            }
            else if (this.format == 'Thumbnails') {
                div.className += ' image-block image-block-legacy';
                img.addEventListener('load', () => {
                    let ratio = img.width / img.height;
                    div.style.width = `${(ratio * 8) + 2}rem`;
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
                menu.innerText = '⬤⬤⬤';
                menu.addEventListener('click', () => {
                    doPopover(popoverId);
                });
                div.appendChild(menu);
            }
            div.title = desc.description;
            img.src = desc.image;
            container.appendChild(div);
        }
    }

    /**
     * Central call to build the browser content area.
     */
    build(path, folders, files) {
        if (path.endsWith('/')) {
            path = path.substring(0, path.length - 1);
        }
        this.refillTree(path, folders);
        let folderScroll = this.folderTreeDiv ? this.folderTreeDiv.scrollTop : 0;
        this.container.innerHTML = '';
        this.folderTreeDiv = createDiv(`${this.id}-foldertree`, 'browser-folder-tree-container');
        let folderTreeSplitter = createDiv(`${this.id}-splitter`, 'browser-folder-tree-splitter splitter-bar');
        let headerBar = createDiv(`${this.id}-header`, 'browser-header-bar');
        let fullContentDiv = createDiv(`${this.id}-content`, 'browser-fullcontent-container');
        let contentDiv = createDiv(`${this.id}-content`, 'browser-content-container');
        let formatSelector = document.createElement('select');
        formatSelector.id = `${this.id}-format-selector`;
        formatSelector.className = 'browser-format-selector';
        for (let format of ['Cards', 'Thumbnails', 'List']) {
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
            setCookie(`${this.id}_format`, this.format, 365);
            this.update();
        });
        let buttons = createSpan(`${this.id}-button-container`, 'browser-header-buttons', `
            <button id="${this.id}_refresh_button" title="Refresh" class="refresh-button">&#128472;</button>
            <button id="${this.id}_up_button" class="refresh-button" disabled autocomplete="off" title="Go back up 1 folder">&#x21d1;</button>
            `);
        let buttonArr = buttons.getElementsByTagName('button');
        let refreshButton = buttonArr[0];
        let upButton = buttonArr[1];
        headerBar.appendChild(formatSelector);
        headerBar.appendChild(buttons);
        headerBar.appendChild(this.genPath(path, upButton));
        refreshButton.onclick = this.refresh.bind(this);
        fullContentDiv.appendChild(headerBar);
        this.buildTreeElements(this.folderTreeDiv, '', this.tree);
        this.buildContentList(contentDiv, files);
        this.container.appendChild(this.folderTreeDiv);
        this.container.appendChild(folderTreeSplitter);
        fullContentDiv.appendChild(contentDiv);
        this.container.appendChild(fullContentDiv);
        if (this.lastListenMove) {
            document.removeEventListener('mousemove', this.lastListenMove);
            document.removeEventListener('mouseup', this.lastListenUp);
            layoutResets.slice(layoutResets.indexOf(this.lastReset), 1);
        }
        let barSpot;
        let selfRef = this;
        function setBar() {
            selfRef.folderTreeDiv.style.width = `${barSpot}px`;
            fullContentDiv.style.width = `calc(100vw - ${barSpot}px - 1rem)`;
        }
        this.lastReset = () => {
            barSpot = parseInt(getCookie(`barspot_browser_${this.id}`) || convertRemToPixels(15));
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
                setCookie(`barspot_browser_${this.id}`, barSpot, 365);
                setBar();
            }
        };
        this.lastListenUp = (e) => {
            isDrag = false;
        };
        document.addEventListener('mousemove', this.lastListen);
        document.addEventListener('mouseup', this.lastListenUp);
        layoutResets.push(this.lastReset);
        this.folderTreeDiv.scrollTop = folderScroll;
    }
}
