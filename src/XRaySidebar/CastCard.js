// Feel It Stream - Cast Card Component

const React = require('react');
const PropTypes = require('prop-types');
const classnames = require('classnames');
const styles = require('./CastCard.less');

const PLACEHOLDER_IMAGE = 'data:image/svg+xml,' + encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">' +
    '<rect fill="#1a1a1a" width="80" height="80"/>' +
    '<circle fill="#333" cx="40" cy="30" r="14"/>' +
    '<ellipse fill="#333" cx="40" cy="70" rx="22" ry="18"/>' +
    '</svg>'
);

const CastCard = ({ name, role, img }) => {
    const [imgError, setImgError] = React.useState(false);

    const onImageError = React.useCallback(() => {
        setImgError(true);
    }, []);

    const imageSrc = imgError || !img ? PLACEHOLDER_IMAGE : img;

    return (
        <div className={styles['cast-card']}>
            <img
                className={styles['cast-photo']}
                src={imageSrc}
                alt={name}
                onError={onImageError}
                loading={'lazy'}
            />
            <div className={styles['cast-info']}>
                <span className={styles['cast-name']}>{name}</span>
                <span className={styles['cast-role']}>{role}</span>
            </div>
        </div>
    );
};

CastCard.propTypes = {
    name: PropTypes.string.isRequired,
    role: PropTypes.string,
    img: PropTypes.string,
};

module.exports = CastCard;
