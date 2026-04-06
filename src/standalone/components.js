// Feel It Stream - Standalone Component Primitives
// Replaces Stremio's <Button>, <Icon>, <TextInput>, classnames, etc.
// Uses plain HTML elements + fis- prefixed CSS classes

const React = require('react');

/**
 * Lightweight classnames replacement.
 * @param {...(string|object|undefined|null|false)} args
 * @returns {string}
 */
const cx = (...args) => {
    const classes = [];
    for (const arg of args) {
        if (!arg) continue;
        if (typeof arg === 'string') {
            classes.push(arg);
        } else if (typeof arg === 'object') {
            for (const key of Object.keys(arg)) {
                if (arg[key]) classes.push(key);
            }
        }
    }
    return classes.join(' ');
};

/**
 * FISButton - Replaces Stremio's <Button> component.
 * A simple clickable div with optional className, title, tabIndex.
 */
const FISButton = React.forwardRef(({ className, title, tabIndex, onClick, children, ...rest }, ref) => {
    const onKeyDown = React.useCallback((e) => {
        if ((e.key === 'Enter' || e.key === ' ') && typeof onClick === 'function') {
            e.preventDefault();
            onClick(e);
        }
    }, [onClick]);

    return React.createElement('div', {
        ref,
        className: className || undefined,
        title: title || undefined,
        tabIndex: tabIndex != null ? tabIndex : 0,
        role: 'button',
        onClick,
        onKeyDown,
        ...rest,
    }, children);
});

FISButton.displayName = 'FISButton';

/**
 * FISTextInput - Replaces Stremio's <TextInput>.
 * A styled <input type="text">.
 */
const FISTextInput = React.forwardRef(({ className, value, onChange, onSubmit, placeholder, ...rest }, ref) => {
    const onKeyDown = React.useCallback((e) => {
        if (e.key === 'Enter' && typeof onSubmit === 'function') {
            onSubmit(e);
        }
    }, [onSubmit]);

    return React.createElement('input', {
        ref,
        type: 'text',
        className: className || undefined,
        value: value || '',
        onChange,
        onKeyDown,
        placeholder: placeholder || '',
        ...rest,
    });
});

FISTextInput.displayName = 'FISTextInput';

/**
 * Close icon SVG (X mark) - Replaces Stremio's <Icon name="close" />.
 */
const CloseIcon = ({ className }) => {
    return React.createElement('svg', {
        className,
        viewBox: '0 0 24 24',
        fill: 'none',
        xmlns: 'http://www.w3.org/2000/svg',
    },
    React.createElement('path', {
        d: 'M18 6L6 18M6 6l12 12',
        stroke: 'currentColor',
        strokeWidth: '2',
        strokeLinecap: 'round',
        strokeLinejoin: 'round',
    }));
};

/**
 * Checkmark icon SVG - Replaces Stremio's <Icon name="checkmark" />.
 */
const CheckmarkIcon = ({ className }) => {
    return React.createElement('svg', {
        className,
        viewBox: '0 0 24 24',
        fill: 'none',
        xmlns: 'http://www.w3.org/2000/svg',
    },
    React.createElement('path', {
        d: 'M20 6L9 17l-5-5',
        stroke: 'currentColor',
        strokeWidth: '2',
        strokeLinecap: 'round',
        strokeLinejoin: 'round',
    }));
};

module.exports = {
    cx,
    FISButton,
    FISTextInput,
    CloseIcon,
    CheckmarkIcon,
};
