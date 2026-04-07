// Feel It Stream - Player Injector
// Mounts XRayButton (pure DOM) and XRaySidebar into the Stremio player.
// Uses MutationObserver to react to DOM changes.
//
// Architecture:
//   Button → pure DOM (no React, avoids React 18 scheduler issues in WebView2)
//   Sidebar → React rendered ONCE, visibility toggled via CSS classes
//
// React 18's scheduler fails to flush re-renders in Stremio's WebView2.
// We work around this by rendering the sidebar once on mount and using
// plain DOM class toggles for show/hide, avoiding React re-render entirely.

const React = require('react');
const ReactDOM = require('react-dom');
const { query, findSpacingInControlBar } = require('../dom-query');
const { createOverlayController } = require('../overlay-controller');
const XRaySidebar = require('../components/XRaySidebar');

const BUTTON_ANCHOR_ID = 'fis-button-root';
const SIDEBAR_ANCHOR_ID = 'fis-sidebar-root';

// SVG icon for the X-Ray button (scan frame + center line + pulse dot)
const XRAY_BUTTON_SVG = [
    '<svg class="fis-xray-icon" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">',
    '<path d="M3 8V5a2 2 0 012-2h3M21 8V5a2 2 0 00-2-2h-3M3 16v3a2 2 0 002 2h3M21 16v3a2 2 0 01-2 2h-3"',
    ' stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>',
    '<line x1="7" y1="12" x2="17" y2="12" stroke="currentColor" stroke-width="1.8"',
    ' stroke-linecap="round" class="fis-scan-line"/>',
    '<circle cx="12" cy="12" r="2" class="fis-pulse-dot"/>',
    '</svg>',
].join('');

