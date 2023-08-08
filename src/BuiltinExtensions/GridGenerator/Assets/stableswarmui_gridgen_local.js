// TODO

let all_metadata = {};

function getScoreFor(img) {
    return (((all_metadata[img] || {})['sui_image_params'] || {})['scoring'] || {})['average'] || null;
}

function getMetadataScriptFor(slashed) {
    return `${slashed}.metadata.js`;
}

function getMetadataForImage(img) {
    let data = all_metadata[img.dataset.img_path];
    if (!data) {
        return "";
    }
    return formatMetadata(data);
}

function formatMetadata(metadata) {
    let data = metadata.sui_image_params;
    if (!data) {
        return '';
    }
    let result = '';
    function appendObject(obj) {
        for (let key of Object.keys(obj)) {
            let val = obj[key];
            if (val) {
                if (typeof val == 'object') {
                    result += `${key}: `;
                    appendObject(val);
                    result += `, `;
                }
                else {
                    result += `${key}: ${val}, `;
                }
            }
        }
    };
    appendObject(data);
    return result;
}
