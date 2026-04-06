// Feel It Stream - X-Ray Button Component

const React = require('react');
const PropTypes = require('prop-types');
const classnames = require('classnames');
const { Button } = require('stremio/components');
const styles = require('./styles');

const XRayIcon = ({ className }) => (
    <svg
        className={className}
        viewBox={'0 0 24 24'}
        fill={'none'}
        xmlns={'http://www.w3.org/2000/svg'}
    >
        {/* Scan frame corners */}
        <path
            d={'M3 8V5a2 2 0 012-2h3M21 8V5a2 2 0 00-2-2h-3M3 16v3a2 2 0 002 2h3M21 16v3a2 2 0 01-2 2h-3'}
            stroke={'currentColor'}
            strokeWidth={'1.8'}
            strokeLinecap={'round'}
            strokeLinejoin={'round'}
        />
        {/* Center scan line */}
        <line
            x1={'7'}
            y1={'12'}
            x2={'17'}
            y2={'12'}
            stroke={'currentColor'}
            strokeWidth={'1.8'}
            strokeLinecap={'round'}
            className={styles['scan-line']}
        />
        {/* Pulse dot */}
        <circle
            cx={'12'}
            cy={'12'}
            r={'2'}
            className={styles['pulse-dot']}
        />
    </svg>
);

const XRayButton = ({ className, onClick, active }) => {
    return (
        <Button
            className={classnames(className, styles['xray-button'], { [styles['active']]: active })}
            title={'Feel It Stream'}
            tabIndex={-1}
            onClick={onClick}
        >
            <XRayIcon className={styles['xray-icon']} />
        </Button>
    );
};

XRayButton.propTypes = {
    className: PropTypes.string,
    onClick: PropTypes.func,
    active: PropTypes.bool,
};

module.exports = XRayButton;
