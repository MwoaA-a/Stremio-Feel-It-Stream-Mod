// Feel It Stream - Overlay Controller
// Prevents Stremio from hiding the player overlay (control bar, nav bar)
// when the X-Ray sidebar is open.
//
// Strategy: Toggle a 'fis-sidebar-open' class on the player container.
// CSS rules in styles.css force overlay layers visible via !important.

const { query } = require('./dom-query');

const SIDEBAR_OPEN_CLASS = 'fis-sidebar-open';

const createOverlayController = () => {
    let playerContainer = null;
    let isLocked = false;

    const findPlayer = () => {
        if (!playerContainer || !playerContainer.isConnected) {
            playerContainer = query('playerContainer');
        }
        return playerContainer;
    };

    const lock = () => {
        const el = findPlayer();
        if (el && !isLocked) {
            el.classList.add(SIDEBAR_OPEN_CLASS);
            isLocked = true;
        }
    };

    const unlock = () => {
        const el = findPlayer();
        if (el && isLocked) {
            el.classList.remove(SIDEBAR_OPEN_CLASS);
            isLocked = false;
        }
    };

    const toggle = (open) => {
        if (open) {
            lock();
        } else {
            unlock();
        }
    };

    const destroy = () => {
        unlock();
        playerContainer = null;
    };

    return { lock, unlock, toggle, destroy };
};

module.exports = { createOverlayController, SIDEBAR_OPEN_CLASS };
