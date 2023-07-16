// TODO


function getScoreFor(img) {
    return (((all_metadata[img] || {})['sui_image_params'] || {})['scoring'] || {})['average'] || null;
}
function getMetadataScriptFor(slashed) {
    return `${slashed}.metadata.js`;
}
