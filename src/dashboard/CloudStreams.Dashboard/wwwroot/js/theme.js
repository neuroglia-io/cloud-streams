/*
 * Applies the dashboard's color theme before the first paint (see #229): the theme the user chose, remembered in the local storage, or, by default, the system's.
 * Loaded synchronously in the <head>, so that pages never flash in the wrong theme.
 */
(function () {
    const storageKey = 'cloud-streams.theme';
    const darkQuery = window.matchMedia('(prefers-color-scheme: dark)');
    let observer = null;

    /** Gets the theme the user chose: 'light', 'dark' or, by default, 'auto' */
    function getPreference() {
        try {
            const theme = window.localStorage.getItem(storageKey);
            return theme === 'light' || theme === 'dark' ? theme : 'auto';
        }
        catch {
            return 'auto';
        }
    }

    /** Applies the theme the user chose, or the system's */
    function apply() {
        const preference = getPreference();
        const theme = preference === 'auto' ? (darkQuery.matches ? 'dark' : 'light') : preference;
        document.documentElement.setAttribute('data-bs-theme', theme);
        if (window.monaco && window.monaco.editor) window.monaco.editor.setTheme(theme === 'dark' ? 'vs-dark' : 'vs');
        if (observer) observer.invokeMethodAsync('OnThemeChanged', theme);
        return theme;
    }

    window.cloudStreamsTheme = {
        /** Gets the theme the user chose: 'light', 'dark' or 'auto' */
        getPreference,
        /** Gets the theme in effect: 'light' or 'dark' */
        getTheme: () => document.documentElement.getAttribute('data-bs-theme') || 'dark',
        /** Notifies the specified .NET object, through its 'OnThemeChanged' method, whenever the theme in effect changes */
        observe: function (reference) { observer = reference; },
        /** Remembers and applies the specified theme: 'light', 'dark' or 'auto', and returns the theme in effect */
        setPreference: function (preference) {
            try {
                if (preference === 'light' || preference === 'dark') window.localStorage.setItem(storageKey, preference);
                else window.localStorage.removeItem(storageKey);
            }
            catch { }
            return apply();
        }
    };

    darkQuery.addEventListener('change', () => { if (getPreference() === 'auto') apply(); });
    apply();
})();
