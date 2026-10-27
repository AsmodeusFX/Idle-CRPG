import FairyEditor = CS.FairyEditor;
import FairyGUI = CS.FairyGUI;
import UnityEngine = CS.UnityEngine;
import App = CS.FairyEditor.App;
import * as fx from './effects';
import { toArray, toStringArray } from '../../shared/cs';
import { inputText, isEditing, parseCustomData } from '../../shared/ui';
import type { Logger } from '../../shared/diag';
import { getMesh } from './targets';
import { describeInspectorList, placeSectionBefore } from '../../shared/inspectorOrder';
import { colorToHex, parseColor } from '../../shared/color';

const INSPECTOR_NAME = 'ShaderFx';
const URL_COMBO = 'ui://nk9ejx23gcza18';      // Basic/ComboBox/ComboBox
const URL_COLOR = 'ui://nk9ejx23au3n5y';      // Basic/ColorPicker/ColorInput
const URL_NUMERIC = 'ui://nk9ejx23au3n69';    // Basic/Input/NumericInput

const LABEL_X = 4, LABEL_W = 80, INPUT_X = 87, ROW_H = 26, TOP = 6, PANEL_W = 270;
const NUMERIC_W = 60, COMBO_W = 152;
const LABEL_COLOR = new UnityEngine.Color(0xbd / 255, 0xbd / 255, 0xbd / 255, 1);
const PANEL_TITLE = 'Shader 特效';
const PLACE_BEFORE = 'etc'; // 放在原生“效果”（effect）与“其它”（etc）之间

// image, movieclip, loader (image/movieclip content only), component and extensions (whole composited component)
const T = FairyEditor.FObjectType;
const SUPPORTED_TYPES = [T.IMAGE, T.MOVIECLIP, T.LOADER, T.COMPONENT, T.EXT_BUTTON, T.EXT_LABEL, T.EXT_COMBOBOX, T.EXT_PROGRESS_BAR, T.EXT_SLIDER];
const HINT_COLOR = new UnityEngine.Color(0xe0 / 255, 0xb0 / 255, 0x40 / 255, 1);
const UNSUPPORTED_CONTENT_HINT = '当前内容不是图片/动画，特效不会生效';

// 控件的 onChanged 不是实时的（数字框要等失焦），因此每帧轮询：
// 值一变化立即作为预览（liveState）生效，停止变化 COMMIT_DELAY 毫秒后再写入自定义数据（一次撤销记录）
const COMMIT_DELAY = 300;

/** fx 为 '' 表示无特效，其余为参数 */
export type FxState = { fx: string } & Record<string, any>;

export interface InspectorApi {
    poll(): void;
    /** 面板中尚未写入自定义数据的实时状态；没有时为 undefined */
    override(obj: FairyEditor.FObject): FxState | undefined;
    flush(): void;
}

interface ParamControl {
    field: fx.FieldDef;
    input: any; // FairyEditor.Component.ColorInput | NumericInput
}

interface ParamGroup {
    component: FairyGUI.GComponent;
    rows: number;
    controls: Record<string, ParamControl>;
}

function createLabel(parent: FairyGUI.GComponent, text: string, y: number): FairyGUI.GTextField {
    const label = new FairyGUI.GTextField();
    label.autoSize = FairyGUI.AutoSizeType.None;
    label.SetXY(LABEL_X, y);
    label.SetSize(LABEL_W, 19);
    const textFormat = label.textFormat;
    textFormat.size = 12;
    textFormat.color = LABEL_COLOR;
    label.textFormat = textFormat;
    label.align = FairyGUI.AlignType.Center;
    label.text = text;
    parent.AddChild(label);
    return label;
}

function createControl(parent: FairyGUI.GComponent, url: string, y: number, width: number): any {
    const obj = FairyGUI.UIPackage.CreateObjectFromURL(url);
    obj.SetXY(INPUT_X, y);
    if (width) {
        obj.SetSize(width, obj.height);
    }
    parent.AddChild(obj);
    return obj;
}

