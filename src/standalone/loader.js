// Feel It Stream - Loader Script
// This lightweight script fetches and executes the full FIS bundle.
// React is bundled WITH the plugin, so no globals check needed.
//
// Installation methods:
//
// 1. Browser Console:
//   const s = document.createElement('script');
//   s.src = 'https://feel-it.stream/stremio/feelit.bundle.js';
//   document.head.appendChild(s);
//
// 2. Bookmarklet (one-click):
//   javascript:void(document.head.appendChild(Object.assign(document.createElement('script'),{src:'https://feel-it.stream/stremio/feelit.bundle.js'})))
//
// 3. Userscript (Tampermonkey/Violentmonkey):
//   // ==UserScript==
//   // @name         Feel It.Stream for Stremio
//   // @match        https://app.strem.io/*
//   // @match        https://web.stremio.com/*
//   // @grant        none
//   // @run-at       document-idle
//   // ==/UserScript==
//   const s = document.createElement('script');
//   s.src = 'https://feel-it.stream/stremio/feelit.bundle.js';
//   document.head.appendChild(s);

(function () {
    'use strict';

    var FIS_BUNDLE_URL = 'https://feel-it.stream/stremio/feelit.bundle.js';
    var FIS_LOADER_ID = 'fis-loader-script';

    // Prevent double-loading
    if (document.getElementById(FIS_LOADER_ID)) {
        console.log('[FIS] Already loaded, skipping.');
        return;
    }

    // Prevent loading outside of a browser context
    if (typeof document === 'undefined') {
        return;
    }

    var script = document.createElement('script');
    script.id = FIS_LOADER_ID;
    script.src = FIS_BUNDLE_URL;
    script.async = true;

    script.onload = function () {
        console.log('[FIS] Bundle loaded successfully.');
    };

    script.onerror = function () {
        console.error('[FIS] Failed to load bundle from ' + FIS_BUNDLE_URL);
    };

    document.head.appendChild(script);
})();