const createPlayerInjector = () => {
    let buttonEl = null;
    let sidebarAnchor = null;
    let sidebarInner = null; // The .fis-xray-sidebar div rendered by React
    let overlayCtrl = null;
    let observer = null;
    let currentId = null;
    let mounted = false;
    let sidebarOpen = false;

    // --- Toggle sidebar visibility (pure DOM, no React re-render) ---

    const setSidebarVisible = (visible) => {
        sidebarOpen = visible;

        // Button active state
        if (buttonEl) {
            if (visible) {
                buttonEl.classList.add('fis-active');
            } else {
                buttonEl.classList.remove('fis-active');
            }
        }

        // Sidebar visibility via CSS class
        if (sidebarAnchor) {
            // Toggle pointer-events on root
            if (visible) {
                sidebarAnchor.classList.add('fis-open');
            } else {
                sidebarAnchor.classList.remove('fis-open');
            }

            // Find the inner sidebar div (rendered by React)
            if (!sidebarInner) {
                sidebarInner = sidebarAnchor.querySelector('.fis-xray-sidebar');
            }
            if (sidebarInner) {
                if (visible) {
                    sidebarInner.classList.add('fis-sidebar-visible');
                } else {
                    sidebarInner.classList.remove('fis-sidebar-visible');
                }
            }
        }

        // Overlay controller (keep control bar visible when sidebar is open)
        if (overlayCtrl) {
            overlayCtrl.toggle(visible);
        }
    };

    // --- Click outside to close (standard UX pattern) ---

    const onDocumentMouseDown = (e) => {
        if (!sidebarOpen) return;
        // Ignore clicks on the sidebar itself or the toggle button
        if (sidebarAnchor && sidebarAnchor.contains(e.target)) return;
        if (buttonEl && buttonEl.contains(e.target)) return;
        setSidebarVisible(false);
    };

    // --- Button (Pure DOM) ---

    const createButton = () => {
        const btn = document.createElement('div');
        btn.id = BUTTON_ANCHOR_ID;
        btn.className = 'fis-xray-button';
        btn.title = 'Feel It Stream';
        btn.tabIndex = -1;
        btn.setAttribute('role', 'button');
        btn.innerHTML = XRAY_BUTTON_SVG;
        btn.addEventListener('click', onButtonClick);
        return btn;
    };

    const onButtonClick = () => {
        setSidebarVisible(!sidebarOpen);
    };

    // --- Sidebar (React rendered once, then CSS-toggled) ---

    const renderSidebar = () => {
        if (!sidebarAnchor) return;
        try {
            // Expose close function on the anchor for the React component to call
            sidebarAnchor.__fisClose = () => setSidebarVisible(false);

            ReactDOM.render(
                React.createElement(XRaySidebar, {
                    id: currentId,
                    onClose: () => {
                        // This closure is captured once. Use the anchor's __fisClose
                        // to get the current close function.
                        if (sidebarAnchor && sidebarAnchor.__fisClose) {
                            sidebarAnchor.__fisClose();
                        }
                    },
                }),
                sidebarAnchor
            );
        } catch (err) {
            console.error('[FIS] Sidebar render error:', err);
        }
    };

    // --- Mount / Unmount ---

    const tryMount = () => {
        if (mounted) return true;

        const controlBarButtons = query('controlBarButtonsContainer');
        const playerContainer = query('playerContainer');

        if (!controlBarButtons || !playerContainer) {
            return false;
        }

        // Find the spacing div in the control bar to insert button AFTER it
        // (right side, with the other icon buttons like subtitles, cast, etc.)
        const spacing = findSpacingInControlBar();

        // Create button in control bar (pure DOM)
        buttonEl = createButton();

        if (spacing && spacing.nextSibling) {
            controlBarButtons.insertBefore(buttonEl, spacing.nextSibling);
        } else {
            controlBarButtons.appendChild(buttonEl);
        }

        // Create sidebar anchor as child of player container
        sidebarAnchor = document.createElement('div');
        sidebarAnchor.id = SIDEBAR_ANCHOR_ID;
        sidebarAnchor.className = 'fis-sidebar-root';
        playerContainer.appendChild(sidebarAnchor);

        // Create overlay controller
        overlayCtrl = createOverlayController();

        // Render sidebar once (visibility controlled entirely via DOM classList,
        // never via React props — avoids React 18 async scheduler race conditions)
        sidebarOpen = false;
        sidebarInner = null;
        renderSidebar();

        // Click outside sidebar → close it (closes when user opens episode list, etc.)
        document.addEventListener('mousedown', onDocumentMouseDown, true);

        mounted = true;
        return true;
    };

    const unmount = () => {
        document.removeEventListener('mousedown', onDocumentMouseDown, true);
        if (sidebarAnchor) {
            ReactDOM.unmountComponentAtNode(sidebarAnchor);
        }
        if (overlayCtrl) {
            overlayCtrl.destroy();
            overlayCtrl = null;
        }
        if (buttonEl && buttonEl.parentNode) {
            buttonEl.removeEventListener('click', onButtonClick);
            buttonEl.parentNode.removeChild(buttonEl);
            buttonEl = null;
        }
        if (sidebarAnchor && sidebarAnchor.parentNode) {
            sidebarAnchor.parentNode.removeChild(sidebarAnchor);
            sidebarAnchor = null;
        }
        sidebarInner = null;
        sidebarOpen = false;
        mounted = false;
    };

    const inject = (id) => {
        currentId = id;
        if (mounted) {
            // Properly close sidebar (cleans up all CSS classes + overlay state)
            setSidebarVisible(false);
            sidebarInner = null;
            renderSidebar();
            return;
        }

        // Try immediate mount
        if (tryMount()) return;

        // Watch for DOM changes until control bar appears
        if (observer) observer.disconnect();
        const obs = new MutationObserver(() => {
            if (tryMount()) {
                obs.disconnect();
                if (observer === obs) observer = null;
            }
        });
        observer = obs;
        observer.observe(document.body, { childList: true, subtree: true });
    };

    const eject = () => {
        if (observer) {
            observer.disconnect();
            observer = null;
        }
        unmount();
        currentId = null;
    };

    const destroy = () => {
        eject();
    };

    return { inject, eject, destroy };
};

module.exports = { createPlayerInjector };