/** effect id '' means none */
function createEffectSelector(parent: FairyGUI.GComponent, y: number) {
    const effectIds = [''].concat(fx.EFFECTS.map(effect => effect.id));
    const labels = ['无'].concat(fx.EFFECTS.map(effect => effect.label));
    const combo = createControl(parent, URL_COMBO, y, COMBO_W) as FairyGUI.GComboBox;
    combo.items = toStringArray(labels);
    combo.visibleItemCount = 10;
    return {
        getEffectId: () => effectIds[combo.selectedIndex] || '',
        setEffectId: (effectId: string) => { combo.selectedIndex = Math.max(0, effectIds.indexOf(effectId)); },
    };
}

/** param controls of an effect (added to panel by showParamGroup) */
function createParamGroup(effect: fx.EffectDef): ParamGroup {
    const component = new FairyGUI.GComponent();
    const controls: Record<string, ParamControl> = {};
    effect.fields.forEach((field, row) => {
        let y = row * ROW_H;
        createLabel(component, field.label, y);
        if (field.type === 'color') {
            let input = createControl(component, URL_COLOR, y, 0);
            input.showAlpha = true;
            controls[field.key] = { field, input };
        } else {
            let input = createControl(component, URL_NUMERIC, y, NUMERIC_W);
            input.min = field.min;
            input.max = field.max;
            input.step = field.step;
            input.fractionDigits = field.digits;
            controls[field.key] = { field, input };
        }
    });
    component.SetSize(PANEL_W, effect.fields.length * ROW_H);
    return { component, rows: effect.fields.length, controls };
}

function getValueFromControl(control: ParamControl): string | number {
    const { field, input } = control;
    if (field.type === 'color') {
        let color = input.colorValue as UnityEngine.Color;
        return colorToHex([color.r, color.g, color.b, color.a]);
    }

    // NumericInput.value 只在失焦时更新，输入/拖动过程中直接解析文字；输入到一半（如 "-"）时沿用当前值
    const value = parseFloat(inputText(input));
    if (isNaN(value)) return input.value;
    return Math.min(field.max, Math.max(field.min, value));
}

function setControlValue(control: ParamControl, value: string | number) {
    if (control.field.type === 'color') {
        let color = parseColor(value as string);
        control.input.colorValue = new UnityEngine.Color(color[0], color[1], color[2], color[3]);
    } else {
        control.input.value = Number(value);
    }
}

