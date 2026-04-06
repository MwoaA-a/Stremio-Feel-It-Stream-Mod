// Feel It Stream - X-Ray Data Hook

const React = require('react');
const { readSettings } = require('./useFeelItSettings');

const MOCK_CAST = [
    { name: 'Tim Robbins', role: 'Andy Dufresne', img: 'https://image.tmdb.org/t/p/w500/3FfJMIVwXgsIXbAT8ECBSZJAncR.jpg' },
    { name: 'Morgan Freeman', role: 'Ellis Boyd \'Red\' Redding', img: 'https://image.tmdb.org/t/p/w500/jPsLqiYGSofU4s6BjrxnefMfabb.jpg' },
    { name: 'Bob Gunton', role: 'Warden Norton', img: 'https://image.tmdb.org/t/p/w500/ulbVvuBToBN3aCGcV028hwO0MOP.jpg' },
    { name: 'William Sadler', role: 'Heywood', img: 'https://image.tmdb.org/t/p/w500/xC9sijoDnjS3oDZ5eszcGKHKAOp.jpg' },
    { name: 'Clancy Brown', role: 'Captain Byron T. Hadley', img: 'https://image.tmdb.org/t/p/w500/1JeBRNG7VS7r64V9lOvej9bZXW5.jpg' },
    { name: 'Gil Bellows', role: 'Tommy', img: 'https://image.tmdb.org/t/p/w500/eCOIv2nSGnWTHdn88NoMyNOKWyR.jpg' },
];

const MOCK_MUSIC = [];

const useXRayData = (id) => {
    const [data, setData] = React.useState({
        title: null,
        cast: [],
        music: [],
        loading: true,
        error: null,
    });

    React.useEffect(() => {
        if (!id) {
            setData((prev) => ({ ...prev, loading: false }));
            return;
        }

        let cancelled = false;

        const fetchData = async () => {
            setData((prev) => ({ ...prev, loading: true, error: null }));

            try {
                const { apiUrl } = readSettings();
                const response = await fetch(`${apiUrl}/${id}`);
                if (!cancelled) {
                    if (response.ok) {
                        const result = await response.json();
                        setData({
                            title: result.title || null,
                            cast: result.cast || [],
                            music: result.music || [],
                            loading: false,
                            error: null,
                        });
                    } else {
                        throw new Error(`API returned ${response.status}`);
                    }
                }
            } catch {
                if (!cancelled) {
                    // Fallback to mock data when API is unavailable
                    setData({
                        title: null,
                        cast: MOCK_CAST,
                        music: MOCK_MUSIC,
                        loading: false,
                        error: null,
                    });
                }
            }
        };

        fetchData();

        return () => {
            cancelled = true;
        };
    }, [id]);

    return data;
};

module.exports = useXRayData;
