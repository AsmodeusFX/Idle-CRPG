import App = CS.FairyEditor.App;
import Path = CS.System.IO.Path;
import File = CS.System.IO.File;
import Directory = CS.System.IO.Directory;

export interface Logger {
    log(msg: string): void;
    warn(msg: string): void;
    error(msg: string): void;
    trace(msg: string): void;
}

function pluginFolder(pluginName: string): string | null {
    let dir = Path.Combine(App.pluginManager.projectPluginFolder, pluginName);
    if (Directory.Exists(dir)) return dir;
    return null;
}

export function errorText(e: unknown): string {
    return String(e && ((e as Error).stack || (e as Error).message) || e);
}

export function createLogger(pluginName: string, prefix: string, traceEnabled = false): Logger {
    let diagPath: string | null = null;

    function write(level: string, msg: string) {
        // only debugs on my own machine
        if (process.env.DEV_USER != "BBBIRDER") return;

        try {
            if (!diagPath) {
                let dir = pluginFolder(pluginName);
                if (!dir) return;
                diagPath = Path.Combine(dir, 'diag.log');
            }
            File.AppendAllText(diagPath, new Date().toISOString() + ' ' + level + ' ' + msg + '\n');
        } catch (e) { }
    }

    return {
        log(msg) {
            App.consoleView.Log(`[${prefix}] ${msg}`);
            write('INFO', msg);
        },
        warn(msg) {
            App.consoleView.LogWarning(`[${prefix}] ${msg}`);
            write('WARN', msg);
        },
        error(msg) {
            App.consoleView.LogError(`[${prefix}] ${msg}`);
            write('ERROR', msg);
        },
        trace(msg) {
            if (traceEnabled) {
                write('TRACE', msg);
            }
        },
    };
}
