// Feel It Stream - Standalone X-Ray Sidebar Component
// No dependency on Stremio's <Button>, <Icon>, or CSS Modules

const React = require('react');
const { cx, FISButton, CloseIcon } = require('../components');
const { XRAY_TABS } = require('../../constants');
const useXRayData = require('../../hooks/useXRayData');
const CastCard = require('./CastCard');
const MusicSection = require('./MusicSection');

const TAB_LABELS = {
    [XRAY_TABS.CASTING]: 'Cast',
    [XRAY_TABS.MUSIC]: 'Music',
};

const SECTION_TITLES = {
    [XRAY_TABS.CASTING]: 'ON-SCREEN CAST',
    [XRAY_TABS.MUSIC]: 'SOUNDTRACK',
};

const XRaySidebar = React.memo(({ id, onClose }) => {
    const [activeTab, setActiveTab] = React.useState(XRAY_TABS.CASTING);
    const { cast, music, loading } = useXRayData(id);

    const onTabClick = React.useCallback((tab) => {
        setActiveTab(tab);
    }, []);

    const onCloseClick = React.useCallback(() => {
        if (typeof onClose === 'function') {
            onClose();
        }
    }, [onClose]);

    const renderLoading = () => {
        return React.createElement('div', { className: 'fis-section-block' },
            React.createElement('span', { className: 'fis-section-label' }, SECTION_TITLES[activeTab]),
            React.createElement('div', { className: 'fis-loading-container' },
                React.createElement('div', { className: 'fis-loading-spinner' })
            )
        );
    };

    const renderCasting = () => {
        return React.createElement('div', { className: 'fis-section-block' },
            React.createElement('span', { className: 'fis-section-label' }, SECTION_TITLES[XRAY_TABS.CASTING]),
            cast.length > 0
                ? React.createElement('div', { className: 'fis-cards-list' },
                    cast.map((actor) =>
                        React.createElement(CastCard, {
                            key: actor.name,
                            name: actor.name,
                            role: actor.role,
                            img: actor.img,
                        })
                    )
                )
                : React.createElement('span', { className: 'fis-placeholder-text' }, 'Waiting for movie...')
        );
    };

    const renderMusic = () => {
        return React.createElement('div', { className: 'fis-section-block' },
            React.createElement('span', { className: 'fis-section-label' }, SECTION_TITLES[XRAY_TABS.MUSIC]),
            music.length > 0
                ? React.createElement(MusicSection, { tracks: music })
                : React.createElement('span', { className: 'fis-placeholder-text' }, 'Searching Spotify...')
        );
    };

    const renderContent = () => {
        if (loading) return renderLoading();
        switch (activeTab) {
            case XRAY_TABS.CASTING: return renderCasting();
            case XRAY_TABS.MUSIC: return renderMusic();
            default: return null;
        }
    };

    // Visibility is controlled entirely by DOM classList in player-injector.js
    // (never via React props — avoids React 18 async scheduler race conditions)
    return React.createElement('div', {
        className: 'fis-xray-sidebar',
    },
    // Header
    React.createElement('div', { className: 'fis-sidebar-header' },
        // Title row
        React.createElement('div', { className: 'fis-header-title-row' },
            React.createElement('span', { className: 'fis-brand-text' },
                'FEEL IT',
                React.createElement('span', { className: 'fis-brand-dot' }, '.'),
                'STREAM'
            ),
            React.createElement(FISButton, {
                className: 'fis-close-button',
                tabIndex: -1,
                onClick: onCloseClick,
            },
            React.createElement(CloseIcon, { className: 'fis-close-icon' }))
        ),
        // Tabs
        React.createElement('div', { className: 'fis-tabs-container' },
            Object.values(XRAY_TABS).map((tab) =>
                React.createElement(FISButton, {
                    key: tab,
                    className: cx('fis-tab-button', { 'fis-tab-active': activeTab === tab }),
                    tabIndex: -1,
                    onClick: () => onTabClick(tab),
                },
                React.createElement('span', null, TAB_LABELS[tab]))
            )
        )
    ),
    // Content
    React.createElement('div', { className: 'fis-sidebar-content' },
        renderContent()
    ));
});

XRaySidebar.displayName = 'XRaySidebar';

module.exports = XRaySidebar;
