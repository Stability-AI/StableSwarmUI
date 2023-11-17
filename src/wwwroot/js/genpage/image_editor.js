
class ImageEditorTool {
    constructor(editor, id, icon, name, description) {
        this.editor = editor;
        this.id = id;
        this.icon = icon;
        this.iconImg = new Image();
        this.iconImg.src = `/imgs/${icon}.png`;
        this.name = name;
        this.description = description;
        this.active = false;
        this.cursor = 'crosshair';
        this.makeDivs();
    }

    makeDivs() {
        this.infoBubble = document.createElement('div');
        this.infoBubble.className = 'image-editor-info-bubble';
        this.infoBubble.innerHTML = `<div class="image-editor-info-bubble-title">${escapeHtml(this.name)}</div><div class="image-editor-info-bubble-description">${escapeHtml(this.description)}</div>`;
        this.div = document.createElement('div');
        this.div.className = 'image-editor-tool';
        this.div.style.backgroundImage = `url(/imgs/${this.icon}.png)`;
        this.div.addEventListener('click', () => this.onClick());
        this.div.addEventListener('mouseenter', () => {
            this.infoBubble.style.top = `${this.div.offsetTop}px`;
            this.infoBubble.style.left = `${this.div.offsetLeft + this.div.clientWidth + 5}px`;
            this.infoBubble.classList.add('image-editor-info-bubble-visible');
        });
        this.div.addEventListener('mouseleave', () => {
            this.infoBubble.classList.remove('image-editor-info-bubble-visible');
        });
        this.editor.leftBar.appendChild(this.infoBubble);
        this.editor.leftBar.appendChild(this.div);
        this.configDiv = document.createElement('div');
        this.configDiv.className = 'image-editor-tool-bottombar';
        this.configDiv.style.display = 'none';
        this.editor.bottomBar.appendChild(this.configDiv);
    }

    onClick() {
        this.editor.activateTool(this.id);
    }

    setActive() {
        if (this.active) {
            return;
        }
        this.active = true;
        this.div.classList.add('image-editor-tool-selected');
        this.configDiv.style.display = 'flex';
    }

    setInactive() {
        if (!this.active) {
            return;
        }
        this.active = false;
        this.div.classList.remove('image-editor-tool-selected');
        this.configDiv.style.display = 'none';
    }

    draw() {
    }

    drawCircleBrush(x, y, radius) {
        this.editor.ctx.strokeStyle = '#ffffff';
        this.editor.ctx.lineWidth = 1;
        this.editor.ctx.globalCompositeOperation = 'difference';
        this.editor.ctx.beginPath();
        this.editor.ctx.arc(x, y, radius, 0, 2 * Math.PI);
        this.editor.ctx.stroke();
        this.editor.ctx.globalCompositeOperation = 'source-over';
    }

    onMouseDown() {
    }

    onMouseUp() {
    }

    onMouseMove() {
    }

    onGlobalMouseMove() {
        return false;
    }

    onGlobalMouseUp() {
        return false;
    }
}

class ImageEditorToolNavigate extends ImageEditorTool {
    constructor(editor) {
        super(editor, 'navigate', 'mouse', 'Navigate', 'Pure navigation tool, just moves around, no funny business.');
    }

    draw() {
        this.cursor = this.editor.mouseDown ? 'grabbing' : 'crosshair';
    }

    onGlobalMouseMove() {
        if (this.editor.mouseDown) {
            this.editor.offsetX += (this.editor.mouseX - this.editor.lastMouseX) / this.editor.zoomLevel;
            this.editor.offsetY += (this.editor.mouseY - this.editor.lastMouseY) / this.editor.zoomLevel;
            return true;
        }
        return false;
    }
}

