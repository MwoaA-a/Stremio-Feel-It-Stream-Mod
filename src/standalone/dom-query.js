// Feel It Stream - DOM Query Engine
// Finds Stremio elements despite CSS Modules hashed class names
// CSS Modules format: [local]-[hash:base64:5] → prefix is always preserved

const QUERIES = {
    playerContainer: '[class*="player-container-"]',
    controlBarContainer: '[class*="control-bar-container-"]',
    controlBarButtonsContainer: '[class*="control-bar-buttons-container-"]',
    spacing: '[class*="spacing-"]',
    navBarLayer: '[class*="nav-bar-layer-"]',
    controlBarLayer: '[class*="control-bar-layer-"]',
    menuLayer: '[class*="menu-layer-"]',
    sideDrawerButtonLayer: '[class*="side-drawer-button-layer-"]',
    settingsContent: '[class*="settings-content-"]',
    sectionsContainer: '[class*="sections-container-"]',
    settingsMenu: '[class*="menu-"]:has(> [data-section])',
    settingsMenuSpacing: '[class*="menu-"]:has(> [data-section]) > [class*="spacing-"]',
    versionInfoLabel: '[class*="version-info-label-"]',
};

const query = (key, parent) => {
    const root = parent || document;
    const selector = QUERIES[key];
    if (!selector) {
        console.warn('[FIS] Unknown query key:', key);
        return null;
    }
    return root.querySelector(selector);
};

const queryAll = (key, parent) => {
    const root = parent || document;
    const selector = QUERIES[key];
    if (!selector) return [];
    return Array.from(root.querySelectorAll(selector));
};

const findSpacingInControlBar = () => {
    const buttonsContainer = query('controlBarButtonsContainer');
    if (!buttonsContainer) return null;
    const children = Array.from(buttonsContainer.children);
    return children.find((child) => {
        return child.className && child.className.toString().includes('spacing-');
    }) || null;
};

const findMenuSpacing = () => {
    const menu = query('settingsMenu');
    if (!menu) return null;
    const children = Array.from(menu.children);
    return children.find((child) => {
        return child.className && child.className.toString().includes('spacing-');
    }) || null;
};

const findFirstVersionLabel = () => {
    const menu = query('settingsMenu');
    if (!menu) return null;
    const children = Array.from(menu.children);
    return children.find((child) => {
        return child.className && child.className.toString().includes('version-info-label-');
    }) || null;
};

module.exports = {
    QUERIES,
    query,
    queryAll,
    findSpacingInControlBar,
    findMenuSpacing,
    findFirstVersionLabel,
};
