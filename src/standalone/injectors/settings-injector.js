// Feel It Stream - Settings Injector
// Injects a "Feel It.Stream" menu button into Stremio's Settings page
// and renders the FeelItSettings panel in the content area.

const React = require('react');
const ReactDOM = require('react-dom');
const { query, findMenuSpacing, findFirstVersionLabel } = require('../dom-query');
const FeelItSettings = require('../components/FeelItSettings');
const { MOD_VERSION } = require('../../hooks/useFeelItSettings');

const MENU_BUTTON_ID = 'fis-settings-menu-btn';
const SETTINGS_ROOT_ID = 'fis-settings-root';
const SECTION_DATA_ATTR = 'feelit';

const createSettingsInjector = () => {
    let menuButton = null;
    let settingsRoot = null;
    let reactRoot = null;
    let observer = null;
    let mounted = false;

    const createMenuButton = () => {
        const btn = document.createElement('div');
        btn.id = MENU_BUTTON_ID;
        btn.className = 'fis-settings-menu-button';
        btn.setAttribute('data-section', SECTION_DATA_ATTR);
        btn.innerHTML = 'Feel It<span class="fis-settings-menu-dot">.</span>Stream';
        btn.addEventListener('click', onMenuClick);
        return btn;
    };

    const createVersionLabel = () => {
        const label = document.createElement('div');
        label.className = 'fis-settings-version-label';
        label.textContent = `Feel It.Stream v${MOD_VERSION}`;
        return label;
    };

    const onMenuClick = () => {
        // Deselect all existing Stremio menu buttons
        const settingsMenu = query('settingsMenu');
        if (settingsMenu) {
            const buttons = Array.from(settingsMenu.querySelectorAll('[data-section]'));
            buttons.forEach((btn) => {
                if (btn.id !== MENU_BUTTON_ID) {
                    // Remove Stremio's "selected" class (CSS Modules hashed)
                    const classes = Array.from(btn.classList);
                    const selectedClass = classes.find((c) => c.includes('selected-'));
                    if (selectedClass) btn.classList.remove(selectedClass);
                }
            });
        }

        // Toggle own selected state
        if (menuButton) {
            menuButton.classList.add('fis-selected');
        }

        // Show FIS settings panel, hide Stremio sections
        showSettingsPanel();
    };

    const showSettingsPanel = () => {
        const sectionsContainer = query('sectionsContainer');
        if (!sectionsContainer) return;

        // Hide all existing section children
        Array.from(sectionsContainer.children).forEach((child) => {
            if (child.id !== SETTINGS_ROOT_ID) {
                child.style.display = 'none';
            }
        });

        // Show or create FIS settings root
        if (!settingsRoot || !settingsRoot.isConnected) {
            settingsRoot = document.createElement('div');
            settingsRoot.id = SETTINGS_ROOT_ID;
            sectionsContainer.appendChild(settingsRoot);

            reactRoot = settingsRoot;
        }

        settingsRoot.style.display = '';
        ReactDOM.render(React.createElement(FeelItSettings), reactRoot);
    };

    const hideSettingsPanel = () => {
        if (settingsRoot) {
            settingsRoot.style.display = 'none';
        }
        if (menuButton) {
            menuButton.classList.remove('fis-selected');
        }
    };

    const watchStremioMenuClicks = () => {
        // Listen for clicks on Stremio's native settings menu buttons
        const settingsMenu = query('settingsMenu');
        if (!settingsMenu) return;

        const nativeButtons = Array.from(
            settingsMenu.querySelectorAll('[data-section]')
        ).filter((btn) => btn.id !== MENU_BUTTON_ID);

        nativeButtons.forEach((btn) => {
            btn.addEventListener('click', () => {
                hideSettingsPanel();

                // Restore visibility of Stremio sections
                const sectionsContainer = query('sectionsContainer');
                if (sectionsContainer) {
                    Array.from(sectionsContainer.children).forEach((child) => {
                        if (child.id !== SETTINGS_ROOT_ID) {
                            child.style.display = '';
                        }
                    });
                }
            });
        });
    };

    const tryMount = () => {
        if (mounted) return true;

        const settingsMenu = query('settingsMenu');
        if (!settingsMenu) return false;

        // Insert menu button before the spacing div
        const spacing = findMenuSpacing();
        menuButton = createMenuButton();

        if (spacing) {
            settingsMenu.insertBefore(menuButton, spacing);
        } else {
            settingsMenu.appendChild(menuButton);
        }

        // Insert version label after the first existing version label
        const existingVersionLabel = findFirstVersionLabel();
        if (existingVersionLabel && existingVersionLabel.parentNode) {
            const versionLabel = createVersionLabel();
            existingVersionLabel.parentNode.insertBefore(
                versionLabel,
                existingVersionLabel.nextSibling
            );
        }

        // Watch for native menu button clicks
        watchStremioMenuClicks();

        mounted = true;
        return true;
    };

    const inject = () => {
        if (tryMount()) return;

        // Observe DOM until settings menu appears
        if (observer) observer.disconnect();
        observer = new MutationObserver(() => {
            if (tryMount()) {
                observer.disconnect();
                observer = null;
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    };

    const eject = () => {
        if (observer) {
            observer.disconnect();
            observer = null;
        }
        if (reactRoot) {
            ReactDOM.unmountComponentAtNode(reactRoot);
            reactRoot = null;
        }
        if (menuButton && menuButton.parentNode) {
            menuButton.parentNode.removeChild(menuButton);
            menuButton = null;
        }
        if (settingsRoot && settingsRoot.parentNode) {
            settingsRoot.parentNode.removeChild(settingsRoot);
            settingsRoot = null;
        }
        mounted = false;
    };

    const destroy = () => {
        eject();
    };

    return { inject, eject, destroy };
};

module.exports = { createSettingsInjector };
