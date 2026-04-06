// Feel It Stream - Music Section Component

const React = require('react');
const PropTypes = require('prop-types');
const styles = require('./MusicSection.less');

const MusicSection = ({ tracks }) => {
    if (!Array.isArray(tracks) || tracks.length === 0) {
        return (
            <div className={styles['music-empty']}>
                <span className={styles['empty-text']}>No music available at the moment.</span>
            </div>
        );
    }

    return (
        <div className={styles['music-section']}>
            {tracks.map((track) => (
                <div key={track.id} className={styles['music-track']}>
                    <div className={styles['track-info']}>
                        <span className={styles['track-title']}>{track.title}</span>
                        <span className={styles['track-artist']}>{track.artist}</span>
                    </div>
                    {track.spotifyUri ? (
                        <iframe
                            className={styles['spotify-embed']}
                            src={`https://open.spotify.com/embed/track/${track.spotifyUri.split(':').pop()}?theme=0`}
                            width={'100%'}
                            height={'80'}
                            frameBorder={'0'}
                            allow={'encrypted-media'}
                            loading={'lazy'}
                            title={`${track.title} - ${track.artist}`}
                        />
                    ) : null}
                </div>
            ))}
        </div>
    );
};

MusicSection.propTypes = {
    tracks: PropTypes.arrayOf(PropTypes.shape({
        id: PropTypes.number.isRequired,
        title: PropTypes.string.isRequired,
        artist: PropTypes.string.isRequired,
        spotifyUri: PropTypes.string,
    })),
};

module.exports = MusicSection;
