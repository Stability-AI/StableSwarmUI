
let mouseX, mouseY;
let popHide = [];
let lastPopoverTime = 0, lastPopover = null;

class AdvancedPopover {
    /**
     * eg: new AdvancedPopover('my_popover', [ { key: 'Button 1', action: () => console.log("Clicked!") } ], true, mouseX, mouseY, 'Button 1', document.body);
     * Buttons can optionally exclude action to make unclickable.
     */
    constructor(id, buttons, canSearch, x, y, root, preSelect = null, flipYHeight = null) {
        this.id = id;
        this.buttons = buttons;
        this.popover = createDiv(`popover_${id}`, 'sui-popover sui_popover_model sui-popover-notransition');
        this.textInput = null;
        this.flipYHeight = flipYHeight;
        this.preSelect = preSelect;
        if (canSearch) {
            this.textInput = document.createElement('input');
            this.textInput.type = 'text';
            this.textInput.classList.add('sui_popover_text_input');
            this.textInput.value = '';
            this.textInput.placeholder = 'Search...';
            this.textInput.addEventListener('input', (e) => {
                this.buildList();
            });
            this.textInput.addEventListener('keydown', (e) => {
                this.onKeyDown(e);
            });
            this.popover.appendChild(this.textInput);
        }
        this.optionArea = createDiv(null, 'sui_popover_scrollable_tall');
        this.expectedHeight = 0;
        this.targetY = null;
        this.blockHeight = parseFloat(getComputedStyle(document.documentElement).fontSize) * 1.3;
        this.buildList();
        this.popover.appendChild(this.optionArea);
        root.appendChild(this.popover);
        this.show(x, y);
        if (canSearch) {
            this.textInput.focus();
        }
        this.created = Date.now();
    }

    remove() {
        if (this.popover) {
            this.hide();
            this.popover.remove();
            this.popover = null;
        }
    }

    buildList() {
        let selectedElem = this.selected();
        let selected = this.preSelect ? this.preSelect : selectedElem ? selectedElem.innerText : null;
        let scroll = this.optionArea.scrollTop;
        this.optionArea.innerHTML = '';
        let searchText = this.textInput ? this.textInput.value.toLowerCase() : '';
        let didSelect = false;
        this.expectedHeight = 0;
        for (let button of this.buttons) {
            if (button.key.toLowerCase().includes(searchText)) {
                let optionDiv = document.createElement('div');
                optionDiv.classList.add('sui_popover_model_button');
                optionDiv.innerText = button.key;
                if (button.key == selected) {
                    optionDiv.classList.add('sui_popover_model_button_selected');
                    didSelect = true;
                }
                if (!button.action) {
                    optionDiv.classList.add('sui_popover_model_button_disabled');
                }
                else {
                    optionDiv.addEventListener('click', () => {
                        button.action();
                        this.remove();
                    });
                }
                this.optionArea.appendChild(optionDiv);
                this.expectedHeight += this.blockHeight;
            }
        }
        if (!didSelect) {
            let selected = this.optionArea.querySelector('.sui_popover_model_button');
            if (selected) {
                selected.classList.add('sui_popover_model_button_selected');
            }
        }
        this.optionArea.scrollTop = scroll;
        this.scrollFix();
        if (this.targetY != null) {
            this.reposition();
        }
    }

    selected() {
        if (!this.popover) {
            return [];
        }
        return this.popover.getElementsByClassName('sui_popover_model_button_selected')[0];
    }

    scrollFix() {
        let selected = this.selected();
        if (!selected) {
            return;
        }
        if (selected.offsetTop + selected.offsetHeight > this.optionArea.scrollTop + this.optionArea.offsetHeight) {
            this.optionArea.scrollTop = selected.offsetTop + selected.offsetHeight - this.optionArea.offsetHeight + 6;
        }
        else if (selected.offsetTop < this.optionArea.scrollTop) {
            this.optionArea.scrollTop = selected.offsetTop;
        }
    }

