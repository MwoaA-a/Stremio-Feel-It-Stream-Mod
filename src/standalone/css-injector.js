// Feel It Stream - CSS Injector

const FIS_STYLE_ID = 'fis-styles';

const injectCSS = (cssText) => {
    if (document.getElementById(FIS_STYLE_ID)) return;

    const style = document.createElement('style');
    style.id = FIS_STYLE_ID;
    style.textContent = cssText;
    document.head.appendChild(style);
};

const removeCSS = () => {
    const el = document.getElementById(FIS_STYLE_ID);
    if (el) el.remove();
};

module.exports = { injectCSS, removeCSS };
