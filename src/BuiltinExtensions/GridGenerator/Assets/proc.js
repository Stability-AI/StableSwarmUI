/*
 * This file is part of Infinity Grid Generator, view the README.md at https://github.com/mcmonkeyprojects/sd-infinity-grid-generator-script for more information.
*/

let suppressUpdate = true;
let file_extensions_alt = {};

function loadData() {
    let rawHash = window.location.hash;
    document.getElementById('x_' + rawData.axes[0].id).click();
    document.getElementById('x2_none').click();
    document.getElementById('y2_none').click();
    let makegif_axis = document.getElementById('makegif_axis');
    // rawData.ext/title/description
    for (var axis of rawData.axes) {
        // axis.id/title/description
        for (var val of axis.values) {
            // val.key/title/description/show
            let clicktab = getNavValTab(axis.id, val.key);
            clicktab.addEventListener('click', fillTable);
            if (!val.show) {
                setShowVal(axis.id, val.key, false);
            }
        }
        for (var prefix of ['x_', 'y_', 'x2_', 'y2_']) {
            document.getElementById(prefix + axis.id).addEventListener('click', fillTable);
        }
        for (var label of ['x2_none', 'y2_none']) {
            document.getElementById(label).addEventListener('click', fillTable);
        }
        makegif_axis.appendChild(new Option(axis.title, axis.id));
    }
    console.log(`Loaded data for '${rawData.title}'`);
    document.getElementById('autoScaleImages').addEventListener('change', updateScaling);
    document.getElementById('stickyNavigation').addEventListener('change', toggleTopSticky);
    document.getElementById('stickyLabels').addEventListener('change', toggleLabelSticky);
    document.getElementById('toggle_nav_button').addEventListener('click', updateTitleSticky);
    document.getElementById('toggle_adv_button').addEventListener('click', updateTitleSticky);
    document.getElementById('showDescriptions').checked = rawData.defaults.show_descriptions;
    document.getElementById('autoScaleImages').checked = rawData.defaults.autoscale;
    document.getElementById('stickyNavigation').checked = rawData.defaults.sticky;
    document.getElementById('stickyLabels').checked = rawData.defaults.sticky_labels;
    document.getElementById('score_display').addEventListener('click', fillTable);
    document.getElementById('score_setting').style.display = typeof getScoreFor == 'undefined' ? 'none' : 'inline-block';
    document.getElementById('pauseVideos').addEventListener('click', updatePauseVideos);
    updateStylesToMatchInputs();
    for (var axis of ['x', 'y', 'x2', 'y2']) {
        if (rawData.defaults[axis] != '') {
            document.getElementById(axis + '_' + rawData.defaults[axis]).click();
        }
    }
    applyHash(rawHash);
    suppressUpdate = false;
    fillTable();
    startAutoScroll();
    if (rawData.will_run) {
        setTimeout(checkForUpdates, 5000);
    }
}

function updatePauseVideos() {
    let doPause = document.getElementById('pauseVideos').checked;
    for (let video of document.getElementsByTagName('video')) {
        if (doPause) {
            video.pause();
        }
        else {
            video.play();
        }
    }
    updateHash();
}

function getExtension(filePath) {
    if (filePath in file_extensions_alt) {
        return file_extensions_alt[filePath];
    }
    return rawData.ext;
}

/** External callable. */
function fix_video(path) {
    let ext = getExtension(path);
    let matches = document.querySelectorAll(`img[data-img_path="${path}"]`);
    for (let match of matches) {
        if (ext != 'webm' && ext != 'mp4' && ext != 'mov') {
            if (!match.src.endsWith(ext)) {
                match.src = `${path}.${ext}`;
            }
            continue;
        }
        if (match.tagName == 'VIDEO') {
            continue;
        }
        let video = document.createElement('video');
        video.loop = true;
        video.muted = true;
        video.autoplay = true;
        let doPause = document.getElementById('pauseVideos').checked;
        video.paused = doPause;
        video.classList = match.classList;
        video.dataset.img_path = match.dataset.img_path;
        video.onclick = "doPopupFor(this)";
        video.onerror = "setImgPlaceholder(this)";
        let source = document.createElement('source');
        source.src = `${path}.${ext}`;
        source.type = `video/${ext}`;
        video.appendChild(source);
        match.parentElement.replaceChild(video, match);
        if (doPause) {
            video.pause();
        }
    }
}