    possible() {
        if (!this.popover) {
            return [];
        }
        return [...this.popover.getElementsByClassName('sui_popover_model_button')].filter(e => !e.classList.contains('sui_popover_model_button_disabled'));
    }

    onKeyDown(e) {
        if (e.shiftKey || e.ctrlKey) {
            return;
        }
        let possible = this.possible();
        if (!possible) {
            return;
        }
        if (e.key == 'Escape') {
            this.remove();
        }
        else if (e.key == 'Tab' || e.key == 'Enter') {
            let selected = this.popover.querySelector('.sui_popover_model_button_selected');
            if (selected) {
                selected.click();
            }
            e.preventDefault();
            return false;
        }
        else if (e.key == 'ArrowUp') {
            let selectedIndex = possible.findIndex(e => e.classList.contains('sui_popover_model_button_selected'));
            possible[selectedIndex].classList.remove('sui_popover_model_button_selected');
            possible[(selectedIndex + possible.length - 1) % possible.length].classList.add('sui_popover_model_button_selected');
            this.scrollFix();
        }
        else if (e.key == 'ArrowDown') {
            let selectedIndex = possible.findIndex(e => e.classList.contains('sui_popover_model_button_selected'));
            possible[selectedIndex].classList.remove('sui_popover_model_button_selected');
            possible[(selectedIndex + 1) % possible.length].classList.add('sui_popover_model_button_selected');
            this.scrollFix();
        }
        else {
            return;
        }
        e.preventDefault();
        return false;
    }

    hide() {
        if (this.popover.dataset.visible == "true") {
            this.popover.classList.remove('sui-popover-visible');
            this.popover.dataset.visible = "false";
            popHide.splice(popHide.indexOf(this), 1);
        }
    }

    reposition() {
        if (this.popover.classList.contains('sui_popover_reverse')) {
            this.popover.classList.remove('sui_popover_reverse');
        }
        let y;
        let maxHeight;
        let extraHeight = (this.textInput ? this.textInput.offsetHeight : 0) + 32;
        let expected = this.expectedHeight + extraHeight;
        if (this.targetY + expected < window.innerHeight) {
            y = this.targetY;
            maxHeight = this.expectedHeight;
        }
        else if (this.flipYHeight != null && this.targetY > window.innerHeight / 2) {
            y = Math.max(0, this.targetY - this.flipYHeight - expected);
            this.popover.classList.add('sui_popover_reverse');
            maxHeight = this.targetY - this.flipYHeight - 32;
        }
        else {
            y = this.targetY;
            maxHeight = window.innerHeight - y - extraHeight - 10;
        }
        this.popover.style.top = `${y}px`;
        this.optionArea.style.maxHeight = `${maxHeight}px`;
    }

    show(targetX, targetY) {
        this.targetY = targetY;
        if (this.popover.dataset.visible == "true") {
            this.hide();
        }
        this.popover.classList.add('sui-popover-visible');
        this.popover.style.width = '200px';
        this.popover.dataset.visible = "true";
        let x = Math.min(targetX, window.innerWidth - this.popover.offsetWidth - 10);
        let y = Math.min(targetY, window.innerHeight - this.popover.offsetHeight);
        this.popover.style.left = `${x}px`;
        this.popover.style.top = `${y}px`;
        this.popover.style.width = '';
        this.reposition();
        popHide.push(this);
        lastPopoverTime = Date.now();
        lastPopover = this;
    }
}

