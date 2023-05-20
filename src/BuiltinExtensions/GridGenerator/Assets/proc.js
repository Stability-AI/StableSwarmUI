/**
 * This file is part of Infinity Grid Generator, view the README.md at https://github.com/mcmonkeyprojects/sd-infinity-grid-generator-script for more information.
 */

let supressUpdate = true;

function loadData() {
    let rawHash = window.location.hash;
    document.getElementById('x_' + rawData.axes[0].id).click();
    document.getElementById('x2_none').click();
    document.getElementById('y2_none').click();
    // rawData.ext/title/description
    for (var axis of rawData.axes) {
        // axis.id/title/description
        for (var val of axis.values) {
            // val.key/title/description/show
            var clicktab = document.getElementById('clicktab_' + axis.id + '__' + val.key);
            clicktab.addEventListener('click', fillTable);
            if (!val.show) {
                document.getElementById('showval_' + axis.id + '__' + val.key).checked = false;
                clicktab.classList.add('tab_hidden');
            }
        }
        for (var prefix of ['x_', 'y_', 'x2_', 'y2_']) {
            document.getElementById(prefix + axis.id).addEventListener('click', fillTable);
        }
        for (var label of ['x2_none', 'y2_none']) {
            document.getElementById(label).addEventListener('click', fillTable);
        }
    }
    console.log(`Loaded data for '${rawData.title}'`);
    document.getElementById('autoScaleImages').addEventListener('change', updateScaling);
    document.getElementById('stickyNavigation').addEventListener('change', toggleTopSticky);
    document.getElementById('toggle_nav_button').addEventListener('click', updateTitleSticky);
    document.getElementById('toggle_adv_button').addEventListener('click', updateTitleSticky);
    document.getElementById('showDescriptions').checked = rawData.defaults.show_descriptions;
    document.getElementById('autoScaleImages').checked = rawData.defaults.autoscale;
    document.getElementById('stickyNavigation').checked = rawData.defaults.sticky;
    for (var axis of ['x', 'y', 'x2', 'y2']) {
        if (rawData.defaults[axis] != '') {
            console.log('find ' + axis + '_' + rawData.defaults[axis]);
            document.getElementById(axis + '_' + rawData.defaults[axis]).click();
        }
    }
    applyHash(rawHash);
    supressUpdate = false;
    fillTable();
    startAutoScroll();
    if (rawData.will_run) {
        setTimeout(checkForUpdates, 5000);
    }
}

function getAxisById(id) {
    return rawData.axes.find(axis => axis.id == id);
}

function getNextAxis(axes, startId) {
    var next = false;
    for (var subAxis of axes) {
        if (subAxis.id == startId) {
            next = true;
        }
        else if (next) {
            return subAxis;
        }
    }
    return null;
}

function getSelectedValKey(axis) {
    for (var subVal of axis.values) {
        if (window.getComputedStyle(document.getElementById('tab_' + axis.id + '__' + subVal.key)).display != 'none') {
            return subVal.key;
        }
    }
    return null;
}

var popoverLastImg = null;

function clickRowImage(rows, x, y) {
    $('#image_info_modal').modal('hide');
    var columns = rows[y].getElementsByTagName('td');
    columns[x].getElementsByTagName('img')[0].click();
}