export function register(logger: Logger): InspectorApi {
    const panel = new FairyGUI.GComponent();
    createLabel(panel, '特效', TOP);
    const effectSelector = createEffectSelector(panel, TOP);

    const paramGroups: Record<string, ParamGroup> = {};
    for (const effect of fx.EFFECTS) {
        paramGroups[effect.id] = createParamGroup(effect);
    }

    const hint = createLabel(panel, UNSUPPORTED_CONTENT_HINT, 0);
    hint.SetSize(PANEL_W - LABEL_X * 2, 19);
    hint.align = FairyGUI.AlignType.Left;
    hint.color = HINT_COLOR;
    hint.visible = false;

    let shownEffectId: string | null = null;
    let prevStateKey: string | null = null;
    let liveState: FxState | null = null;             // 尚未写入的控件状态
    let liveTargets: FairyEditor.FObject[] = [];      // 改动开始时选中的对象（写入对象）
    let changedAt = 0;
    let orderLogged = false;

    function updateLayout() {
        let group = paramGroups[shownEffectId];
        let height = TOP + ROW_H * (1 + (group ? group.rows : 0));
        if (hint.visible) {
            hint.y = height;
            height += ROW_H;
        }
        panel.SetSize(PANEL_W, height + TOP);
    }

    function showParamGroup(effectId: string) {
        if (effectId === shownEffectId) return;
        if (shownEffectId && paramGroups[shownEffectId]) {
            panel.RemoveChild(paramGroups[shownEffectId].component);
        }
        shownEffectId = effectId;
        let group = paramGroups[effectId];
        if (group) {
            group.component.SetXY(0, TOP + ROW_H);
            panel.AddChild(group.component);
        }
        updateLayout();
    }

    /** show hint when a selected loader's content is not image/movieclip */
    function updateHint(targets: FairyEditor.FObject[]) {
        let unsupported = targets.some(obj => obj.objectType === T.LOADER && !getMesh(obj));
        if (hint.visible !== unsupported) {
            hint.visible = unsupported;
            updateLayout();
        }
    }

    function getSelectedTargets(): FairyEditor.FObject[] {
        let doc = App.activeDoc;
        let list = doc && toArray<FairyEditor.FObject>(doc.inspectingTargets as any);
        return (list || []).filter(obj => SUPPORTED_TYPES.indexOf(obj.objectType) >= 0);
    }

    function getStateFromControls(): FxState {
        let effectId = effectSelector.getEffectId();
        let state: FxState = { fx: effectId };
        let group = paramGroups[effectId];
        if (group) {
            for (const key in group.controls) {
                state[key] = getValueFromControl(group.controls[key]);
            }
        }
        return state;
    }

    function isAnyControlEditing(): boolean {
        let group = paramGroups[shownEffectId];
        if (!group) return false;

        for (const key in group.controls) {
            if (isEditing(group.controls[key].input)) return true;
        }
        return false;
    }

    /** transfer state: customData -> controls */
    function updateUI(): boolean {
        // commit pending changes to the previous selection first
        flush();

        let targets = getSelectedTargets();
        if (targets.length === 0) return false;

        let data = parseCustomData(targets[0].customData) || {};
        let effectId = fx.BY_ID[data.fx] ? data.fx : '';

        for (const effect of fx.EFFECTS) {
            let values = Object.assign({}, effect.defaults, effect.id === effectId ? data : {});
            let controls = paramGroups[effect.id].controls;
            for (const key in controls) {
                setControlValue(controls[key], values[key]);
            }
        }
        effectSelector.setEffectId(effectId);
        showParamGroup(effectId);
        updateHint(targets);
        prevStateKey = JSON.stringify(getStateFromControls());
        return true;
    }

    /** transfer state: controls -> customData (keeps non-fx keys) */
    function setTargetsState(state: FxState, targets: FairyEditor.FObject[]) {
        for (const obj of targets) {
            let data = parseCustomData(obj.customData);
            if (data === null) {
                App.Alert('对象“' + obj.name + '”的自定义数据不是 JSON，已被其它用途占用，无法写入特效参数。');
                continue;
            }
            for (const key of fx.ALL_KEYS) {
                delete data[key];
            }
            if (state.fx) {
                Object.assign(data, state);
            }
            let text = Object.keys(data).length ? JSON.stringify(data) : '';
            if (text !== (obj.customData || '')) {
                obj.docElement.SetProperty('customData', text);
            }
        }
    }

    function flush() {
        if (!liveState) return;

        let state = liveState, targets = liveTargets;
        liveState = null;
        liveTargets = [];
        setTargetsState(state, targets);
    }

    function poll() {
        // reorder: move before "etc"
        if (panel.onStage && !placeSectionBefore(panel, PLACE_BEFORE) && !orderLogged) {
            orderLogged = true;
            logger.log(`inspector order: ${describeInspectorList(panel)}`);
        }

        // check controls state changed: preview immediately
        if (prevStateKey !== null && panel.onStage) {
            let controlsState = getStateFromControls();
            let key = JSON.stringify(controlsState);
            if (key !== prevStateKey) {
                prevStateKey = key;
                if (!liveState) {
                    liveTargets = getSelectedTargets();
                }
                liveState = controlsState;
                changedAt = Date.now();
                if (controlsState.fx !== shownEffectId) {
                    showParamGroup(controlsState.fx);
                }
            }
        }

        // commit after changes settle; 输入框有焦点时不写入（写入会刷新面板、打断输入）
        if (liveState && Date.now() - changedAt >= COMMIT_DELAY && !isAnyControlEditing()) {
            flush();
        }
    }

    function override(obj: FairyEditor.FObject): FxState | undefined {
        if (!liveState) return undefined;

        for (const target of liveTargets) {
            if (target === obj || target.Equals(obj)) return liveState;
        }
        return undefined;
    }

    App.inspectorView.AddInspector(() => {
        let inspector = new FairyEditor.View.PluginInspector();
        inspector.panel = panel;
        inspector.updateAction = updateUI;
        return inspector;
    }, INSPECTOR_NAME, PANEL_TITLE);
    for (const type of SUPPORTED_TYPES) {
        App.docFactory.ConnectInspector(INSPECTOR_NAME, type, false, false);
    }
    logger.log('inspector registered');
    return { poll, override, flush };
}

export function unregister() {
    App.docFactory.disconnectInspector(INSPECTOR_NAME);
    App.inspectorView.RemoveInspector(INSPECTOR_NAME);
}