class ImageEditorToolBrush extends ImageEditorTool {
    constructor(editor, id, icon, name, description, isEraser) {
        super(editor, id, icon, name, description);
        this.cursor = 'none';
        this.color = '#ffffff';
        this.radius = 10;
        this.opacity = 1;
        this.brushing = false;
        this.isEraser = isEraser;
        let colorHTML = `
        <div class="image-editor-tool-block">
            <label>Color:&nbsp;</label>
            <input type="text" class="auto-number id-col1" style="width:75px;flex-grow:0;" value="#ffffff">
            <input type="color" class="id-col2" value="#ffffff">
        </div>`;
        let radiusHtml = `<div class="image-editor-tool-block">
                <label>Radius:&nbsp;</label>
                <input type="number" style="width: 40px;" class="auto-number id-rad1" min="1" max="512" step="1" value="10">
                <input type="range" style="flex-grow: 2" class="auto-slider-range id-rad2" min="1" max="512" step="1" value="10">
            </div>`
        let opacityHtml = `<div class="image-editor-tool-block">
                <label>Opacity:&nbsp;</label>
                <input type="number" style="width: 40px;" class="auto-number id-opac1" min="1" max="100" step="1" value="100">
                <input type="range" style="flex-grow: 2" class="auto-slider-range id-opac2" min="1" max="100" step="1" value="100">
            </div>`;
        if (isEraser) {
            this.configDiv.innerHTML = radiusHtml + opacityHtml;
        }
        else {
            this.configDiv.innerHTML = colorHTML + radiusHtml + opacityHtml;
            this.colorText = this.configDiv.querySelector('.id-col1');
            this.colorSelector = this.configDiv.querySelector('.id-col2');
            this.colorText.addEventListener('input', () => {
                this.colorSelector.value = this.colorText.value;
                this.onConfigChange();
            });
            this.colorSelector.addEventListener('change', () => {
                this.colorText.value = this.colorSelector.value;
                this.onConfigChange();
            });
        }
        this.radiusNumber = this.configDiv.querySelector('.id-rad1');
        this.radiusSelector = this.configDiv.querySelector('.id-rad2');
        this.opacityNumber = this.configDiv.querySelector('.id-opac1');
        this.opacitySelector = this.configDiv.querySelector('.id-opac2');
        this.radiusNumber.addEventListener('input', () => {
            this.radiusSelector.value = this.radiusNumber.value;
            this.onConfigChange();
        });
        this.radiusSelector.addEventListener('input', () => {
            this.radiusNumber.value = this.radiusSelector.value;
            this.onConfigChange();
        });
        this.radiusNumber.dispatchEvent(new Event('input'));
        this.opacityNumber.addEventListener('input', () => {
            this.opacitySelector.value = this.opacityNumber.value;
            this.onConfigChange();
        });
        this.opacitySelector.addEventListener('input', () => {
            this.opacityNumber.value = this.opacitySelector.value;
            this.onConfigChange();
        });
        this.opacityNumber.dispatchEvent(new Event('input'));
    }

    onConfigChange() {
        if (!this.isEraser) {
            this.color = this.colorText.value;
        }
        this.radius = parseInt(this.radiusNumber.value);
        this.opacity = parseInt(this.opacityNumber.value) / 100;
    }

    draw() {
        this.drawCircleBrush(this.editor.mouseX, this.editor.mouseY, this.radius * this.editor.zoomLevel);
    }

    brush() {
        let [lastX, lastY] = this.bufferLayer.canvasCoordToLayerCoord(this.editor.lastMouseX, this.editor.lastMouseY);
        let [x, y] = this.bufferLayer.canvasCoordToLayerCoord(this.editor.mouseX, this.editor.mouseY);
        this.bufferLayer.drawFilledCircle(lastX, lastY, this.radius, this.color);
        this.bufferLayer.drawFilledCircleStrokeBetween(lastX, lastY, x, y, this.radius, this.color);
        this.bufferLayer.drawFilledCircle(x, y, this.radius, this.color);
    }

    onMouseDown() {
        if (this.brushing) {
            return;
        }
        this.brushing = true;
        let target = this.editor.activeLayer;
        this.bufferLayer = new ImageEditorLayer(this.editor, target.canvas.width, target.canvas.height);
        this.bufferLayer.opacity = this.opacity;
        if (this.isEraser) {
            this.bufferLayer.globalCompositeOperation = 'destination-out';
        }
        this.editor.activeLayer.childLayers.push(this.bufferLayer);
        this.brush();
    }

    onMouseMove() {
        if (this.brushing) {
            this.brush();
        }
    }

    onGlobalMouseUp() {
        if (this.brushing) {
            this.editor.activeLayer.childLayers.pop();
            this.editor.activeLayer.ctx.globalAlpha = this.opacity;
            this.bufferLayer.drawToBackDirect(this.editor.activeLayer.ctx, 0, 0, 1);
            this.bufferLayer = null;
            this.brushing = false;
            return true;
        }
        return false;
    }
}