window.addEventListener('keydown', function(kbevent) {
    if ($('#image_info_modal').is(':visible')) {
        if (kbevent.key == 'Escape') {
            $('#image_info_modal').modal('toggle');
            kbevent.preventDefault();
            kbevent.stopPropagation();
            return false;
        }
        var tableElem = document.getElementById('image_table');
        var rows = tableElem.getElementsByTagName('tr');
        var matchedRow = null;
        var x = 0, y = 0;
        for (var row of rows) {
            var columns = row.getElementsByTagName('td');
            for (var column of columns) {
                var images = column.getElementsByTagName('img');
                if (images.length == 1 && images[0] == popoverLastImg) {
                    matchedRow = row;
                    break;
                }
                x++;
            }
            if (matchedRow != null) {
                break;
            }
            x = 0;
            y++;
        }
        if (matchedRow == null) {
            return;
        }
        if (kbevent.key == 'ArrowLeft') {
            if (x > 1) {
                x--;
                clickRowImage(rows, x, y);
            }
        }
        else if (kbevent.key == 'ArrowRight') {
            x++;
            var columns = matchedRow.getElementsByTagName('td');
            if (columns.length > x) {
                clickRowImage(rows, x, y);
            }
        }
        else if (kbevent.key == 'ArrowUp') {
            if (y > 1) {
                y--;
                clickRowImage(rows, x, y);
            }
        }
        else if (kbevent.key == 'ArrowDown') {
            y++;
            if (rows.length > y) {
                clickRowImage(rows, x, y);
            }
        }
        else {
            return;
        }
        kbevent.preventDefault();
        kbevent.stopPropagation();
        return false;
    }
    var elem = document.activeElement;
    if (!elem.id.startsWith('clicktab_')) {
        return;
    }
    var axisId = elem.id.substring('clicktab_'.length);
    var splitIndex = axisId.lastIndexOf('__');
    axisId = axisId.substring(0, splitIndex);
    var axis = getAxisById(axisId);
    if (kbevent.key == 'ArrowLeft') {
        var tabPage = document.getElementById('tablist_' + axis.id);
        var tabs = tabPage.getElementsByClassName('nav-link');
        var newTab = clickTabAfterActiveTab(Array.from(tabs).reverse());
        newTab.focus();
    }
    else if (kbevent.key == 'ArrowRight') {
        var tabPage = document.getElementById('tablist_' + axis.id);
        var tabs = tabPage.getElementsByClassName('nav-link');
        var newTab = clickTabAfterActiveTab(tabs);
        newTab.focus();
    }
    else if (kbevent.key == 'ArrowUp') {
        var next = getNextAxis(Array.from(rawData.axes).reverse(), axisId);
        if (next != null) {
            var selectedKey = getSelectedValKey(next);
            var swapToTab = this.document.getElementById(`clicktab_${next.id}__${selectedKey}`);
            swapToTab.focus();
        }
    }
    else if (kbevent.key == 'ArrowDown') {
        var next = getNextAxis(rawData.axes, axisId);
        if (next != null) {
            var selectedKey = getSelectedValKey(next);
            var swapToTab = this.document.getElementById(`clicktab_${next.id}__${selectedKey}`);
            swapToTab.focus();
        }
    }
    else {
        return;
    }
    kbevent.preventDefault();
    kbevent.stopPropagation();
    return false;
}, true);

