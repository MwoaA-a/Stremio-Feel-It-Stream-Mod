// Feel It Stream - Settings Section Component

const React = require('react');
const { Button, TextInput } = require('stremio/components');
const { default: Icon } = require('@stremio/stremio-icons/react');
const { Section, Option } = require('stremio/routes/Settings/components');
const useFeelItSettings = require('../hooks/useFeelItSettings');
const styles = require('./styles');

const FeelItSettings = React.forwardRef((props, ref) => {
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

    return (
        <Section ref={ref} label={'Feel It.Stream'}>
            <Option label={'API URL'}>
                <div className={styles['url-input-row']}>
                    <TextInput
                        className={styles['url-input']}
                        value={urlInput}
                        onChange={onUrlChange}
                        onSubmit={onUrlSave}
                        placeholder={'http://localhost:3000/api/v2/xray'}
                    />
                    <Button className={styles['save-button']} onClick={onUrlSave}>
                        <Icon name={saved ? 'checkmark' : 'checkmark'} className={styles['save-icon']} />
                        <span className={styles['save-label']}>{saved ? 'OK' : 'Save'}</span>
                    </Button>
                </div>
            </Option>
            <Option label={'Mod Version'}>
                <div className={styles['version-label']}>
                    {'Feel It.Stream v' + modVersion}
                </div>
            </Option>
        </Section>
    );
});

FeelItSettings.displayName = 'FeelItSettings';

module.exports = FeelItSettings;