class ImageEditorLayer {
    constructor(editor, width, height) {
        this.editor = editor;
        this.canvas = document.createElement('canvas');
        this.canvas.width = width;
        this.canvas.height = height;
        this.ctx = this.canvas.getContext('2d');
        this.offsetX = 0;
        this.offsetY = 0;
        this.opacity = 1;
        this.globalCompositeOperation = 'source-over';
        this.childLayers = [];
        this.buffer = null;
    }

    canvasCoordToLayerCoord(x, y) {
        let [x2, y2] = this.editor.canvasCoordToImageCoord(x, y);
        return [x2 - this.offsetX, y2 - this.offsetY];
    }

    layerCoordToCanvasCoord(x, y) {
        return this.editor.imageCoordToCanvasCoord(x + this.offsetX, y + this.offsetY);
    }

    drawFilledCircle(x, y, radius, color) {
        this.ctx.fillStyle = color;
        this.ctx.beginPath();
        this.ctx.arc(x, y, radius, 0, 2 * Math.PI);
        this.ctx.fill();
    }

    drawFilledCircleStrokeBetween(x1, y1, x2, y2, radius, color) {
        let angle = Math.atan2(y2 - y1, x2 - x1) + Math.PI / 2;
        let [rx, ry] = [radius * Math.cos(angle), radius * Math.sin(angle)];
        this.ctx.fillStyle = color;
        this.ctx.beginPath();
        this.ctx.moveTo(x1 + rx, y1 + ry);
        this.ctx.lineTo(x2 + rx, y2 + ry);
        this.ctx.lineTo(x2 - rx, y2 - ry);
        this.ctx.lineTo(x1 - rx, y1 - ry);
        this.ctx.closePath();
        this.ctx.fill();
    }

    drawToBackDirect(ctx, offsetX, offsetY, zoom) {
        let x = offsetX + this.offsetX;
        let y = offsetY + this.offsetY;
        ctx.globalAlpha = this.opacity;
        ctx.globalCompositeOperation = this.globalCompositeOperation;
        ctx.drawImage(this.canvas, x * zoom, y * zoom, this.canvas.width * zoom, this.canvas.height * zoom);
        ctx.globalAlpha = 1;
        ctx.globalCompositeOperation = 'source-over';
    }

    drawToBack(ctx, offsetX, offsetY, zoom) {
        if (this.childLayers.length > 0) {
            if (this.buffer == null) {
                this.buffer = new ImageEditorLayer(this.editor, this.canvas.width, this.canvas.height);
            }
            this.buffer.opacity = this.opacity;
            this.buffer.globalCompositeOperation = this.globalCompositeOperation;
            this.drawToBackDirect(this.buffer.ctx, -this.offsetX, -this.offsetY, 1);
            for (let layer of this.childLayers) {
                layer.drawToBack(this.buffer.ctx, 0, 0, 1);
            }
            this.buffer.drawToBackDirect(ctx, offsetX, offsetY, zoom);
        }
        else {
            this.buffer = null;
            this.drawToBackDirect(ctx, offsetX, offsetY, zoom);
        }
    }
}

class ImageEditor {
    constructor() {
        // Configurables:
        this.zoomRate = 1.1;
        this.gridScale = 4;
        this.backgroundColor = '#202020';
        this.gridColor = '#404040';
        this.uiColor = '#606060';
        this.uiBorderColor = '#b0b0b0';
        this.textColor = '#ffffff';
        this.boundaryColor = '#ffff00';
        // Data:
        this.active = false;
        this.inputDiv = getRequiredElementById('image_editor_input');
        this.leftBar = createDiv('image_editor_leftbar', 'image_editor_leftbar');
        this.inputDiv.appendChild(this.leftBar);
        this.bottomBar = createDiv('image_editor_bottombar', 'image_editor_bottombar');
        this.inputDiv.appendChild(this.bottomBar);
        this.zoomLevel = 1;
        this.offsetX = 0;
        this.offsetY = 0;
        this.tools = {};
        this.mouseX = 0;
        this.mouseY = 0;
        this.lastMouseX = 0;
        this.lastMouseY = 0;
        this.mouseDown = false;
        this.layers = [];
        this.activeLayer = null;
        this.realWidth = 512;
        this.realHeight = 512;
        this.finalOffsetX = 0;
        this.finalOffsetY = 0;
        // Tools:
        this.addTool(new ImageEditorToolNavigate(this));
        this.activateTool('navigate');
        this.addTool(new ImageEditorTool(this, 'select', 'select', 'Select', 'Select a region of the image.'));
        this.addTool(new ImageEditorToolBrush(this, 'brush', 'paintbrush', 'Paintbrush', 'Draw on the image.', false));
        this.addTool(new ImageEditorToolBrush(this, 'eraser', 'eraser', 'Eraser', 'Erase parts of the image.', true));
    }

