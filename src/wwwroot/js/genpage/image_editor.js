
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
        this.y = editor.lastToolY;
        this.cursor = 'crosshair';
        editor.lastToolY += 40;
    }

    isMouseOver() {
        return this.editor.isMouseInBox(10, this.y, 32, 32);
    }

    draw() {
        let color = this.editor.toolBoxColor;
        if (this.editor.isMouseInBox(10, this.y, 32, 32)) {
            color = this.editor.toolBoxHoverColor;
            this.editor.drawTextBubble(`${this.name}\n${this.description}`, '12px sans-serif', 10 + 32 + 5, this.y, 300);
            this.editor.canvas.style.cursor = 'pointer';
        }
        else if (this.active) {
            color = this.editor.toolBoxActivecolor;
        }
        this.editor.drawBox(10, this.y, 32, 32, color, this.editor.uiBorderColor);
        if (this.iconImg.complete && this.iconImg.naturalWidth > 0) {
            this.editor.ctx.drawImage(this.iconImg, 10, this.y, 32, 32);
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
        this.uiColor = '#808080';
        this.uiBorderColor = '#b0b0b0';
        this.toolBoxColor = '#606060';
        this.toolBoxHoverColor = '#a0a0a0';
        this.toolBoxActivecolor = '#202020';
        this.textColor = '#ffffff';
        // Data:
        this.active = false;
        this.inputDiv = getRequiredElementById('image_editor_input');
        this.zoomLevel = 1;
        this.offsetX = 0;
        this.offsetY = 0;
        this.tools = {};
        this.mouseX = 0;
        this.mouseY = 0;
        this.lastToolY = 10;
        this.leftUiWidth = 52;
        // Tools:
        this.addTool(new ImageEditorTool(this, 'navigate', 'mouse', 'Navigate', 'Pure navigation tool, just moves around, no funny business.'));
        this.activateTool('navigate');
        this.addTool(new ImageEditorTool(this, 'select', 'select', 'Select', 'Select a region of the image.'));
        this.addTool(new ImageEditorTool(this, 'brush', 'paintbrush', 'Paintbrush', 'Draw on the image.'));
        this.addTool(new ImageEditorTool(this, 'eraser', 'eraser', 'Eraser', 'Erase parts of the image.'));
    }

    addTool(tool) {
        this.tools[tool.id] = tool;
    }

    activateTool(id) {
        if (this.activeTool) {
            this.activeTool.active = false;
        }
        this.tools[id].active = true;
        this.activeTool = this.tools[id];
    }

    createCanvas() {
        let canvas = document.createElement('canvas');
        canvas.width = this.inputDiv.clientWidth;
        canvas.height = this.inputDiv.clientHeight;
        this.inputDiv.appendChild(canvas);
        this.canvas = canvas;
        canvas.addEventListener('wheel', (e) => this.mouseWheelEvent(e));
        canvas.addEventListener('mousedown', (e) => this.mouseDownEvent(e));
        document.addEventListener('mouseup', (e) => this.globalMouseUpEvent(e));
        canvas.addEventListener('mouseup', (e) => this.localMouseUpEvent(e));
        canvas.addEventListener('mousemove', (e) => this.mouseMoveEvent(e));
        this.ctx = canvas.getContext('2d');
        canvas.style.cursor = 'none';
        this.redraw();
    }

    canvasCoordToImageCoord(x, y) {
        return [x / this.zoomLevel - this.offsetX, y / this.zoomLevel - this.offsetY];
    }

    imageCoordToCanvasCoord(x, y) {
        return [(x + this.offsetX) * this.zoomLevel, (y + this.offsetY) * this.zoomLevel];
    }

    mouseWheelEvent(e) {
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

    mouseDownEvent(e) {
        this.mouseClickedTool = null;
        if (this.isMouseInBox(0, 0, this.leftUiWidth, this.canvas.height)) {
            for (let tool of Object.values(this.tools)) {
                if (tool.isMouseOver()) {
                    this.mouseClickedTool = tool;
                }
            }
        }
        else {
            this.dragStartX = this.mouseX;
            this.dragStartY = this.mouseY;
            this.dragging = true;
        }
        this.redraw();
    }

    localMouseUpEvent(e) {
        if (this.mouseClickedTool && this.mouseClickedTool.isMouseOver()) {
            this.activateTool(this.mouseClickedTool.id);
        }
        this.mouseClickedTool = null;
        this.redraw();
    }

    globalMouseUpEvent(e) {
        this.dragging = false;
    }

    mouseMoveEvent(e) {
        this.mouseX = e.clientX - this.canvas.offsetLeft;
        this.mouseY = e.clientY - this.canvas.offsetTop;
        if (this.dragging) {
            this.offsetX += (this.mouseX - this.dragStartX) / this.zoomLevel;
            this.offsetY += (this.mouseY - this.dragStartY) / this.zoomLevel;
            this.dragStartX = this.mouseX;
            this.dragStartY = this.mouseY;
        }
        this.redraw();
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
    }

    deactivate() {
        this.active = false;
        this.inputDiv.style.display = 'none';
        this.unhideParams();
        setPageBarsFunc();
    }

    setBaseImage(img) {
        if (img) {
            this.baseImage = img.cloneNode();
        }
        else {
            this.baseImage = null;
        }
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
        this.drawBox(x - 1, y - 1, widest + 10, (fontHeight * lines.length) + 10, this.toolBoxColor, this.uiBorderColor);
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

    redraw() {
        if (this.isMouseInBox(0, 0, this.leftUiWidth, this.canvas.height)) {
            this.canvas.style.cursor = 'default';
        }
        else {
            this.canvas.style.cursor = this.activeTool.cursor;
        }
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
        // Image:
        if (this.baseImage) {
            this.ctx.drawImage(this.baseImage, this.offsetX * this.zoomLevel, this.offsetY * this.zoomLevel, this.baseImage.naturalWidth * this.zoomLevel, this.baseImage.naturalHeight * this.zoomLevel);
        }
        // UI:
        this.drawBox(1, 1, this.leftUiWidth, this.canvas.height - 1, this.uiColor, this.uiBorderColor);
        for (let tool of Object.values(this.tools)) {
            tool.draw();
        }
        this.drawTextBubble("SWARM IMAGE EDITOR - DEVELOPMENT PREVIEW\nIt doesn't work yet. Those icons are placeholders. This is just a preview.", '12px sans-serif', this.canvas.width / 4, 10, this.canvas.width / 2);
    }

    getFinalImageData() {
        let canvas = document.createElement('canvas');
        canvas.width = this.baseImage.naturalWidth;
        canvas.height = this.baseImage.naturalHeight;
        let ctx = canvas.getContext('2d');
        ctx.drawImage(this.baseImage, 0, 0);
        return canvas.toDataURL('image/png');
    }

    getFinalMaskData() {
        let canvas = document.createElement('canvas');
        canvas.width = this.baseImage.naturalWidth;
        canvas.height = this.baseImage.naturalHeight;
        let ctx = canvas.getContext('2d');
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        return canvas.toDataURL('image/png');
    }
}

let imageEditor = new ImageEditor();
