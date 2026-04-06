// Feel It Stream - Standalone Settings Component
// No dependency on Stremio's <Section>, <Option>, <Button>, <TextInput>

const React = require('react');
const { FISButton, FISTextInput, CheckmarkIcon } = require('../components');
const useFeelItSettings = require('../../hooks/useFeelItSettings');

const FeelItSettings = React.memo(() => {
    const [settings, updateSettings, modVersion] = useFeelItSettings();
    const [urlInput, setUrlInput] = React.useState(settings.apiUrl);
    const [saved, setSaved] = React.useState(false);

    const onUrlChange = React.useCallback((event) => {
        setUrlInput(event.target.value);
        setSaved(false);
    }, []);

    const onUrlSave = React.useCallback(() => {
        updateSettings({ apiUrl: urlInput });
        setSaved(true);
        setTimeout(() => setSaved(false), 2000);
    }, [urlInput, updateSettings]);

    return React.createElement('div', { className: 'fis-settings-section' },
        React.createElement('div', { className: 'fis-settings-title' }, 'Feel It.Stream'),
        // API URL option
        React.createElement('div', { className: 'fis-settings-option' },
            React.createElement('span', { className: 'fis-settings-option-label' }, 'API URL'),
            React.createElement('div', { className: 'fis-url-input-row' },
                React.createElement(FISTextInput, {
                    className: 'fis-url-input',
                    value: urlInput,
                    onChange: onUrlChange,
                    onSubmit: onUrlSave,
                    placeholder: 'http://localhost:3000/api/v2/xray',
                }),
                React.createElement(FISButton, {
                    className: 'fis-save-button',
                    onClick: onUrlSave,
                },
                React.createElement(CheckmarkIcon, {
                    className: 'fis-save-icon',
                    style: { width: '1rem', height: '1rem' },
                }),
                React.createElement('span', null, saved ? 'OK' : 'Save'))
            )
        ),
        // Version
        React.createElement('div', { className: 'fis-settings-option' },
            React.createElement('span', { className: 'fis-settings-option-label' }, 'Mod Version'),
            React.createElement('span', { className: 'fis-version-label' },
                'Feel It.Stream v' + modVersion
            )
        )
    );
});

FeelItSettings.displayName = 'FeelItSettings';

module.exports = FeelItSettings;