function updateStylesToMatchInputs() {
    toggleTopSticky();
    toggleLabelSticky();
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
            return subVal.path;
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
        newTab.focus({ preventScroll: true });
    }
    else if (kbevent.key == 'ArrowRight') {
        var tabPage = document.getElementById('tablist_' + axis.id);
        var tabs = tabPage.getElementsByClassName('nav-link');
        var newTab = clickTabAfterActiveTab(tabs);
        newTab.focus({ preventScroll: true });
    }
    else if (kbevent.key == 'ArrowUp') {
        var next = getNextAxis(Array.from(rawData.axes).reverse(), axisId);
        if (next != null) {
            var selectedKey = getSelectedValKey(next);
            var swapToTab = getNavValTab(next.id, selectedKey);
            swapToTab.focus({ preventScroll: true });
        }
    }
    else if (kbevent.key == 'ArrowDown') {
        var next = getNextAxis(rawData.axes, axisId);
        if (next != null) {
            var selectedKey = getSelectedValKey(next);
            var swapToTab = getNavValTab(next.id, selectedKey);
            swapToTab.focus({ preventScroll: true });
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
    if (typeof text != 'string') {
        return text;
    }
    return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

function unescapeHtml(text) {
    return text.replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&quot;', '"').replaceAll('&#039;', "'").replaceAll('&amp;', '&');
}

function canShowVal(axis, val) {
    return document.getElementById(`showval_${axis}__${val}`).checked;
}

function setShowVal(axis, val, show) {
    document.getElementById(`showval_${axis}__${val}`).checked = show;
    getNavValTab(axis, val).classList.toggle('tab_hidden', !show);
}

function getNavValTab(axis, val) {
    return document.getElementById(`clicktab_${axis}__${val}`);
}

function percentToRedGreen(percent) {
    return `color-mix(in srgb, red, green ${percent}%)`;
}

let scoreTrackCounter = 0;
let scoreUpdates = [];
let lastScoreBump = Date.now();
let scoreBumpTracker = null;
let scoreMin = 0, scoreMax = 1;

function getXAxisContent(x, y, xAxis, yval, x2Axis, x2val, y2Axis, y2val) {
    let scriptDump = document.getElementById('image_script_dump');
    let imgPath = [];
    let index = 0;
    for (let subAxis of rawData.axes) {
        if (subAxis.id == x) {
            index = imgPath.length;
            imgPath.push(null);
        }
        else if (subAxis.id == y) {
            imgPath.push(yval.path);
        }
        else if (x2Axis != null && subAxis.id == x2Axis.id) {
            imgPath.push(x2val.path);
        }
        else if (y2Axis != null && subAxis.id == y2Axis.id) {
            imgPath.push(y2val.path);
        }
        else {
            imgPath.push(getSelectedValKey(subAxis));
        }
    }
    let newContent = '';
    let subInd = 0;
    let scoreDisplay = document.getElementById('score_display').value;
    for (let xVal of xAxis.values) {
        subInd++;
        if (!canShowVal(xAxis.id, xVal.key)) {
            continue;
        }
        imgPath[index] = xVal.path;
        let slashed = imgPath.join('/');
        let ext = getExtension(slashed);
        let actualUrl = slashed + '.' + ext;
        let id = scoreTrackCounter++;
        newContent += `<td id="td-img-${id}"><span></span>`;
        if (ext == 'mp4' || ext == 'webm' || ext == 'mov') {
            newContent += `<video loop autoplay muted class="table_img" data-img_path="${slashed}" onclick="doPopupFor(this)" onerror="setImgPlaceholder(this)" alt="${actualUrl}"><source src="${actualUrl}" type="video/${ext}"></source></video>`;
        }
        else {
            newContent += `<img class="table_img" data-img_path="${slashed}" onclick="doPopupFor(this)" onerror="setImgPlaceholder(this)" src="${actualUrl}" alt="${actualUrl}" />`;
        }
        newContent += '</td>';
        let newScr = null;
        if (typeof getMetadataScriptFor != 'undefined') {
            newScr = document.createElement('script');
            newScr.src = getMetadataScriptFor(slashed);
        }
        let doScores = scoreDisplay != 'None' && typeof getScoreFor != 'undefined';
        if (doScores) {
            scoreUpdates.push(() => {
                let score = getScoreFor(slashed);
                if (score) {
                    score = (score - scoreMin) / (scoreMax - scoreMin);
                    let elem = document.getElementById(`td-img-${id}`);
                    let color = percentToRedGreen(score * 100);
                    let blockColor = '';
                    if (scoreDisplay == 'Thin Outline')
                    {
                        let xborder = `border-top: 2px solid ${color}; border-bottom: 2px solid ${color};`;
                        let yborder = `border-left: 2px solid ${color}; border-right: 2px solid ${color};`;
                        elem.getElementsByTagName('img')[0].style = `${xborder} ${yborder}`;
                    }
                    else if (scoreDisplay == 'Thick Bars')
                    {
                        elem.getElementsByTagName('img')[0].style = `border-top: 10px solid ${color}; border-left: 10px solid ${color};`;
                    }
                    else if (scoreDisplay == 'Heatmap')
                    {
                        blockColor = `color-mix(in srgb, ${color} 50%, transparent)`;
                    }
                    elem.firstChild.innerHTML = `<div style="position: relative; width: 0; height: 0"><div style="position: absolute; left: 0; z-index: 20;">${Math.round(score * 100)}%</div><div class="heatmapper" style="position: absolute; left: 0; width: 100px; height: 100px; z-index: 10; background-color: ${blockColor}"></div></div>`;
                }
            });
        }
        if (newScr != null) {
            newScr.onload = () => {
                setTimeout(() => {
                    lastScoreBump = Date.now();
                    let ext = file_extensions_alt[slashed];
                    if (ext && !actualUrl.endsWith(ext)) {
                        fix_video(slashed);
                    }
                }, 1);
                if (scoreBumpTracker == null) {
                    scoreBumpTracker = setInterval(() => {
                        if (Date.now() - lastScoreBump > 300) {
                            clearInterval(scoreBumpTracker);
                            scoreBumpTracker = null;
                            if (doScores) {
                                scoreMin = 1;
                                scoreMax = 0;
                                for (let image of document.getElementsByClassName('table_img')) {
                                    let score = getScoreFor(image.dataset.img_path);
                                    if (score) {
                                        scoreMin = Math.min(scoreMin, score);
                                        scoreMax = Math.max(scoreMax, score);
                                    }
                                }
                                let upds = scoreUpdates;
                                scoreUpdates = [];
                                for (let update of upds) {
                                    update();
                                }
                                updateScaling();
                            }
                        }
                    }, 100);
                }
            };
        }
        if (newScr) {
            scriptDump.appendChild(newScr);
        }
    }
    return newContent;
}

function setImgPlaceholder(img) {
    if (!img.parentElement) {
        return;
    }
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
    return isFirst && val != null ? '<span title="' + escapeHtml(val.description) + '"><b>' + escapeHtml(val.title) + '</b></span><br>' : (val != null ? '<br>' : '');
}

function fillTable() {
    if (suppressUpdate) {
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
    document.getElementById('image_script_dump').innerHTML = '';
    for (var x2val of (x2Axis == null ? [null] : x2Axis.values)) {
        if (x2val != null && !canShowVal(x2Axis.id, x2val.key)) {
            continue;
        }
        var x2first = true;
        for (var val of xAxis.values) {
            if (!canShowVal(xAxis.id, val.key)) {
                continue;
            }
            newContent += `<th${(superFirst ? '' : ' class="superaxis_second"')} title="${val.description.replaceAll('"', '&quot;')}">${optDescribe(x2first, x2val)}<b>${escapeHtml(val.title)}</b></th>`;
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
            newContent += `<tr><td class="axis_label_td${(superFirst ? '' : ' superaxis_second')}" title="${escapeHtml(val.description)}">${optDescribe(y2first, y2val)}<b>${escapeHtml(val.title)}</b></td>`;
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

function getShownItemsOfAxis(axis) {
    return axis.values.filter(val => canShowVal(axis.id, val.key));
}

function getWantedScaling() {
    if (!document.getElementById('autoScaleImages').checked) {
        return 0;
    }
    var x = getCurrentSelectedAxis('x');
    var xAxis = getAxisById(x);
    var count = getShownItemsOfAxis(xAxis).length;
    var x2 = getCurrentSelectedAxis('x2');
    if (x2 != 'none') {
        var x2Axis = getAxisById(x2);
        count *= getShownItemsOfAxis(x2Axis).length;
    }
    return (90 / count);
}

function setImageScale(image, percent) {
    let heatmapper = image.parentElement.getElementsByClassName('heatmapper')[0];
    if (percent == 0) {
        image.style.width = '';
        image.style.height = '';
        if (heatmapper) {
            heatmapper.style.width = `${image.clientWidth}px`;
            heatmapper.style.height = `${image.clientWidth}px`;
        }
    }
    else {
        image.style.width = percent + 'vw';
        if (heatmapper) {
            heatmapper.style.width = percent + 'vw';
            heatmapper.style.height = percent * (parseFloat(image.clientWidth) / parseFloat(image.clientHeight)) + 'vw';
        }
        let width = image.getAttribute('width');
        let height = image.getAttribute('height');
        if (width != null && height != null) { // Rescale placeholders cleanly
            image.style.height = (percent * (parseFloat(height) / parseFloat(width))) + 'vw';
        }
        else {
            image.style.height = '';
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
        setShowVal(axisId, val.key, !hide);
    }
    fillTable();
}

function toggleShowVal(axis, val) {
    var show = canShowVal(axis, val);
    let element = getNavValTab(axis, val);
    element.classList.toggle('tab_hidden', !show);
    if (!show && element.classList.contains('active')) {
        var next = [...element.parentElement.parentElement.getElementsByClassName('nav-link')].filter(e => !e.classList.contains('tab_hidden'));
        if (next.length > 0) {
            next[0].click();
        }
    }
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
    if (!initialData) {
        return {};
    }
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
    let modalElem = document.getElementById('image_info_modal');
    let metaText;
    if (typeof getMetadataForImage != 'undefined') {
        metaText = getMetadataForImage(img);
    }
    else {
        let imgPath = img.dataset.img_path.split('/');
        let metaData = crunchMetadata(imgPath);
        metaText = typeof(formatMetadata) == 'undefined' ? JSON.stringify(metaData) : formatMetadata(metaData);
    }
    let params = escapeHtml(metaText).replaceAll('\n', '\n<br>');
    let text = 'Image: ' + img.alt + (params.length > 1 ? ', parameters: <br>' + params : '<br>(parameters hidden)');
    modalElem.innerHTML = `<div class="modal-dialog" style="display:none">(click outside image to close)</div><div class="modal_inner_div"><img onclick="$('#image_info_modal').modal('hide')" class="popup_modal_img" src="${img.src}"><br><div class="popup_modal_undertext">${text}</div>`;
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
    var header = document.getElementById('image_table_header');
    if (!header) {
        return;
    }
    updateHash();
    var topBar = document.getElementById('top_nav_bar');
    if (!topBar.classList.contains('sticky_top')) {
        header.style.top = '0';
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
    topBar.classList.remove('sticky_top');
    if (document.getElementById('stickyNavigation').checked) {
        topBar.classList.add('sticky_top');
    }
    updateTitleSticky();
}

function toggleLabelSticky() {
    updateHash();
    var table = document.getElementById('image_table');
    table.classList.remove('nostickytable');
    if (!document.getElementById('stickyLabels').checked) {
        table.classList.add('nostickytable');
    }
}

function removeGeneratedImages() {
    document.getElementById('save_image_output').innerHTML = '';
}

function makeImage(minRow = 0, doClear = true) {
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
    let count = 0;
    let sizeMult = parseFloat(document.getElementById('makeimage_size').value.replaceAll('x', ''));
    for (var row of rows) {
        count++;
        if (count < minRow) {
            continue;
        }
        var images = Array.from(row.getElementsByTagName('img'));
        var real_images = images.filter(i => i.src != 'placeholder.png');
        widest_width = Math.max(widest_width, ...real_images.map(i => i.naturalWidth * sizeMult));
        var height = Math.max(...real_images.map(i => i.naturalHeight * sizeMult));
        var y = pad_y + total_height;
        if (total_height + height > 30000) { // 32,767 is max canvas size
            setTimeout(() => makeImage(count, false), 100);
            break;
        }
        total_height += height + 1;
        columns = Math.max(columns, images.length);
        var label = row.getElementsByClassName('axis_label_td')[0];
        rowData.push({ row, images, real_images, height, label, y });
    }
    var holder = document.getElementById('save_image_output');
    if (doClear) {
        removeGeneratedImages();
    }
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
    pad_x = Math.min(pad_x, widest_width / 2);
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
    function wrap(text, width) {
        var words = text.split(' ');
        var lines = [];
        var line = '';
        for (var word of words) {
            var newLine = line + word + ' ';
            if (ctx.measureText(newLine).width > width) {
                lines.push(line);
                line = word + ' ';
            }
            else {
                line = newLine;
            }
        }
        lines.push(line);
        return lines.join('\n');
    }
    function writeMultiline(ctx, text, x, y) {
        for (var line of text.split('\n')) {
            ctx.fillText(line, x, y);
            y += 16;
        }
    }
    for (var row of rowData) {
        var blocks = row.label.getElementsByTagName('b');
        if (blocks.length == 2) {
            writeMultiline(ctx, wrap(blocks[0].textContent + "\n" + blocks[1].textContent, widest_width / 2), 5, row.y + 4);
        }
        else {
            writeMultiline(ctx, wrap(blocks[0].textContent, widest_width / 2), 5, row.y + 25);
        }
    }
    // Images
    for (var row of rowData) {
        var x = pad_x;
        for (var image of row.images) {
            if (image.src != 'placeholder.png') {
                ctx.drawImage(image, x, row.y, image.naturalWidth * sizeMult, image.naturalHeight * sizeMult);
                x += widest_width + 1;
            }
        }
    }
    var imageType = document.getElementById('makeimage_type').value;
    try {
        var data = canvas.toDataURL(`image/${imageType}`);
        canvas.remove();
        var img = new Image();
        img.className = 'save_image_output_img';
        img.src = data;
        holder.appendChild(img);
    }
    catch (e) {
        holder.appendChild(canvas);
        canvas.className = 'save_image_output_img';
        canvas.style.width = "200px";
        canvas.style.height = "200px";
    }
    $('#save_image_output_modal').modal('show');
}

function makeGif() {
    let holder = document.getElementById('save_image_output');
    removeGeneratedImages();
    let axisId = document.getElementById('makegif_axis').value;
    if (axisId == 'x-axis') {
        axisId = getCurrentSelectedAxis('x');
    }
    let sizeMult = parseFloat(document.getElementById('makegif_size').value.replaceAll('x', ''));
    let speed = parseFloat(document.getElementById('makegif_speed').value.replaceAll('/s', ''));
    let axis = getAxisById(axisId);
    let images = [];
    let imgPath = [];
    let index = 0;
    for (let subAxis of rawData.axes) {
        if (subAxis.id == axisId) {
            index = imgPath.length;
            imgPath.push(null);
        }
        else {
            imgPath.push(getSelectedValKey(subAxis));
        }
    }
    for (let val of axis.values) {
        if (!canShowVal(axis.id, val.key)) {
            continue;
        }
        imgPath[index] = val.path;
        let slashed = imgPath.join('/');
        let actualUrl = slashed + '.' + getExtension(slashed);
        images.push(actualUrl);
    }
    if (document.getElementById('makegif_direction').value == 'Backwards') {
        images.reverse();
    }
    let encoder = new GIFEncoder();
    encoder.setRepeat(0);
    encoder.setDelay(1000 / speed);
    encoder.start();
    let image1 = new Image();
    image1.src = images[0];
    image1.decode().then(() => {
        let canvas = document.createElement('canvas');
        canvas.width = image1.naturalWidth * sizeMult;
        canvas.height = image1.naturalHeight * sizeMult;
        ctx = canvas.getContext('2d');
        ctx.beginPath();
        let id = 1;
        let image2 = new Image();
        let callback = () => {
            ctx.drawImage(image2, 0, 0, canvas.width, canvas.height);
            encoder.addFrame(ctx);
            if (id >= images.length) {
                encoder.finish();
                let binary_gif = encoder.stream().getData();
                let data_url = 'data:image/gif;base64,' + encode64(binary_gif);
                let animatedImage = document.createElement('img');
                animatedImage.className = 'save_image_output_img';
                animatedImage.src = data_url;
                image1.remove();
                image2.remove();
                holder.appendChild(animatedImage);
                $('#save_image_output_modal').modal('show');
            }
            else {
                image2 = new Image();
                image2.src = images[id];
                id++;
                image2.decode().then(callback);
            }
        };
        image2.src = images[0];
        image2.decode().then(callback);
    });

}

function updateHash() {
    var hash = `#auto-loc`;
    for (let elem of ['showDescriptions', 'autoScaleImages', 'stickyNavigation', 'stickyLabels', 'pauseVideos']) {
        hash += `,${document.getElementById(elem).checked}`;
    }
    for (let val of ['x', 'y', 'x2', 'y2']) {
        hash += `,${encodeURIComponent(getCurrentSelectedAxis(val))}`;
    }
    for (let subAxis of rawData.axes) {
        hash += `,${encodeURIComponent(getSelectedValKey(subAxis))}`;
    }
    for (let axis of rawData.axes) {
        for (let value of axis.values) {
            if (!canShowVal(axis.id, value.key)) {
                hash += `&hide=${encodeURIComponent(axis.id)},${encodeURIComponent(value.key)}`;
            }
        }
    }
    history.pushState(null, null, hash);
}

function applyHash(hash) {
    if (!hash) {
        return;
    }
    let params = hash.substring(1).split('&');
    for (let hidden of params.slice(1)) {
        let [action, value] = hidden.split('=');
        if (action == 'hide') {
            let [axis, val] = value.split(',');
            setShowVal(axis, val, false);
        }
    }
    let hashInputs = params[0].split(',');
    let expectedLen = 1 + 5 + 4 + rawData.axes.length;
    if (hashInputs.length != expectedLen) {
        console.log(`Hash length mismatch: ${hashInputs.length} != ${expectedLen}, skipping value reload.`);
        return;
    }
    if (hashInputs[0] != 'auto-loc') {
        console.log(`Hash prefix mismatch: ${hashInputs[0]} != auto-loc, skipping value reload.`);
        return;
    }
    let index = 1;
    for (let elem of ['showDescriptions', 'autoScaleImages', 'stickyNavigation', 'stickyLabels', 'pauseVideos']) {
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
        let val = decodeURIComponent(hashInputs[index++]);
        let target = getNavValTab(subAxis.id, val);
        if (!target) {
            console.log(`Axis-value element not found: ${id}, skipping value reload.`);
            return;
        }
        target.click();
    }
    updateStylesToMatchInputs();
}

let lastUpdateObj = null;
let updateCheckCount = 0;
let updatesWithoutData = 0;

function tryReloadImg(img) {
    let target = img.dataset.errored_src;
    delete img.dataset.errored_src;
    img.removeAttribute('width');
    img.removeAttribute('height');
    img.addEventListener('error', function() {
        setImgPlaceholder(img);
    });
    img.src = target;
    if (typeof getMetadataScriptFor != 'undefined') {
        let newScr = document.createElement('script');
        newScr.src = getMetadataScriptFor(img.dataset.img_path);
        document.getElementById('image_script_dump').appendChild(newScr);
    }
}

function checkForUpdates() {
    if (!window.lastUpdated) {
        if (updatesWithoutData++ > 2) {
            console.log('Update-checker has no more updates.');
            for (let img of document.querySelectorAll(`img[data-errored_src]`)) {
                tryReloadImg(img);
            }
            return;
        }
    }
    else {
        console.log(`Update-checker found ${window.lastUpdated.length} updates.`);
        for (let url of window.lastUpdated) {
            for (let img of document.querySelectorAll(`img[data-errored_src]`)) {
                if (img.dataset.errored_src.endsWith(url)) {
                    tryReloadImg(img);
                }
            }
        }
        updateScaling();
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