    addTool(tool) {
        this.tools[tool.id] = tool;
    }

    activateTool(id) {
        if (this.activeTool) {
            this.activeTool.setInactive();
        }
        this.tools[id].setActive();
        this.activeTool = this.tools[id];
    }

    createCanvas() {
        let canvas = document.createElement('canvas');
        canvas.tabIndex = 1; // Force to be selectable
        canvas.className = 'image-editor-canvas';
        this.inputDiv.insertBefore(canvas, this.bottomBar);
        this.canvas = canvas;
        canvas.addEventListener('wheel', (e) => this.onMouseWheel(e));
        canvas.addEventListener('mousedown', (e) => this.onMouseDown(e));
        document.addEventListener('mouseup', (e) => this.onGlobalMouseUp(e));
        canvas.addEventListener('mouseup', (e) => this.onMouseUp(e));
        document.addEventListener('mousemove', (e) => this.onGlobalMouseMove(e));
        canvas.addEventListener('keydown', (e) => this.onKeyDown(e));
        canvas.addEventListener('keyup', (e) => this.onKeyUp(e));
        document.addEventListener('keydown', (e) => this.onGlobalKeyDown(e));
        document.addEventListener('keyup', (e) => this.onGlobalKeyUp(e));
        this.ctx = canvas.getContext('2d');
        canvas.style.cursor = 'none';
        this.resize();
    }

    handleAltDown() {
        if (!this.preAltTool) {
            this.preAltTool = this.activeTool;
            this.activateTool('navigate');
            this.redraw();
        }
    }

    onKeyDown(e) {
        if (e.key === 'Alt') {
            e.preventDefault();
            this.handleAltDown();
        }
    }

    onGlobalKeyDown(e) {
        if (e.key == 'Alt') {
            this.altDown = true;
        }
    }

    onKeyUp() {
    }

    onGlobalKeyUp(e) {
        if (e.key === 'Alt') {
            this.altDown = false;
            if (this.preAltTool) {
                e.preventDefault();
                this.activateTool(this.preAltTool.id);
                this.preAltTool = null;
                this.redraw();
            }
        }
    }

    onMouseWheel(e) {
        let zoom = Math.pow(this.zoomRate, -e.deltaY / 100);
        let mouseX = e.clientX - this.canvas.offsetLeft;
        let mouseY = e.clientY - this.canvas.offsetTop;
        let [origX, origY] = this.canvasCoordToImageCoord(mouseX, mouseY);
        this.zoomLevel *= zoom;
        let [newX, newY] = this.canvasCoordToImageCoord(mouseX, mouseY);
        this.offsetX += newX - origX;
        this.offsetY += newY - origY;
        this.redraw();
    }

    onMouseDown(e) {
        if (this.altDown) {
            this.handleAltDown();
        }
        this.mouseDown = true;
        this.activeTool.onMouseDown();
        this.redraw();
    }

    onMouseUp(e) {
        this.mouseDown = false;
        this.activeTool.onMouseUp();
        this.redraw();
    }

    onGlobalMouseUp(e) {
        let wasDown = this.mouseDown;
        this.mouseDown = false;
        if (this.activeTool.onGlobalMouseUp() || wasDown) {
            this.redraw();
        }
    }

    onGlobalMouseMove(e) {
        this.mouseX = e.clientX - this.canvas.offsetLeft;
        this.mouseY = e.clientY - this.canvas.offsetTop;
        let draw = false;
        if (this.isMouseInBox(0, 0, this.canvas.width, this.canvas.height)) {
            this.activeTool.onMouseMove();
            draw = true;
        }
        if (this.activeTool.onGlobalMouseMove()) {
            draw = true;
        }
        if (draw) {
            this.redraw();
        }
        this.lastMouseX = this.mouseX;
        this.lastMouseY = this.mouseY;
    }

