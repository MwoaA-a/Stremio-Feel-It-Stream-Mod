// Feel It Stream - X-Ray Sidebar Component

const React = require('react');
const PropTypes = require('prop-types');
const classnames = require('classnames');
const { Button } = require('stremio/components');
const { default: Icon } = require('@stremio/stremio-icons/react');
const { XRAY_TABS } = require('../constants');
const useXRayData = require('../hooks/useXRayData');
const CastCard = require('./CastCard');
const MusicSection = require('./MusicSection');
const styles = require('./styles');

const TAB_LABELS = {
    [XRAY_TABS.CASTING]: 'Cast',
    [XRAY_TABS.MUSIC]: 'Music',
};

const SECTION_TITLES = {
    [XRAY_TABS.CASTING]: 'ON-SCREEN CAST',
    [XRAY_TABS.MUSIC]: 'SOUNDTRACK',
};

const XRaySidebar = React.memo(React.forwardRef(({
    className,
    metaItem,
    urlParams,
    closeSidebar,
}, ref) => {
    const [activeTab, setActiveTab] = React.useState(XRAY_TABS.CASTING);

    const id = React.useMemo(() => {
        return urlParams?.id || null;
    }, [urlParams]);

    const { cast, music, loading } = useXRayData(id);

    const onTabClick = React.useCallback((tab) => {
        setActiveTab(tab);
    }, []);

    const onCloseClick = React.useCallback(() => {
        if (typeof closeSidebar === 'function') {
            closeSidebar();
        }
    }, [closeSidebar]);

    const renderContent = React.useCallback(() => {
        if (loading) {
            return (
                <div className={styles['section-block']}>
                    <span className={styles['section-label']}>{SECTION_TITLES[activeTab]}</span>
                    <div className={styles['loading-container']}>
                        <div className={styles['loading-spinner']} />
                    </div>
                </div>
            );
        }

        switch (activeTab) {
            case XRAY_TABS.CASTING:
                return (
                    <div className={styles['section-block']}>
                        <span className={styles['section-label']}>{SECTION_TITLES[XRAY_TABS.CASTING]}</span>
                        {cast.length > 0 ? (
                            <div className={styles['cards-list']}>
                                {cast.map((actor) => (
                                    <CastCard
                                        key={actor.name}
                                        name={actor.name}
                                        role={actor.role}
                                        img={actor.img}
                                    />
                                ))}
                            </div>
                        ) : (
                            <span className={styles['placeholder-text']}>Waiting for movie...</span>
                        )}
                    </div>
                );

            case XRAY_TABS.MUSIC:
                return (
                    <div className={styles['section-block']}>
                        <span className={styles['section-label']}>{SECTION_TITLES[XRAY_TABS.MUSIC]}</span>
                        {music.length > 0 ? (
                            <MusicSection tracks={music} />
                        ) : (
                            <span className={styles['placeholder-text']}>Searching Spotify...</span>
                        )}
                    </div>
                );

            default:
                return null;
        }
    }, [activeTab, cast, music, loading]);

    return (
        <div ref={ref} className={classnames(className, styles['xray-sidebar'])}>
            <div className={styles['sidebar-header']}>
                <div className={styles['header-title-row']}>
                    <span className={styles['brand-text']}>FEEL IT<span className={styles['brand-dot']}>.</span>STREAM</span>
                    <Button
                        className={styles['close-button']}
                        tabIndex={-1}
                        onClick={onCloseClick}
                    >
                        <Icon className={styles['close-icon']} name={'close'} />
                    </Button>
                </div>
                <div className={styles['tabs-container']}>
                    {Object.values(XRAY_TABS).map((tab) => (
                        <Button
                            key={tab}
                            className={classnames(styles['tab-button'], {
                                [styles['tab-active']]: activeTab === tab,
                            })}
                            tabIndex={-1}
                            onClick={() => onTabClick(tab)}
                        >
                            <span>{TAB_LABELS[tab]}</span>
                        </Button>
                    ))}
                </div>
            </div>
            <div className={styles['sidebar-content']}>
                {renderContent()}
            </div>
        </div>
    );
}));

XRaySidebar.displayName = 'XRaySidebar';

XRaySidebar.propTypes = {
    className: PropTypes.string,
    metaItem: PropTypes.object,
    urlParams: PropTypes.object,
    closeSidebar: PropTypes.func,
};

module.exports = XRaySidebar;
