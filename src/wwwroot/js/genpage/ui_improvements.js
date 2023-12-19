
class AdvancedPopover {
    /**
     * eg: new AdvancedPopover('my_popover', [ { key: 'Button 1', action: () => console.log("Clicked!") } ], true, mouseX, mouseY, 'Button 1');
     * Buttons can optionally exclude action to make unclickable.
     */
    constructor(id, buttons, canSearch, x, y, preSelect = null, flipYHeight = null) {
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
        this.optionArea = createDiv(null, 'sui_popover_scrollable');
        this.buildList();
        this.popover.appendChild(this.optionArea);
        document.body.appendChild(this.popover);
        this.show(x, y);
        if (canSearch) {
            this.textInput.focus();
        }
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
    }

    selected() {
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

    show(targetX, targetY) {
        if (this.popover.dataset.visible == "true") {
            this.hide();
        }
        this.popover.classList.add('sui-popover-visible');
        this.popover.style.width = '200px';
        this.popover.dataset.visible = "true";
        let x = Math.min(targetX, window.innerWidth - this.popover.offsetWidth - 10);
        let y = Math.min(targetY, window.innerHeight - this.popover.offsetHeight);
        if (this.flipYHeight && targetY + this.popover.offsetHeight > window.innerHeight) {
            y = Math.max(0, targetY - this.flipYHeight - this.popover.offsetHeight);
        }
        this.popover.style.left = `${x}px`;
        this.popover.style.top = `${y}px`;
        this.popover.style.width = '';
        popHide.push(this);
    }
}

class UIImprovementHandler {
    constructor() {
        this.lastPopover = null;
        document.addEventListener('mousedown', (e) => {
            if (e.target.tagName == 'SELECT') {
                return this.onSelectClicked(e.target, e);
            }
        }, true);
        document.addEventListener('click', (e) => {
            if (e.target.tagName == 'SELECT' && !e.shiftKey) {
                e.preventDefault();
                e.stopPropagation();
                return false;
            }
        }, true);
        document.addEventListener('mouseup', (e) => {
            if (e.target.tagName == 'SELECT' && !e.shiftKey) {
                e.preventDefault();
                e.stopPropagation();
                return false;
            }
        }, true);
    }

    onSelectClicked(elem, e) {
        if (this.lastPopover) {
            this.lastPopover.remove();
            this.lastPopover = null;
        }
        if (e.shiftKey) {
            return true;
        }
        let popId = `uiimprover_${elem.id}`;
        let rect = elem.getBoundingClientRect();
        let buttons = [...elem.options].map(o => { return { key: o.innerText, action: () => { elem.value = o.value; triggerChangeFor(elem); } }; })
        this.lastPopover = new AdvancedPopover(popId, buttons, true, rect.x, rect.y, elem.value);
        e.preventDefault();
        e.stopPropagation();
        return false;
    }
}

uiImprover = new UIImprovementHandler();
