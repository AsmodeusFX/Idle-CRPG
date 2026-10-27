import App = CS.FairyEditor.App;
import * as panel from './panel';
import { createLogger, errorText } from '../../shared/diag';

const TRACE = false;
const logger = createLogger('spine-loader', 'Loader3D', TRACE);

let disposed = false;
let panelApi: panel.PanelApi | null = null;
let reportedError = false;

function onUpdate() {
    if (disposed || !panelApi) return;

    try {
        panelApi.poll();
    } catch (e) {
        if (!reportedError) {
            reportedError = true;
            logger.error(errorText(e));
        }
    }
}

function onDestroy() {
    disposed = true;
    App.remove_onUpdate(onUpdate);
    try {
        panel.unregister();
    } catch (e) {
        logger.warn('unregister: ' + errorText(e));
    }
}

try {
    panelApi = panel.register(logger);
} catch (e) {
    logger.error('register: ' + errorText(e));
}

App.add_onUpdate(onUpdate);
logger.log('ready');

export { onDestroy };
