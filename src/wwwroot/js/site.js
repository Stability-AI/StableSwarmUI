function siteLoad() {
    for (let div of document.getElementsByClassName('auto-slider-box')) {
        let range = div.querySelector('input[type="range"]');
        let number = div.querySelector('input[type="number"]');
        range.addEventListener('input', () => number.value = range.value);
        number.addEventListener('input', () => range.value = number.value);
    }
}

siteLoad();
