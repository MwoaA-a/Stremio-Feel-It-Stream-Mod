// Feel It Stream - Standalone Music Section Component
// No dependency on CSS Modules

const React = require('react');

const MusicSection = React.memo(({ tracks }) => {
    if (!Array.isArray(tracks) || tracks.length === 0) {
        return React.createElement('div', { className: 'fis-music-empty' },
            'No music available at the moment.'
        );
    }

    return React.createElement('div', { className: 'fis-music-section' },
        tracks.map((track) =>
            React.createElement('div', {
                key: track.id || track.title,
                className: 'fis-music-track',
            },
            React.createElement('div', { className: 'fis-track-info' },
                React.createElement('span', { className: 'fis-track-title' }, track.title),
                React.createElement('span', { className: 'fis-track-artist' }, track.artist)
            ),
            track.spotifyUri
                ? React.createElement('iframe', {
                    className: 'fis-spotify-embed',
                    src: `https://open.spotify.com/embed/track/${track.spotifyUri.split(':').pop()}?theme=0`,
                    width: '100%',
                    height: '80',
                    frameBorder: '0',
                    allow: 'encrypted-media',
                    loading: 'lazy',
                    title: `${track.title} - ${track.artist}`,
                })
                : null
            )
        )
    );
});

MusicSection.displayName = 'MusicSection';

module.exports = MusicSection;