    canvasCoordToImageCoord(x, y) {
        return [x / this.zoomLevel - this.offsetX, y / this.zoomLevel - this.offsetY];
    }

    imageCoordToCanvasCoord(x, y) {
        return [(x + this.offsetX) * this.zoomLevel, (y + this.offsetY) * this.zoomLevel];
    }

    isMouseInBox(x, y, width, height) {
        return this.mouseX >= x && this.mouseX < x + width && this.mouseY >= y && this.mouseY < y + height;
    }

    activate() {
        this.active = true;
        this.inputDiv.style.display = 'inline-block';
        this.doParamHides();
        setPageBarsFunc();
        if (!this.canvas) {
            this.createCanvas();
        }
        else {
            this.resize();
        }
    }

    deactivate() {
        this.active = false;
        this.inputDiv.style.display = 'none';
        this.unhideParams();
        setPageBarsFunc();
    }

    setBaseImage(img) {
        let layer = new ImageEditorLayer(this, img.naturalWidth, img.naturalHeight);
        layer.ctx.drawImage(img, 0, 0);
        this.layers = [layer];
        this.activeLayer = layer;
        this.realWidth = img.naturalWidth;
        this.realHeight = img.naturalHeight;
        this.finalOffsetX = 0;
        this.finalOffsetY = 0;
        if (this.active) {
            this.redraw();
        }
    }

    doParamHides() {
        let initImage = getRequiredElementById('input_initimage');
        let maskImage = getRequiredElementById('input_maskimage');
        if (initImage) {
            let parent = findParentOfClass(initImage, 'auto-input');
            parent.style.display = 'none';
            parent.dataset.visible_controlled = 'true';
        }
        if (maskImage) {
            let parent = findParentOfClass(maskImage, 'auto-input');
            parent.style.display = 'none';
            parent.dataset.visible_controlled = 'true';
        }
    }

    unhideParams() {
        let initImage = getRequiredElementById('input_initimage');
        let maskImage = getRequiredElementById('input_maskimage');
        if (initImage) {
            let parent = findParentOfClass(initImage, 'auto-input');
            parent.style.display = '';
            delete parent.dataset.visible_controlled;
        }
        if (maskImage) {
            let parent = findParentOfClass(maskImage, 'auto-input');
            parent.style.display = '';
            delete parent.dataset.visible_controlled;
        }
    }

    renderFullGrid(scale, width, color) {
        this.ctx.strokeStyle = color;
        this.ctx.beginPath();
        this.ctx.lineWidth = width;
        let [leftX, topY] = this.canvasCoordToImageCoord(0, 0);
        let [rightX, bottomY] = this.canvasCoordToImageCoord(this.canvas.width, this.canvas.height);
        for (let x = Math.floor(leftX / scale) * scale; x < rightX; x += scale) {
            let [canvasX, _] = this.imageCoordToCanvasCoord(x, 0);
            this.ctx.moveTo(canvasX, 0);
            this.ctx.lineTo(canvasX, this.canvas.height);
        }
        for (let y = Math.floor(topY / scale) * scale; y < bottomY; y += scale) {
            let [_, canvasY] = this.imageCoordToCanvasCoord(0, y);
            this.ctx.moveTo(0, canvasY);
            this.ctx.lineTo(this.canvas.width, canvasY);
        }
        this.ctx.stroke();
    }

    autoWrapText(text, maxWidth) {
        let lines = [];
        let rawLines = text.split('\n');
        for (let rawLine of rawLines) {
            let words = rawLine.split(' ');
            let line = '';
            for (let word of words) {
                let newLine = line + word + ' ';
                if (this.ctx.measureText(newLine).width > maxWidth) {
                    lines.push(line);
                    line = word + ' ';
                }
                else {
                    line = newLine;
                }
            }
            lines.push(line);
        }
        return lines;
    }

    drawTextBubble(text, font, x, y, maxWidth) {
        let lines = this.autoWrapText(text, maxWidth - 10);
        let widest = lines.map(line => this.ctx.measureText(line).width).reduce((a, b) => Math.max(a, b));
        let metrics = this.ctx.measureText(text);
        let fontHeight = metrics.fontBoundingBoxAscent + metrics.fontBoundingBoxDescent;
        this.drawBox(x - 1, y - 1, widest + 10, (fontHeight * lines.length) + 10, this.uiColor, this.uiBorderColor);
        let currentY = y;
        this.ctx.font = font;
        this.ctx.fillStyle = this.textColor;
        this.ctx.textBaseline = 'top';
        for (let line of lines) {
            this.ctx.fillText(line, x + 5, currentY + 5);
            currentY += fontHeight;
        }
    }