function escapeHtml(text) {
    return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

function unescapeHtml(text) {
    return text.replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&quot;', '"').replaceAll('&#039;', "'").replaceAll('&amp;', '&');
}

function canShowVal(axis, val) {
    return document.getElementById(`showval_${axis}__${val}`).checked;
}

function getXAxisContent(x, y, xAxis, val, x2Axis, x2val, y2Axis, y2val) {
    var imgPath = [];
    var index = 0;
    for (var subAxis of rawData.axes) {
        if (subAxis.id == x) {
            index = imgPath.length;
            imgPath.push(null);
        }
        else if (subAxis.id == y) {
            imgPath.push(val.key);
        }
        else if (x2Axis != null && subAxis.id == x2Axis.id) {
            imgPath.push(x2val.key);
        }
        else if (y2Axis != null && subAxis.id == y2Axis.id) {
            imgPath.push(y2val.key);
        }
        else {
            imgPath.push(getSelectedValKey(subAxis));
        }
    }
    var newContent = '';
    for (var xVal of xAxis.values) {
        if (!canShowVal(xAxis.id, xVal.key)) {
            continue;
        }
        imgPath[index] = xVal.key;
        var actualUrl = imgPath.join('/') + '.' + rawData.ext;
        newContent += `<td><img class="table_img" data-img-path="${imgPath.join('/')}" onclick="doPopupFor(this)" onerror="setImgPlaceholder(this)" src="${actualUrl}" alt="${actualUrl}" /></td>`;
    }
    return newContent;
}

function setImgPlaceholder(img) {
    img.onerror = undefined;
    img.dataset.errored_src = img.src;
    img.src = 'placeholder.png';
    if (rawData.min_width) {
        img.width = rawData.min_width;
        img.height = rawData.min_height;
    }
    setImageScale(img, getWantedScaling());
}

function optDescribe(isFirst, val) {
    return isFirst && val != null ? '<span title="' + escapeHtml(val.description) + '"><b>' + val.title + '</b></span><br>' : (val != null ? '<br>' : '');
}

function fillTable() {
    if (supressUpdate) {
        return;
    }
    var x = getCurrentSelectedAxis('x');
    var y = getCurrentSelectedAxis('y');
    var x2 = getCurrentSelectedAxis('x2');
    var y2 = getCurrentSelectedAxis('y2');
    console.log('Do fill table, x=' + x + ', y=' + y + ', x2=' + x2 + ', y2=' + y2);
    var xAxis = getAxisById(x);
    var yAxis = getAxisById(y);
    var x2Axis = x2 == 'None' || x2 == x || x2 == y ? null : getAxisById(x2);
    var y2Axis = y2 == 'None' || y2 == x2 || y2 == x || y2 == y ? null : getAxisById(y2);
    var table = document.getElementById('image_table');
    var newContent = '<tr id="image_table_header" class="sticky_top"><th></th>';
    var superFirst = true;
    for (var x2val of (x2Axis == null ? [null] : x2Axis.values)) {
        if (x2val != null && !canShowVal(x2Axis.id, x2val.key)) {
            continue;
        }
        var x2first = true;
        for (var val of xAxis.values) {
            if (!canShowVal(xAxis.id, val.key)) {
                continue;
            }
            newContent += `<th${(superFirst ? '' : ' class="superaxis_second"')} title="${val.description.replaceAll('"', '&quot;')}">${optDescribe(x2first, x2val)}<b>${val.title}</b></th>`;
            x2first = false;
        }
        superFirst = !superFirst;
    }
    newContent += '</tr>';
    superFirst = true;
    for (var y2val of (y2Axis == null ? [null] : y2Axis.values)) {
        if (y2val != null && !canShowVal(y2Axis.id, y2val.key)) {
            continue;
        }
        var y2first = true;
        for (var val of yAxis.values) {
            if (!canShowVal(yAxis.id, val.key)) {
                continue;
            }
            newContent += `<tr><td class="axis_label_td${(superFirst ? '' : ' superaxis_second')}" title="${escapeHtml(val.description)}">${optDescribe(y2first, y2val)}<b>${val.title}</b></td>`;
            y2first = false;
            for (var x2val of (x2Axis == null ? [null] : x2Axis.values)) {
                if (x2val != null && !canShowVal(x2Axis.id, x2val.key)) {
                    continue;
                }
                newContent += getXAxisContent(x, y, xAxis, val, x2Axis, x2val, y2Axis, y2val);
            }
            newContent += '</tr>';
            if (x == y) {
                break;
            }
        }
        superFirst = !superFirst;
    }
    table.innerHTML = newContent;
    updateScaling();
}

function getCurrentSelectedAxis(axisPrefix) {
    var id = document.querySelector(`input[name="${axisPrefix}_axis_selector"]:checked`).id;
    var index = id.indexOf('_');
    return id.substring(index + 1);
}

function getWantedScaling() {
    if (!document.getElementById('autoScaleImages').checked) {
        return 0;
    }
    var x = getCurrentSelectedAxis('x');
    var xAxis = getAxisById(x);
    var count = xAxis.values.length;
    var x2 = getCurrentSelectedAxis('x2');
    if (x2 != 'none') {
        var x2Axis = getAxisById(x2);
        count *= x2Axis.values.length;
    }
    return (90 / count);
}

function setImageScale(image, percent) {
    if (percent == 0) {
        image.style.width = '';
        image.style.height = '';
    }
    else {
        image.style.width = percent + 'vw';
        let width = image.getAttribute('width');
        let height = image.getAttribute('height');
        if (width != null && height != null) { // Rescale placeholders cleanly
            image.style.height = (percent * (parseFloat(height) / parseFloat(width))) + 'vw';
        }
    }
}

function updateScaling() {
    let percent = getWantedScaling();
    for (var image of document.getElementById('image_table').getElementsByClassName('table_img')) {
        setImageScale(image, percent);
    }
    updateTitleSticky();
}

function toggleDescriptions() {
    var show = document.getElementById('showDescriptions').checked;
    for (var cName of ['tabval_subdiv', 'axis_table_cell']) {
        for (var elem of document.getElementsByClassName(cName)) {
            elem.classList.toggle('tab_hidden', !show);
        }
    }
    updateTitleSticky();
}

function toggleShowAllAxis(axisId) {
    var axis = getAxisById(axisId);
    var hide = axis.values.some(val => {
        return canShowVal(axisId, val.key);
    });
    for (var val of axis.values) {
        document.getElementById('showval_' + axisId + '__' + val.key).checked = !hide;
        var element = document.getElementById('clicktab_' + axisId + '__' + val.key);
        element.classList.toggle('tab_hidden', hide);
    }
    fillTable();
}

function toggleShowVal(axis, val) {
    var show = canShowVal(axis, val);
    var element = document.getElementById('clicktab_' + axis + '__' + val);
    element.classList.toggle('tab_hidden', !show);
    fillTable();
}

var anyRangeActive = false;

function enableRange(id) {
    var range = document.getElementById('range_tablist_' + id);
    var label = document.getElementById('label_range_tablist_' + id);
    range.oninput = function() {
        anyRangeActive = true;
        label.innerText = (range.value / 2) + ' seconds';
    };
    var tabPage = document.getElementById('tablist_' + id);
    return {
        range,
        counter: 0,
        tabs: tabPage.getElementsByClassName('nav-link')
    };
}

function clickTabAfterActiveTab(tabs) {
    var firstTab = null;
    var foundActive = false;
    var nextTab = Array.from(tabs).find(tab => {
        var isActive = tab.classList.contains('active');
        var isHidden = tab.classList.contains('tab_hidden');
        if (!isHidden && !isActive && !firstTab) {
            firstTab = tab;
        }
        if (isActive) {
            foundActive = true;
            return false;
        }
        return (foundActive && !isHidden);
    }) || firstTab;

    if (nextTab) {
        nextTab.click();
    }
    return nextTab;
}

const timer = ms => new Promise(res => setTimeout(res, ms));

async function startAutoScroll() {
    var rangeSet = [];
    for (var axis of rawData.axes) {
        rangeSet.push(enableRange(axis.id));
    }
    while (true) {
        await timer(500);
        if (!anyRangeActive) {
            continue;
        }
        for (var data of rangeSet) {
            if (data.range.value <= 0) {
                continue;
            }
            data.counter++;
            if (data.counter < data.range.value) {
                continue;
            }
            data.counter = 0;
            clickTabAfterActiveTab(data.tabs);
        }
    }
}

function crunchMetadata(parts) {
    if (!('metadata' in rawData)) {
        return {};
    }
    var initialData = structuredClone(rawData.metadata);
    for (var index = 0; index < parts.length; index++) {
        var part = parts[index];
        var axis = rawData.axes[index];
        var actualVal = axis.values.find(val => val.key == part);
        if (actualVal == null) {
            return { 'error': `metadata parsing failed for part ${index}: ${part}` };
        }
        for (var [key, value] of Object.entries(actualVal.params)) {
            key = key.replaceAll(' ', '');
            if (typeof(crunchParamHook) == 'undefined' || !crunchParamHook(initialData, key, value)) {
                initialData[key] = value;
            }
        }
    }
    return initialData;
}

function doPopupFor(img) {
    popoverLastImg = img;
    var imgPath = img.dataset.imgPath.split('/');
    var modalElem = document.getElementById('image_info_modal');
    var metaData = crunchMetadata(imgPath);
    var metaText = typeof(formatMetadata) == 'undefined' ? JSON.stringify(metaData) : formatMetadata(metaData);
    var params = escapeHtml(metaText).replaceAll('\n', '\n<br>');
    var text = 'Image: ' + img.alt + (params.length > 1 ? ', parameters: <br>' + params : '<br>(parameters hidden)');
    modalElem.innerHTML = `<div class="modal-dialog" style="display:none">(click outside image to close)</div><div class="modal_inner_div"><img class="popup_modal_img" src="${img.src}"><br><div class="popup_modal_undertext">${text}</div>`;
    $('#image_info_modal').modal('toggle');
}

function updateTitleStickyDirect() {
    var height = Math.round(document.getElementById('top_nav_bar').getBoundingClientRect().height);
    var header = document.getElementById('image_table_header');
    if (header.style.top != height + 'px') { // This check is to reduce the odds of the browser yelling at us
        header.style.top = height + 'px';
    }
}

function updateTitleSticky() {
    updateHash();
    var topBar = document.getElementById('top_nav_bar');
    if (!topBar.classList.contains('sticky_top')) {
        document.getElementById('image_table_header').style.top = '0';
        return;
    }
    // client rect is dynamically animated, so, uh, just hack it for now.
    // TODO: Actually smooth attachment.
    var rate = 50;
    for (var time = 0; time <= 500; time += rate) {
        setTimeout(updateTitleStickyDirect, time);
    }
}

function toggleTopSticky() {
    var topBar = document.getElementById('top_nav_bar');
    topBar.classList.toggle('sticky_top');
    updateTitleSticky();
}

function makeImage() {
    // Preprocess data
    var imageTable = document.getElementById('image_table');
    var rows = Array.from(imageTable.getElementsByTagName('tr')).filter(e => e.getElementsByTagName('img').length > 0);
    var header = document.getElementById('image_table_header');
    var headers = Array.from(header.getElementsByTagName('th')).slice(1);
    var widest_width = 0;
    var total_height = 0;
    var columns = 0;
    var rowData = [];
    var pad_x = 64, pad_y = 64;
    for (var row of rows) {
        var images = Array.from(row.getElementsByTagName('img'));
        var real_images = images.filter(i => i.src != 'placeholder.png');
        widest_width = Math.max(widest_width, ...real_images.map(i => i.naturalWidth));
        var height = Math.max(...real_images.map(i => i.naturalHeight));
        var y = pad_y + total_height;
        total_height += height + 1;
        columns = Math.max(columns, images.length);
        var label = row.getElementsByClassName('axis_label_td')[0];
        rowData.push({ row, images, real_images, height, label, y });
    }
    console.log(`Will create image at ${widest_width * columns} x ${total_height} pixels`);
    var holder = document.getElementById('save_image_helper');
    for (var oldImage of holder.getElementsByTagName('img')) {
        oldImage.remove();
    }
    document.getElementById('save_image_info').style.display = 'block';
    // Temporary canvas to measure what padding we need
    var canvas = new OffscreenCanvas(256, 256);
    var ctx = canvas.getContext('2d');
    ctx.beginPath();
    ctx.rect(0, 0, canvas.width, canvas.height);
    ctx.font = '16px sans';
    ctx.textBaseline = 'top';
    for (var row of rowData) {
        var blocks = row.label.getElementsByTagName('b');
        pad_x = Math.max(pad_x, ctx.measureText(blocks[0].textContent).width);
        if (blocks.length == 2) {
            pad_x = Math.max(pad_x, ctx.measureText(blocks[1].textContent).width);
        }
    }
    pad_x += 5;
    canvas = document.createElement('canvas');
    canvas.width = (widest_width + 1) * columns + pad_x;
    canvas.height = total_height + pad_y;
    ctx = canvas.getContext('2d');
    // Background
    ctx.beginPath();
    ctx.rect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#202020';
    ctx.fill();
    // Secondary color toggling
    var doColor = false;
    ctx.fillStyle = '#303030';
    var grid_x = pad_x;
    for (var part of headers) {
        if (part.getElementsByTagName('b').length == 2) {
            doColor = !doColor;
        }
        if (doColor) {
            ctx.beginPath();
            ctx.rect(grid_x, 0, widest_width, pad_y);
            ctx.fill();
        }
        grid_x += widest_width + 1;
    }
    doColor = false;
    for (var row of rowData) {
        if (row.label.getElementsByTagName('b').length == 2) {
            doColor = !doColor;
        }
        if (doColor) {
            ctx.beginPath();
            ctx.rect(0, row.y, pad_x, row.height);
            ctx.fill();
        }
    }
    // Grid lines
    ctx.fillStyle = '#000000';
    for (var row of rowData) {
        ctx.beginPath();
        ctx.rect(0, row.y, canvas.width, 1);
        ctx.fill();
    }
    grid_x = pad_x - 1;
    for (var i = 0; i < columns; i++) {
        ctx.beginPath();
        ctx.rect(grid_x, 0, 1, canvas.height);
        ctx.fill();
        grid_x += widest_width + 1;
    }
    // Text Labels
    ctx.font = '16px sans';
    ctx.textBaseline = 'top';
    ctx.fillStyle = '#ffffff';
    ctx.beginPath();
    ctx.rect(0, 0, canvas.width, canvas.height);
    grid_x = pad_x + 5;
    for (var part of headers) {
        var blocks = part.getElementsByTagName('b');
        if (blocks.length == 2) {
            ctx.fillText(blocks[0].textContent, grid_x, 5, widest_width);
            ctx.fillText(blocks[1].textContent, grid_x, 25, widest_width);
        }
        else {
            ctx.fillText(blocks[0].textContent, grid_x, 25, widest_width);
        }
        grid_x += widest_width + 1;
    }
    for (var row of rowData) {
        var blocks = row.label.getElementsByTagName('b');
        if (blocks.length == 2) {
            ctx.fillText(blocks[0].textContent, 5, row.y + 4);
            ctx.fillText(blocks[1].textContent, 5, row.y + 25);
        }
        else {
            ctx.fillText(blocks[0].textContent, 5, row.y + 25);
        }
    }
    // Images
    for (var row of rowData) {
        var x = pad_x;
        for (var image of row.images) {
            if (image.src != 'placeholder.png') {
                ctx.drawImage(image, x, row.y);
                x += widest_width + 1;
            }
        }
    }
    var imageType = $("#makeimage_type :selected").text();
    var data = canvas.toDataURL(`image/${imageType}`);
    canvas.remove();
    var img = new Image(256, 256);
    img.src = data;
    holder.appendChild(img);
}

function updateHash() {
    var hash = `#auto-loc`;
    for (let elem of ['showDescriptions', 'autoScaleImages', 'stickyNavigation']) {
        hash += `,${document.getElementById(elem).checked}`;
    }
    for (let val of ['x', 'y', 'x2', 'y2']) {
        hash += `,${encodeURIComponent(getCurrentSelectedAxis(val))}`;
    }
    for (let subAxis of rawData.axes) {
        hash += `,${encodeURIComponent(getSelectedValKey(subAxis))}`;
    }
    history.pushState(null, null, hash);
}

function applyHash(hash) {
    if (!hash) {
        return;
    }
    let hashInputs = hash.substring(1).split(',');
    let expectedLen = 1 + 3 + 4 + rawData.axes.length;
    if (hashInputs.length != expectedLen) {
        console.log(`Hash length mismatch: ${hashInputs.length} != ${expectedLen}, skipping value reload.`);
        return;
    }
    if (hashInputs[0] != 'auto-loc') {
        console.log(`Hash prefix mismatch: ${hashInputs[0]} != auto-loc, skipping value reload.`);
        return;
    }
    let index = 1;
    for (let elem of ['showDescriptions', 'autoScaleImages', 'stickyNavigation']) {
        document.getElementById(elem).checked = hashInputs[index++] == 'true';
    }
    for (let axis of ['x', 'y', 'x2', 'y2']) {
        let id = axis + '_' + decodeURIComponent(hashInputs[index++]);
        let target = document.getElementById(id);
        if (!target) {
            console.log(`Axis element not found: ${id}, skipping value reload.`);
            return;
        }
        target.click();
    }
    for (let subAxis of rawData.axes) {
        let id = 'clicktab_' + subAxis.id + '__' + decodeURIComponent(hashInputs[index++]);
        let target = document.getElementById(id);
        if (!target) {
            console.log(`Axis-value element not found: ${id}, skipping value reload.`);
            return;
        }
        target.click();
    }
}

let lastUpdateObj = null;
let updateCheckCount = 0;
let updatesWithoutData = 0;

function checkForUpdates() {
    if (!window.lastUpdated) {
        if (updatesWithoutData++ > 2) {
            console.log('Update-checker has no more updates.');
            return;
        }
    }
    else {
        console.log(`Update-checker found ${window.lastUpdated.length} updates.`);
        for (let url of window.lastUpdated) {
            for (let img of document.querySelectorAll(`img[data-errored_src]`)) {
                if (img.dataset.errored_src.endsWith(url)) {
                    let target = img.dataset.errored_src;
                    img.dataset.errored_src = null;
                    img.src = target;
                }
            }
        }
        window.lastUpdated = null;
    }
    if (lastUpdateObj != null) {
        lastUpdateObj.remove();
    }
    lastUpdateObj = document.createElement('script');
    lastUpdateObj.src = `last.js?vary=${updateCheckCount++}`;
    document.body.appendChild(lastUpdateObj);
    setTimeout(checkForUpdates, 5 * 1000);
}

loadData();
