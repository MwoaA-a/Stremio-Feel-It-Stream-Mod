// Feel It Stream - Standalone X-Ray Button Component
// No dependency on Stremio's <Button> or CSS Modules

const React = require('react');
const { cx, FISButton } = require('../components');

const XRayIcon = ({ className }) => {
    return React.createElement('svg', {
        className,
        viewBox: '0 0 24 24',
        fill: 'none',
        xmlns: 'http://www.w3.org/2000/svg',
    },
    // Scan frame corners
    React.createElement('path', {
        d: 'M3 8V5a2 2 0 012-2h3M21 8V5a2 2 0 00-2-2h-3M3 16v3a2 2 0 002 2h3M21 16v3a2 2 0 01-2 2h-3',
        stroke: 'currentColor',
        strokeWidth: '1.8',
        strokeLinecap: 'round',
        strokeLinejoin: 'round',
    }),
    // Center scan line
    React.createElement('line', {
        x1: '7',
        y1: '12',
        x2: '17',
        y2: '12',
        stroke: 'currentColor',
        strokeWidth: '1.8',
        strokeLinecap: 'round',
        className: 'fis-scan-line',
    }),
    // Pulse dot
    React.createElement('circle', {
        cx: '12',
        cy: '12',
        r: '2',
        className: 'fis-pulse-dot',
    }));
};

const XRayButton = React.memo(({ onClick, active }) => {
    return React.createElement(FISButton, {
        className: cx('fis-xray-button', { 'fis-active': active }),
        title: 'Feel It Stream',
        tabIndex: -1,
        onClick,
    },
    React.createElement(XRayIcon, { className: 'fis-xray-icon' }));
});

XRayButton.displayName = 'XRayButton';

module.exports = XRayButton;
