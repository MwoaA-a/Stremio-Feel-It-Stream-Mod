// Feel It Stream - Settings Persistence Hook (localStorage)

const React = require('react');
const { XRAY_API_BASE_URL } = require('../constants');

const STORAGE_KEY = 'feelit_settings';
const MOD_VERSION = '1.0.1';

const DEFAULT_SETTINGS = {
    apiUrl: XRAY_API_BASE_URL,
};

const readSettings = () => {
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            return { ...DEFAULT_SETTINGS, ...JSON.parse(stored) };
        }
    } catch {
        // ignore parse errors
    }
    return { ...DEFAULT_SETTINGS };
};

const writeSettings = (settings) => {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
    } catch {
        // ignore write errors
    }
};

const useFeelItSettings = () => {
    const [settings, setSettings] = React.useState(readSettings);

    const updateSettings = React.useCallback((patch) => {
        setSettings((prev) => {
            const next = { ...prev, ...patch };
            writeSettings(next);
            return next;
        });
    }, []);

    return [settings, updateSettings, MOD_VERSION];
};

module.exports = useFeelItSettings;
module.exports.MOD_VERSION = MOD_VERSION;
module.exports.readSettings = readSettings;
