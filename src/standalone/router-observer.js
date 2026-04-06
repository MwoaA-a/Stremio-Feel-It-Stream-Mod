// Feel It Stream - Router Observer
// Detects Stremio page changes via hash-based routing

const ROUTES = {
    PLAYER: 'player',
    SETTINGS: 'settings',
    OTHER: 'other',
};

// /#/player/{stream}/{streamTransportUrl}/{metaTransportUrl}/{type}/{id}/{videoId}
const PLAYER_REGEXP = /^#\/player\/([^/]*)(?:\/([^/]*)\/([^/]*)\/([^/]*)\/([^/]*)\/([^/]*))?$/;
const SETTINGS_REGEXP = /^#\/settings/;

const parseHash = (hash) => {
    const playerMatch = hash.match(PLAYER_REGEXP);
    if (playerMatch) {
        return {
            route: ROUTES.PLAYER,
            params: {
                stream: playerMatch[1] || null,
                streamTransportUrl: playerMatch[2] || null,
                metaTransportUrl: playerMatch[3] || null,
                type: playerMatch[4] || null,
                id: playerMatch[5] || null,
                videoId: playerMatch[6] || null,
            },
        };
    }

    if (SETTINGS_REGEXP.test(hash)) {
        return { route: ROUTES.SETTINGS, params: {} };
    }

    return { route: ROUTES.OTHER, params: {} };
};

const createRouterObserver = () => {
    const listeners = [];
    let current = parseHash(window.location.hash);

    const notify = () => {
        // Snapshot to avoid mutation-during-iteration if a listener unsubscribes
        const snapshot = listeners.slice();
        snapshot.forEach((fn) => {
            try {
                fn(current);
            } catch (err) {
                console.error('[FIS] Router listener error:', err);
            }
        });
    };

    const onHashChange = () => {
        const next = parseHash(window.location.hash);
        const changed = next.route !== current.route
            || next.params.id !== current.params.id
            || next.params.videoId !== current.params.videoId;
        if (changed) {
            current = next;
            notify();
        }
    };

    window.addEventListener('hashchange', onHashChange);

    return {
        getCurrent: () => current,
        onChange: (fn) => {
            listeners.push(fn);
            return () => {
                const idx = listeners.indexOf(fn);
                if (idx !== -1) listeners.splice(idx, 1);
            };
        },
        destroy: () => {
            window.removeEventListener('hashchange', onHashChange);
            listeners.length = 0;
        },
        ROUTES,
    };
};

module.exports = { createRouterObserver, parseHash, ROUTES };
