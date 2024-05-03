

class ServerLogsHelper {
    constructor() {
        this.logTypes = [];
        this.loaded = false;
        this.tabButton = getRequiredElementById('logtabbutton');
        this.tabButton.addEventListener('click', () => this.onTabButtonClick());
        this.tabBody = getRequiredElementById('Server-Logs');
        this.serverTabBody = getRequiredElementById('server_tab');
        this.typeSelectors = getRequiredElementById('server_log_type_selector');
        this.actualLogContainer = getRequiredElementById('server_logs_container');
        this.filterInput = getRequiredElementById('server_log_filter');
        this.lastSeq = -1;
        this.logMessagesByType = {};
        this.lastBounce = 0;
        this.levels = ['Verbose', 'Debug', 'Info', 'Init', 'Warning', 'Error'];
    }

    regenTypeListElem() {
        let names = this.logTypes.map((t) => t.name);
        if (arraysEqual(this.lastLogTypes || [], names)) {
            return;
        }
        let html = '';
        let selected = this.typeSelectors.value || 'Info';
        for (let type of this.logTypes) {
            html += `<option>${type.name}</option>`;
        }
        this.typeSelectors.innerHTML = html;
        this.typeSelectors.value = selected;
        this.lastLogTypes = names;
    }

    loadTypeList(callback) {
        genericRequest('ListLogTypes', {}, (data) => {
            this.logTypes = data.types_available;
            this.regenTypeListElem();
            callback();
        });
    }

    onTabButtonClick() {
        if (!this.loaded) {
            this.loadTypeList(() => {
                setInterval(() => this.updateLoop(), 1000);
                this.loaded = true;
            });
        }
    }

    htmlMessage(msg, type, bounceId) {
        return `<div class="log_message log_message_${bounceId}"><span class="log_message_prefix">${msg.time} [<span style="color:${type.color}">${type.name}</span>]</span> ${escapeHtmlNoBr(msg.message)}</div>`;
    }

    getVisibleTypes() {
        let selected = this.typeSelectors.value;
        if (selected == null) {
            return [];
        }
        if (!this.levels.includes(selected)) {
            return [selected];
        }
        return this.levels.slice(this.levels.indexOf(selected));
    }

    matchIdentifier(identifier) {
        let matched = this.logTypes.filter((t) => t.identifier == identifier);
        if (matched.length == 0) {
            return null;
        }
        return matched[0];
    }

    showLogsForIdentifier(identifier) {
        let matched = this.matchIdentifier(identifier);
        if (!matched) {
            return;
        }
        this.typeSelectors.value = matched.name;
        getRequiredElementById('servertabbutton').click();
        this.tabButton.click();
        this.updateLoop();
    }

    updateLoop() {
        if (!this.serverTabBody.classList.contains('active') || !this.tabBody.classList.contains('active')) {
            return;
        }
        this.actualLogContainer.style.height = `calc(100vh - ${this.actualLogContainer.offsetTop}px - 10px)`;
        let lastSeqs = {};
        for (let type of this.logTypes) {
            let data = this.logMessagesByType[type.name];
            if (data) {
                lastSeqs[type.name] = data.last_seq_id;
            }
        }
        let filter = this.filterInput.value.toLowerCase();
        let selected = this.typeSelectors.value;
        let visibleTypes = this.getVisibleTypes();
        if (selected != this.lastVisibleType || filter != this.lastFilter) {
            this.lastVisibleType = selected;
            this.lastFilter = filter;
            this.actualLogContainer.innerHTML = '';
            let toRenderMessages = [];
            for (let typeName of visibleTypes) {
                let storedData = this.logMessagesByType[typeName];
                if (!storedData) {
                    continue;
                }
                let type = this.logTypes.find((t) => t.name == typeName);
                for (let message of Object.values(storedData.raw)) {
                    if (!filter || message.message.toLowerCase().includes(filter)) {
                        toRenderMessages.push([message, type]);
                    }
                }
            }
            toRenderMessages.sort((a, b) => a[0].sequence_id - b[0].sequence_id);
            for (let [message, type] of toRenderMessages) {
                this.actualLogContainer.innerHTML += this.htmlMessage(message, type, this.lastBounce);
                this.lastBounce = (this.lastBounce + 1) % 2;
            }
        }
        genericRequest('ListRecentLogMessages', { lastSeqId: this.lastSeq, types: visibleTypes, last_sequence_ids: lastSeqs }, (data) => {
            if (this.typeSelectors.value != selected) {
                return;
            }
            this.logTypes = data.types_available;
            this.regenTypeListElem();
            this.lastSeq = data.last_sequence_id;
            let wasScrolledDown = this.actualLogContainer.scrollTop + this.actualLogContainer.clientHeight >= this.actualLogContainer.scrollHeight;
            let toRenderMessages = [];
            for (let typeNum in this.logTypes) {
                let type = this.logTypes[typeNum];
                let messages = data.data[type.name];
                if (messages == null) {
                    continue;
                }
                let storedData = this.logMessagesByType[type.name];
                if (!storedData) {
                    storedData = {
                        raw: {},
                        last_seq_id: this.lastSeq
                    };
                    this.logMessagesByType[type.name] = storedData;
                }
                for (let message of messages) {
                    if (storedData.raw[message.sequence_id]) {
                        continue;
                    }
                    storedData.raw[message.sequence_id] = message;
                    storedData.last_seq_id = message.sequence_id;
                    if (!filter || message.message.toLowerCase().includes(filter)) {
                        toRenderMessages.push([message, type]);
                    }
                }
                if (storedData.raw.length > 2048) {
                    let keys = Object.keys(storedData.raw);
                    let removeCount = keys.length - 1536;
                    keys.sort((a, b) => a - b);
                    for (let i = 0; i < removeCount; i++) {
                        delete storedData.raw[keys[i]];
                    }
                }
            }
            toRenderMessages.sort((a, b) => a[0].sequence_id - b[0].sequence_id);
            for (let [message, type] of toRenderMessages) {
                this.actualLogContainer.innerHTML += this.htmlMessage(message, type, this.lastBounce);
                this.lastBounce = (this.lastBounce + 1) % 2;
            }
            if (wasScrolledDown) {
                this.actualLogContainer.scrollTop = this.actualLogContainer.scrollHeight;
            }
        });
    }
}

serverLogs = new ServerLogsHelper();