class UIImprovementHandler {
    constructor() {
        this.lastPopover = null;
        let lastShift = false;
        document.addEventListener('mousedown', (e) => {
            if (e.target.tagName == 'SELECT') {
                lastShift = e.shiftKey;
                if (!lastShift && e.target.options.length > 5) {
                    e.preventDefault();
                    e.stopPropagation();
                    return false;
                }
            }
        }, true);
        document.addEventListener('click', (e) => {
            if (e.target.tagName == 'SELECT' && !lastShift && e.target.options.length > 5) { // e.shiftKey doesn't work in click for some reason
                return this.onSelectClicked(e.target, e);
            }
        }, true);
        document.addEventListener('mouseup', (e) => {
            if (e.target.tagName == 'SELECT' && !e.shiftKey && e.target.options.length > 5) {
                e.preventDefault();
                e.stopPropagation();
                return false;
            }
        }, true);
    }

    onSelectClicked(elem, e) {
        if (this.lastPopover && this.lastPopover.popover) {
            this.lastPopover.remove();
            this.lastPopover = null;
            e.preventDefault();
            e.stopPropagation();
            return false;
        }
        let popId = `uiimprover_${elem.id}`;
        let rect = elem.getBoundingClientRect();
        let buttons = [...elem.options].map(o => { return { key: o.innerText, action: () => { elem.value = o.value; triggerChangeFor(elem); } }; })
        this.lastPopover = new AdvancedPopover(popId, buttons, true, rect.x, rect.y, elem.parentElement, elem.selectedIndex < 0 ? null : elem.selectedOptions[0].innerText, 0);
        e.preventDefault();
        e.stopPropagation();
        return false;
    }
}

uiImprover = new UIImprovementHandler();

///////////// Older-style popover code, to be cleaned

function doPopHideCleanup(target) {
    for (let x = 0; x < popHide.length; x++) {
        let id = popHide[x];
        let pop = id.popover ? id.popover : getRequiredElementById(`popover_${id}`);
        if (id == lastPopover && Date.now() - lastPopoverTime < 50) {
            continue;
        }
        if (pop.contains(target) && !target.classList.contains('sui_popover_model_button')) {
            continue;
        }
        if (id instanceof AdvancedPopover) {
            if (Date.now() - id.created > 50) {
                id.remove();
            }
        }
        else {
            pop.classList.remove('sui-popover-visible');
            pop.dataset.visible = "false";
            popHide.splice(x, 1);
        }
    }
}

document.addEventListener('mousedown', (e) => {
    mouseX = e.pageX;
    mouseY = e.pageY;
    if (e.button == 2) { // right-click
        doPopHideCleanup(e.target);
    }
}, true);

document.addEventListener('click', (e) => {
    if (e.target.tagName == 'BODY') {
        return; // it's impossible on the genpage to actually click body, so this indicates a bugged click, so ignore it
    }
    doPopHideCleanup(e.target);
    if (getRequiredElementById('image_fullview_modal').style.display == 'block' && !findParentOfClass(e.target, 'imageview_popup_modal_undertext')) {
        closeImageFullview();
        e.preventDefault();
        e.stopPropagation();
        return false;
    }
}, true);

/** Ensures the popover for the given ID is hidden. */
function hidePopover(id) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        pop.classList.remove('sui-popover-visible');
        pop.dataset.visible = "false";
        popHide.splice(popHide.indexOf(id), 1);
    }
}

/** Shows the given popover, optionally at the specified location. */
function showPopover(id, targetX = mouseX, targetY = mouseY) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        hidePopover(id); // Hide to reset before showing again.
    }
    pop.classList.add('sui-popover-visible');
    pop.style.width = '200px';
    pop.dataset.visible = "true";
    let x = Math.min(targetX, window.innerWidth - pop.offsetWidth - 10);
    let y = Math.min(targetY, window.innerHeight - pop.offsetHeight);
    pop.style.left = `${x}px`;
    pop.style.top = `${y}px`;
    pop.style.width = '';
    popHide.push(id);
    lastPopoverTime = Date.now();
    lastPopover = id;
}

/** Toggles the given popover, showing it or hiding it as relevant. */
function doPopover(id, e) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        hidePopover(id);
    }
    else {
        showPopover(id);
    }
    
    if (e) {
        e.preventDefault();
        e.stopImmediatePropagation();
    }
}
