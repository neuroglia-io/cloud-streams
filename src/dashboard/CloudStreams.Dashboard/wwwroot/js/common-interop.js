const sleep = (delay) => new Promise(resolve => setTimeout(resolve, delay));

/**
 * Sets a checkbox tri-state
 * @param {any} checkboxEl The validation schema to add
 * @param {any} state 0 = unchecked, 1 = checked, -1 = indeterminate
 * @returns
 */
export function setCheckboxState(checkboxEl, state) {
    if (!checkboxEl) return;
    switch (state) {
        case 1:
            checkboxEl.checked = true;
            checkboxEl.indeterminate = false;
            break;
        case -1:
            checkboxEl.checked = false;
            checkboxEl.indeterminate = true;
            break;
        default:
            checkboxEl.checked = false;
            checkboxEl.indeterminate = false;
            break;
    }
}

/**
 * Visits the provided urls in a new window
 * @param {any} urls
 * @returns
 */
export async function visitUrls(urls) {
    if (!Array.isArray(urls)) throw new Error('Urls must be an array of strings.');
    if (urls.length === 0) return;
    const wHandle = window.open();
    for (const url of urls) {
        wHandle.location = window.location.href.replace(window.location.pathname, url);
        await sleep(300);
    }
    wHandle.close();
}
/**
 * Copies the specified text to the clipboard
 * @param {string} text The text to copy
 * @returns
 */
export async function copyToClipboard(text) {
    await navigator.clipboard.writeText(text);
}

/**
 * Downloads the specified content as a file
 * @param {string} fileName The name of the file to download
 * @param {string} contentType The media type of the file
 * @param {string} base64 The content of the file, encoded in base64
 * @returns
 */
export function downloadFile(fileName, contentType, base64) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([bytes], { type: contentType || 'application/octet-stream' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}

/**
 * Moves the focus to the element with the specified id, if any, e.g. the first invalid field of a form (see #260)
 * @param {string} id The id of the element to focus
 */
export function focusElement(id) {
    const element = document.getElementById(id);
    if (!element) return;
    // a group, such as radio buttons or a list of inputs, is focused through its first control
    const focusable = 'input, select, textarea, button, [tabindex]';
    const target = element.matches(focusable) ? element : element.querySelector(focusable) || element;
    target.focus();
}
