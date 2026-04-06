// Feel It Stream - Bootstrap Entry Point
// Master orchestrator that wires up all standalone modules.
// This is the single entry point compiled by webpack into the IIFE bundle.
//
// Lifecycle:
//   1. Inject CSS stylesheet
//   2. Start router observer
//   3. On PLAYER route  → inject player (button + sidebar)
//   4. On SETTINGS route → inject settings panel
//   5. On OTHER route    → eject both
//   6. On destroy        → clean up everything

const { injectCSS, removeCSS } = require('./css-injector');
const { createRouterObserver, ROUTES } = require('./router-observer');
const { createPlayerInjector } = require('./injectors/player-injector');
const { createSettingsInjector } = require('./injectors/settings-injector');
const FIS_STYLES = require('./styles.css');

const FIS_VERSION = '1.0.0';

const bootstrap = () => {
    // Guard against double bootstrap — set sentinel IMMEDIATELY to block
    // any concurrent call before side effects run
    if (window.__FIS__) {
        console.log('[FIS] Already loaded, skipping duplicate bootstrap');
        return;
    }
    window.__FIS__ = { version: FIS_VERSION };

    // 1. Inject CSS
    injectCSS(FIS_STYLES);

    // 2. Router observer
    const router = createRouterObserver();

    // 3. Injectors
    const playerInjector = createPlayerInjector();
    const settingsInjector = createSettingsInjector();

    // 4. Route handler
    const handleRoute = (state) => {
        switch (state.route) {
            case ROUTES.PLAYER: {
                settingsInjector.eject();
                const id = state.params.id || null;
                playerInjector.inject(id);
                break;
            }
            case ROUTES.SETTINGS: {
                playerInjector.eject();
                settingsInjector.inject();
                break;
            }
            default: {
                playerInjector.eject();
                settingsInjector.eject();
                break;
            }
        }
    };

    // Handle current route on load
    handleRoute(router.getCurrent());

    // Listen for future route changes
    const unsubscribe = router.onChange(handleRoute);

    // 5. Destroy function for full cleanup
    const destroy = () => {
        unsubscribe();
        playerInjector.destroy();
        settingsInjector.destroy();
        router.destroy();
        removeCSS();
    };

    // Complete the sentinel with destroy and router references
    window.__FIS__.destroy = destroy;
    window.__FIS__.router = router;

    // eslint-disable-next-line no-console
    console.log(`[FIS] Feel It.Stream v${FIS_VERSION} loaded`);

    return { destroy };
};

// Auto-bootstrap when DOM is ready
if (typeof document !== 'undefined') {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bootstrap);
    } else {
        bootstrap();
    }
}

module.exports = { bootstrap };
