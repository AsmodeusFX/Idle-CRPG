// 在 FairyGUI 编辑器里预览自定义 Shader 效果。
// 前提：编辑器内置的 FairyGUI/Image 已用 patcher 替换为增强版（编辑器 IL2CPP 构建裁掉了 AssetBundle，无法动态加载 shader）。
// 检查器“Shader 特效”面板的结果以 JSON 写入“自定义数据”，例如 {"fx":"sweep","color":"#FFFFFFCC","width":0.15,...}
// 特效定义见 effects.ts。
import FairyEditor = CS.FairyEditor;
import FairyGUI = CS.FairyGUI;
import UnityEngine = CS.UnityEngine;
import App = CS.FairyEditor.App;
import * as fx from './effects';
import * as inspector from './inspector';
import { createLogger, errorText } from '../../shared/diag';
import { isMeshType, getMesh, Mesh } from './targets';

const logger = createLogger('shader-fx', 'ShaderFx');
const SHADER_NAME = 'FairyGUI/Image';
const FRAME_RATE_TESTING = 512; // FairyEditor.App.FrameRateFactor.Testing
const PAINTING_REQUESTOR = 64;  // 与运行时 ShaderFx.PaintingRequestorId 一致，避开 FairyGUI 内置的 1/2/4/8

interface ActiveEffect {
    effect: fx.EffectDef;
    params: Record<string, any>;
}

let disposed = false;
let appliedEffects = new Map<FairyEditor.FObject, fx.EffectDef>();
const customDataCache = new Map<string, ActiveEffect | null>();
const paintingComponents = new Set<FairyEditor.FObject>(); // 由本插件开启绘画模式的组件
let fastFrameRate = false;
let reportedError = false;
let panelApi: inspector.InspectorApi | null = null;

/** per-object MaterialPropertyBlock: shared material and batching are not affected */
function setUniforms(graphics: FairyGUI.NGraphics, uniforms: fx.Uniforms) {
    // editor.d.ts 未声明 MaterialPropertyBlock.SetVector，但编辑器运行时可用
    let propertyBlock = graphics.materialPropertyBlock as UnityEngine.MaterialPropertyBlock & { SetVector(name: string, value: UnityEngine.Vector4): void };
    for (const name in uniforms) {
        let value = uniforms[name];
        propertyBlock.SetVector(name, new UnityEngine.Vector4(value[0], value[1], value[2], value[3]));
    }
}

function getActiveEffectFromData(data: Record<string, any> | null): ActiveEffect | null {
    let effect = data && fx.BY_ID[data.fx];
    return effect ? { effect, params: fx.resolveParams(effect, data) } : null;
}

function getActiveEffectFromCustomData(customData: string): ActiveEffect | null {
    if (!customData || customData.indexOf('"fx"') < 0) return null;
    if (customDataCache.has(customData)) return customDataCache.get(customData);

    let result: ActiveEffect | null = null;
    try {
        result = getActiveEffectFromData(JSON.parse(customData));
    } catch (e) {
        // not JSON: custom data used for something else
    }
    customDataCache.set(customData, result);
    return result;
}

/** live panel state (not yet written to customData) takes precedence */
function getActiveEffect(obj: FairyEditor.FObject): ActiveEffect | null {
    let liveState = panelApi ? panelApi.override(obj) : undefined;
    return liveState === undefined ? getActiveEffectFromCustomData(obj.customData) : getActiveEffectFromData(liveState);
}

function collectTargets(component: FairyEditor.FComponent, out: FairyEditor.FObject[]) {
    let count = component.numChildren;
    for (let i = 0; i < count; i++) {
        let child = component.GetChildAt(i);
        if (isMeshType(child)) {
            out.push(child);
        } else if (child instanceof FairyEditor.FComponent) {
            out.push(child);
            collectTargets(child, out);
        }
    }
}

// 测试（预览）模式另外实例化一份组件放在 TestView 中，没有直接获取它的接口：
// 遍历显示树，用 FObject.cast 找到最外层的编辑器对象；缓存，脱离显示树时重新查找
let testRoot: FairyEditor.FComponent | null = null;
let testRootLogged = false;

function findTestRoot(): FairyEditor.FComponent | null {
    let view = App.testView;
    if (!view || !view.running) {
        testRoot = null;
        testRootLogged = false;
        return null;
    }
    if (testRoot && testRoot.displayObject && testRoot.displayObject.parent) return testRoot;

    testRoot = null;
    let queue: FairyGUI.DisplayObject[] = [view.displayObject];
    for (let visited = 0; queue.length && visited < 5000; visited++) {
        let display = queue.shift();
        let obj: FairyEditor.FObject | null = null;
        try { obj = FairyEditor.FObject.cast(display); } catch (e) { /* not an editor object */ }
        if (obj instanceof FairyEditor.FComponent) {
            testRoot = obj;
            break;
        }
        if (display instanceof FairyGUI.Container) {
            for (let i = 0; i < display.numChildren; i++) {
                queue.push(display.GetChildAt(i));
            }
        }
    }
    if (testRoot && !testRootLogged) {
        testRootLogged = true;
        logger.log('test view root: ' + testRoot.name);
    }
    return testRoot;
}

