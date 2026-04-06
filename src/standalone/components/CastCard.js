// Feel It Stream - Standalone Cast Card Component
// No dependency on CSS Modules

const React = require('react');

const PLACEHOLDER_IMAGE = 'data:image/svg+xml,' + encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">' +
    '<rect fill="#1a1a1a" width="80" height="80"/>' +
    '<circle fill="#333" cx="40" cy="30" r="14"/>' +
    '<ellipse fill="#333" cx="40" cy="70" rx="22" ry="18"/>' +
    '</svg>'
);

const CastCard = React.memo(({ name, role, img }) => {
    const [imgError, setImgError] = React.useState(false);

    const onImageError = React.useCallback(() => {
        setImgError(true);
    }, []);

    const imageSrc = imgError || !img ? PLACEHOLDER_IMAGE : img;

    return React.createElement('div', { className: 'fis-cast-card' },
        React.createElement('img', {
            className: 'fis-cast-photo',
            src: imageSrc,
            alt: name,
            onError: onImageError,
            loading: 'lazy',
        }),
        React.createElement('div', { className: 'fis-cast-info' },
            React.createElement('span', { className: 'fis-cast-name' }, name),
            React.createElement('span', { className: 'fis-cast-role' }, role)
        )
    );
});

CastCard.displayName = 'CastCard';

module.exports = CastCard;
