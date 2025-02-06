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