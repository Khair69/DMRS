// Localization helpers for DMRS.
// Persists the chosen language in localStorage and flips the document direction
// (and the active Bootstrap stylesheet) between LTR and RTL.
window.dmrsLocalization = {
    storageKey: 'dmrs-language',

    get: function () {
        try {
            return window.localStorage.getItem(this.storageKey);
        } catch {
            return null;
        }
    },

    set: function (language) {
        try {
            window.localStorage.setItem(this.storageKey, language);
        } catch {
            // Ignore storage failures (e.g. private mode); language still applies for the session.
        }
    },

    // Applies <html lang/dir> and swaps Bootstrap to its RTL build when needed.
    apply: function (language, rtl) {
        const html = document.documentElement;
        html.setAttribute('lang', language);
        html.setAttribute('dir', rtl ? 'rtl' : 'ltr');

        const bootstrap = document.getElementById('bootstrap-stylesheet');
        if (bootstrap) {
            const href = rtl
                ? 'css/lib/bootstrap/css/bootstrap.rtl.css'
                : 'css/lib/bootstrap/css/bootstrap.css';
            if (!bootstrap.getAttribute('href').endsWith(rtl ? 'bootstrap.rtl.css' : 'bootstrap.css')) {
                bootstrap.setAttribute('href', href);
            }
        }
    }
};
