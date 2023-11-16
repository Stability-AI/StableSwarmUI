
class ImageEditor {
    constructor() {
        // Configurables:
        this.zoomRate = 1.1;
        this.gridScale = 4;
        // Data:
        this.active = false;
        this.inputDiv = getRequiredElementById('image_editor_input');
        this.zoomLevel = 1;
        this.offsetX = 0;
        this.offsetY = 0;
        this.backgroundColor = '#202020';
        this.gridColor = '#404040';
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
        canvas.addEventListener('mousemove', (e) => this.mouseMoveEvent(e));
        this.ctx = canvas.getContext('2d');
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
        this.dragStartX = e.clientX;
        this.dragStartY = e.clientY;
        this.dragging = true;
    }

    globalMouseUpEvent(e) {
        this.dragging = false;
    }

    mouseMoveEvent(e) {
        if (this.dragging) {
            this.offsetX += (e.clientX - this.dragStartX) / this.zoomLevel;
            this.offsetY += (e.clientY - this.dragStartY) / this.zoomLevel;
            this.dragStartX = e.clientX;
            this.dragStartY = e.clientY;
            this.redraw();
        }
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
        this.baseImage = img;
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

    redraw() {
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
        if (this.baseImage) {
            this.ctx.drawImage(this.baseImage, this.offsetX * this.zoomLevel, this.offsetY * this.zoomLevel, this.baseImage.naturalWidth * this.zoomLevel, this.baseImage.naturalHeight * this.zoomLevel);
        }
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