// log the mesh once per type: confirms content access works in the current editor version
const loggedTypes = new Set<string>();

/**
 * image / movieclip / loader: mesh of the displayed content (see targets.ts);
 * component: painting mode, the whole composited component is swept (painting mesh uses the patched FairyGUI/Image).
 */
function getEffectMesh(obj: FairyEditor.FObject): Mesh | null {
    if (!(obj instanceof FairyEditor.FComponent)) {
        let mesh = getMesh(obj);
        if (!loggedTypes.has(obj.objectType)) {
            loggedTypes.add(obj.objectType);
            logger.log(`mesh for ${obj.objectType}: ${mesh ? mesh.width + 'x' + mesh.height : 'unsupported content'}`);
        }
        return mesh;
    }
    let display = obj.displayObject;
    if (!display) return null;
    if (!paintingComponents.has(obj) || !display.paintingMode) {
        display.EnterPaintingMode(PAINTING_REQUESTOR, null);
        paintingComponents.add(obj);
    }
    return { graphics: display.paintingGraphics, width: obj.width, height: obj.height };
}

function turnOffEffect(obj: FairyEditor.FObject, effect: fx.EffectDef) {
    try {
        if (obj instanceof FairyEditor.FComponent) {
            let display = obj.displayObject;
            if (display && display.paintingGraphics) {
                setUniforms(display.paintingGraphics, effect.off);
            }
            if (display && paintingComponents.has(obj)) {
                display.LeavePaintingMode(PAINTING_REQUESTOR);
            }
            paintingComponents.delete(obj);
        } else {
            let mesh = getMesh(obj);
            if (mesh) {
                setUniforms(mesh.graphics, effect.off);
            }
        }
    } catch (e) {
        // object may have been disposed
        paintingComponents.delete(obj);
    }
}

function onUpdate() {
    if (disposed) return;

    try {
        if (panelApi) {
            panelApi.poll();
        }

        // collect targets: active document and test view
        let doc = App.activeDoc;
        let root = doc && doc.content;
        let targets: FairyEditor.FObject[] = [];
        if (root) {
            collectTargets(root, targets);
        }
        let testRootObj = findTestRoot();
        if (testRootObj) {
            targets.push(testRootObj);
            collectTargets(testRootObj, targets);
        }

        // apply effects
        let nextAppliedEffects = new Map<FairyEditor.FObject, fx.EffectDef>();
        for (const obj of targets) {
            let activeEffect = getActiveEffect(obj);
            if (!activeEffect) continue;

            let mesh = getEffectMesh(obj);
            if (!mesh) continue;

            setUniforms(mesh.graphics, activeEffect.effect.uniforms(activeEffect.params, mesh.width, mesh.height));
            nextAppliedEffects.set(obj, activeEffect.effect);
        }

        // turn off removed / changed effects
        for (const [obj, effect] of appliedEffects) {
            if (nextAppliedEffects.get(obj) !== effect) turnOffEffect(obj, effect);
        }
        appliedEffects = nextAppliedEffects;

        // animate: keep the editor at full frame rate while any effect is applied
        let wantFast = nextAppliedEffects.size > 0;
        if (wantFast !== fastFrameRate) {
            fastFrameRate = wantFast;
            App.SetFrameRateFactor(FRAME_RATE_TESTING, wantFast);
        }
        if (customDataCache.size > 256) {
            customDataCache.clear();
        }
    } catch (e) {
        if (!reportedError) {
            reportedError = true;
            logger.error(errorText(e));
        }
    }
}

/** log whether the built-in FairyGUI/Image is the patched version */
function logShaderPatchState() {
    try {
        let shader = UnityEngine.Shader.Find(SHADER_NAME);
        if (!shader) {
            logger.warn(SHADER_NAME + ' not found');
            return;
        }
        let patched: boolean | string = 'unknown';
        try { patched = shader.FindPropertyIndex('_SweepColor') >= 0; } catch (e) { /* API may be stripped */ }
        logger.log(SHADER_NAME + ' found, patched=' + patched);
    } catch (e) {
        logger.warn('logShaderPatchState: ' + errorText(e));
    }
}

logShaderPatchState();
App.add_onUpdate(onUpdate);
try {
    panelApi = inspector.register(logger);
} catch (e) {
    logger.error('inspector: ' + errorText(e));
}
logger.log('ready');

function onDestroy() {
    disposed = true;
    App.remove_onUpdate(onUpdate);
    try {
        if (panelApi) {
            panelApi.flush();
        }
        inspector.unregister();
    } catch (e) {
        logger.warn('inspector unregister: ' + errorText(e));
    }
    for (const [obj, effect] of appliedEffects) {
        turnOffEffect(obj, effect);
    }
    appliedEffects.clear();
    if (fastFrameRate) {
        App.SetFrameRateFactor(FRAME_RATE_TESTING, false);
    }
}

export { onDestroy };