    drawBox(x, y, width, height, color, borderColor) {
        this.ctx.fillStyle = color;
        this.ctx.lineWidth = 1;
        this.ctx.beginPath();
        this.ctx.moveTo(x, y);
        this.ctx.lineTo(x + width, y);
        this.ctx.lineTo(x + width, y + height);
        this.ctx.lineTo(x, y + height);
        this.ctx.closePath();
        this.ctx.fill();
        if (borderColor) {
            this.ctx.strokeStyle = borderColor;
            this.ctx.stroke();
        }
    }

    resize() {
        if (this.canvas) {
            this.canvas.width = Math.max(100, this.inputDiv.clientWidth - this.leftBar.clientWidth);
            this.canvas.height = Math.max(100, this.inputDiv.clientHeight - this.bottomBar.clientHeight);
            this.redraw();
            setTimeout(() => this.redraw(), 1); // Freshly resized canvas forgets how sizes work and looks wonky, so redraw an extra time to clean it up.
        }
    }

    drawSelectionBox(x, y, width, height, color) {
        this.ctx.strokeStyle = color;
        this.ctx.lineWidth = 1;
        this.ctx.beginPath();
        this.ctx.setLineDash([5, 5]);
        this.ctx.moveTo(x - 1, y - 1);
        this.ctx.lineTo(x + width + 1, y - 1);
        this.ctx.lineTo(x + width + 1, y + height + 1);
        this.ctx.lineTo(x - 1, y + height + 1);
        this.ctx.closePath();
        this.ctx.stroke();
        this.ctx.setLineDash([]);
    }

    redraw() {
        this.ctx.save();
        this.canvas.style.cursor = this.activeTool.cursor;
        // Background:
        this.ctx.fillStyle = this.backgroundColor;
        this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
        let gridScale = this.gridScale;
        while (gridScale * this.zoomLevel < 32) {
            gridScale *= 8;
        }
        if (gridScale > this.gridScale) {
            let factor = (gridScale * this.zoomLevel - 32) / (32 * 8);
            let frac = factor * 100;
            this.renderFullGrid(gridScale / 8, 1, `color-mix(in srgb, ${this.gridColor} ${frac}%, ${this.backgroundColor})`);
        }
        this.renderFullGrid(gridScale, 3, this.gridColor);
        // Layers:
        for (let layer of this.layers) {
            layer.drawToBack(this.ctx, this.offsetX, this.offsetY, this.zoomLevel);
        }
        this.ctx.globalAlpha = 1;
        // UI:
        let [boundaryX, boundaryY] = this.imageCoordToCanvasCoord(this.finalOffsetX, this.finalOffsetY);
        this.drawSelectionBox(boundaryX, boundaryY, this.realWidth * this.zoomLevel, this.realHeight * this.zoomLevel, this.boundaryColor);
        this.activeTool.draw();
        this.drawTextBubble("SWARM IMAGE EDITOR - DEVELOPMENT PREVIEW\nIt doesn't work yet. Those icons are placeholders. This is just a preview.", '12px sans-serif', this.canvas.width / 4, 10, this.canvas.width / 2);
        this.ctx.restore();
    }

    getFinalImageData() {
        let canvas = document.createElement('canvas');
        canvas.width = this.realWidth;
        canvas.height = this.realHeight;
        for (let layer of this.layers) {
            layer.drawToBack(this.ctx, this.finalOffsetX, this.finalOffsetY, 1);
        }
        this.ctx.globalAlpha = 1;
        this.ctx.globalCompositeOperation = 'source-over';
        return canvas.toDataURL('image/png');
    }

    getFinalMaskData() {
        let canvas = document.createElement('canvas');
        canvas.width = this.realWidth;
        canvas.height = this.realHeight;
        let ctx = canvas.getContext('2d');
        // TODO: Actual mask
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        return canvas.toDataURL('image/png');
    }
}

let imageEditor = new ImageEditor();
