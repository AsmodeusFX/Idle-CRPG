// 控件与属性的绑定由插件完成：所有修改通过 docElement.SetProperty 写入（可撤销、会保存和发布）。
// 动画/皮肤列表来自编辑器的 SkeletonAsset（FPackageItem.GetAsset()）。
import FairyEditor = CS.FairyEditor;
import FairyGUI = CS.FairyGUI;
import UnityEngine = CS.UnityEngine;
import App = CS.FairyEditor.App;
import { toArray, toStringArray } from '../../shared/cs';
import { inputText, isEditing } from '../../shared/ui';
import type { Logger } from '../../shared/diag';
import { describeInspectorList, findList, placeSectionBefore } from '../../shared/inspectorOrder';
import { colorToHex } from '../../shared/color';

const INSPECTOR_NAME = 'Loader3DProps';
const URL_PANEL = 'ui://2pshu6oitwdeixicvm'; // Builder/inspectors/Loader3DPropsPanel.xml
const T = FairyEditor.FObjectType;

const FILL_ITEM_VALUES = ['none', 'scale', 'scaleMatchHeight', 'scaleMatchWidth', 'scaleFree', 'scaleNoBorder'];
const FILL_ITEM_LABELS = ['无', '比例缩放(显示全部)', '比例缩放(匹配高度)', '比例缩放(匹配宽度)', '自由缩放', '比例缩放(无边框)'];
const ALIGN_ITEM_VALUES = ['left', 'center', 'right'];
const VALIGN_ITEM_VALUES = ['top', 'middle', 'bottom'];
const MISSING_SUFFIX = ' (缺失)';
const NATIVE_SECTION_PATTERN = /^loader3d$/i;
const PLACE_BEFORE = 'gear';

// 已断开的原生检查器名，卸载插件时重新连接，恢复原生面板
let hiddenNativeName: string | null = null;

/** 面板上可编辑的属性（color 以十六进制字符串比较） */
interface Loader3DState {
    url: string;
    clearOnPublish: boolean;
    autoSize: boolean;
    fill: string;
    shrinkOnly: boolean;
    align: string;
    verticalAlign: string;
    color: string;
    animationName: string;
    skinName: string;
    playing: boolean;
    loop: boolean;
    frame: number;
}

export interface PanelApi {
    poll(): void;
}

interface SkeletonInfo {
    animations: string[];
    skins: string[];
    /** NOTE: check loaded first! */
    asset: FairyEditor.SkeletonAsset | null;
}

function getSkeletonInfo(loader: FairyEditor.FLoader3D): SkeletonInfo {
    try {
        let res = loader.contentRes;
        let item = res && res.packageItem;
        let asset = item && item.GetAsset();
        if (asset instanceof FairyEditor.SkeletonAsset) {
            let data = asset.isLoaded ? asset.data : null;
            return {
                animations: data ? toArray(data.GetAnimations()) : [],
                skins: data ? toArray(data.GetSkins()) : [],
                asset,
            };
        }
    } catch (e) {
        // 资源缺失或加载失败
    }
    return { animations: [], skins: [], asset: null };
}

function getStateFromLoader(loader: FairyEditor.FLoader3D): Loader3DState {
    return {
        url: loader.url || '',
        clearOnPublish: !!loader.clearOnPublish,
        autoSize: !!loader.autoSize,
        fill: loader.fill || 'none',
        shrinkOnly: !!loader.shrinkOnly,
        align: loader.align || 'left',
        verticalAlign: loader.verticalAlign || 'top',
        color: colorToHex([loader.color.r, loader.color.g, loader.color.b, loader.color.a]),
        animationName: loader.animationName || '',
        skinName: loader.skinName || '',
        playing: !!loader.playing,
        loop: !!loader.loop,
        frame: loader.frame,
    };
}

export function register(logger: Logger): PanelApi {
    const panel = FairyGUI.UIPackage.CreateObjectFromURL(URL_PANEL).asCom;
    const panelControls = {
        url: panel.GetChild('url') as FairyEditor.Component.ResourceInput,
        clearOnPublish: panel.GetChild('clearOnPublish') as FairyGUI.GButton,
        autoSize: panel.GetChild('autoSize') as FairyGUI.GButton,
        fill: panel.GetChild('fill') as FairyGUI.GComboBox,
        shrinkOnly: panel.GetChild('shrinkOnly') as FairyGUI.GButton,
        align: panel.GetController('align'),
        verticalAlign: panel.GetController('verticalAlign'),
        color: panel.GetChild('color') as FairyEditor.Component.ColorInput,
        animationName: panel.GetChild('animationName') as FairyGUI.GComboBox,
        skinName: panel.GetChild('skinName') as FairyGUI.GComboBox,
        playing: panel.GetChild('playing') as FairyGUI.GButton,
        loop: panel.GetChild('loop') as FairyGUI.GButton,
        frame: panel.GetChild('frame') as FairyEditor.Component.NumericInput,
    };
    setComboBoxItems(panelControls.fill, FILL_ITEM_LABELS);
    panelControls.frame.min = 0;
    panelControls.frame.max = 100000;
    panelControls.frame.step = 1;
    panelControls.frame.fractionDigits = 0;

    let target: FairyEditor.FLoader3D | null = null;
    let prevStateKey: string | null = null;
    let animComboBoxItems = [''];
    let skinComboBoxItems = [''];
    let pendingAsset: FairyEditor.SkeletonAsset | null = null; // 等待加载完成后刷新动画/皮肤列表
    let nativeChecked = false;
    let orderLogged = false;

    function hideNativeSection() {
        if (nativeChecked) return;

        let list = findList(panel);
        if (!list) return;

        nativeChecked = true;
        for (let i = 0; i < list.numChildren; i++) {
            let item = list.GetChildAt(i);
            if (item !== panel && NATIVE_SECTION_PATTERN.test(item.name || '')) {
                hiddenNativeName = item.name;
                break;
            }
        }

        if (!hiddenNativeName) {
            logger.warn('native loader3d section not found: ' + describeInspectorList(panel));
            return;
        }

        App.docFactory.disconnectInspector(hiddenNativeName);
        logger.log('native inspector disconnected: ' + hiddenNativeName);

        // reselect to refresh
        if (App.activeDoc && target) {
            App.activeDoc.SetSelection(target);
        }
    }

    let updating = false; // 防止 updateUI 重入
    const warned = new Set<string>();

    /** Get selected FLoader3D */
    function getSelectedLoader(): FairyEditor.FLoader3D | null {
        let doc = App.activeDoc;
        let list = doc ? toArray<FairyEditor.FObject>(doc.inspectingTargets as any) : [];
        if (list.length !== 1) return null;
        let obj = list[0];
        return obj.objectType === T.Loader3D ? obj as FairyEditor.FLoader3D : null;
    }

    function setComboBoxItems(combo: FairyGUI.GComboBox, labels: string[]) {
        combo.items = toStringArray(labels);
        combo.visibleItemCount = 15;
    }

    function setComboBoxValue(combo: FairyGUI.GComboBox, values: string[], labels: string[], value: string) {
        let index = values.indexOf(value);
        if (index < 0 && value) {
            values.push(value);
            labels.push(value + MISSING_SUFFIX);
            index = values.length - 1;
        }
        setComboBoxItems(combo, labels);
        combo.selectedIndex = Math.max(0, index);
    }

    function setControlsState(inputState: Loader3DState) {
        logger.trace('fill');
        panelControls.url.text = inputState.url;
        panelControls.clearOnPublish.selected = inputState.clearOnPublish;
        panelControls.autoSize.selected = inputState.autoSize;
        panelControls.fill.selectedIndex = Math.max(0, FILL_ITEM_VALUES.indexOf(inputState.fill));
        panelControls.shrinkOnly.selected = inputState.shrinkOnly;
        panelControls.align.selectedIndex = Math.max(0, ALIGN_ITEM_VALUES.indexOf(inputState.align));
        panelControls.verticalAlign.selectedIndex = Math.max(0, VALIGN_ITEM_VALUES.indexOf(inputState.verticalAlign));
        panelControls.color.colorValue = target.color;
        panelControls.playing.selected = inputState.playing;
        panelControls.loop.selected = inputState.loop;
        panelControls.frame.value = inputState.frame || 0;

        let skeletonInfo = getSkeletonInfo(target);

        // load the not loaded asset
        pendingAsset = skeletonInfo.asset && !skeletonInfo.asset.isLoaded ? skeletonInfo.asset : null;
        if (pendingAsset && !pendingAsset.isLoading) {
            pendingAsset.Load();
        }
        logger.trace(`skeletonInfo ${skeletonInfo.animations.length} animations, ${skeletonInfo.skins.length} skins, pending=${!!pendingAsset}`);
        animComboBoxItems = [''].concat(skeletonInfo.animations);
        skinComboBoxItems = [''].concat(skeletonInfo.skins);

        setComboBoxValue(panelControls.animationName, animComboBoxItems, ["(无)"].concat(skeletonInfo.animations), inputState.animationName);
        setComboBoxValue(panelControls.skinName, skinComboBoxItems, ["(默认)"].concat(skeletonInfo.skins), inputState.skinName);
    }

    function readControl_Url(): string {
        let text = inputText(panelControls.url) || '';
        return text === '' || text.indexOf('ui://') === 0 ? text : (target ? target.url || '' : '');
    }

    function readControl_Frame(): number {
        let v = parseInt(inputText(panelControls.frame), 10);
        return isNaN(v) ? panelControls.frame.value : Math.max(0, v);
    }

    function getStateFromControls(): Loader3DState {
        return {
            url: readControl_Url(),
            clearOnPublish: panelControls.clearOnPublish.selected,
            autoSize: panelControls.autoSize.selected,
            fill: FILL_ITEM_VALUES[panelControls.fill.selectedIndex] || 'none',
            shrinkOnly: panelControls.shrinkOnly.selected,
            align: ALIGN_ITEM_VALUES[panelControls.align.selectedIndex] || 'left',
            verticalAlign: VALIGN_ITEM_VALUES[panelControls.verticalAlign.selectedIndex] || 'top',
            color: colorToHex([panelControls.color.colorValue.r, panelControls.color.colorValue.g, panelControls.color.colorValue.b, panelControls.color.colorValue.a]),
            animationName: animComboBoxItems[panelControls.animationName.selectedIndex] || '',
            skinName: skinComboBoxItems[panelControls.skinName.selectedIndex] || '',
            playing: panelControls.playing.selected,
            loop: panelControls.loop.selected,
            frame: readControl_Frame(),
        };
    }

    /** transfer state: loader -> controls */
    function updateUI(): boolean {
        if (updating) {
            logger.trace('updateUI re-entered, skipped');
            return !!target;
        }
        updating = true;
        try {
            target = getSelectedLoader();

            // not inspecting a FLoader3D
            if (!target) return false;

            setControlsState(getStateFromLoader(target));
            prevStateKey = JSON.stringify(getStateFromControls());
            return true;
        } finally {
            updating = false;
        }
    }

    function setLoaderProperty(name: string, value: unknown) {
        try {
            target.docElement.SetProperty(name, value);
        } catch (e) {
            (target as any)[name] = value;
            App.activeDoc.SetModified(true);
            if (!warned.has(name)) {
                warned.add(name);
                logger.warn(`SetProperty(${name}) failed, assigned directly: ${(e as Error)?.message || e}`);
            }
        }
    }

    function poll() {
        if (!target || !panel.onStage) return;

        // once effect: hide native & reorder
        hideNativeSection();
        if (!placeSectionBefore(panel, PLACE_BEFORE) && !orderLogged) {
            orderLogged = true;
            logger.log(`inspector order: ${describeInspectorList(panel)}`);
        }

        // update controls if skeleton loaded
        if (pendingAsset && pendingAsset.isLoaded) {
            pendingAsset = null;
            updateUI();
            return;
        }

        // URL/帧数输入框有焦点时不写入（写入会刷新面板、打断输入）
        if (isEditing(panelControls.url) || isEditing(panelControls.frame)) return;

        // check controls state changed
        let controlsState = getStateFromControls();
        let key = JSON.stringify(controlsState);
        if (key === prevStateKey) return;
        prevStateKey = key;

        // transfer state: controls -> loader
        let loaderState = getStateFromLoader(target);
        for (const name of Object.keys(controlsState) as (keyof Loader3DState)[]) {
            if (controlsState[name] === loaderState[name]) continue;

            logger.trace(`write ${name}: ${JSON.stringify(loaderState[name])} -> ${JSON.stringify(controlsState[name])}`);
            setLoaderProperty(name, name === 'color' ? panelControls.color.colorValue : controlsState[name]);
        }
    }

    App.inspectorView.AddInspector(() => {
        let inspector = new FairyEditor.View.PluginInspector();
        inspector.panel = panel;
        inspector.updateAction = updateUI;
        return inspector;
    }, INSPECTOR_NAME, '3D装载器');
    App.docFactory.ConnectInspector(INSPECTOR_NAME, T.Loader3D, false, false);
    logger.log('loader3d inspector registered');
    return { poll };
}

export function unregister() {
    if (hiddenNativeName) {
        App.docFactory.ConnectInspector(hiddenNativeName, T.Loader3D, false, false);
        hiddenNativeName = null;
    }
    App.docFactory.disconnectInspector(INSPECTOR_NAME);
    App.inspectorView.RemoveInspector(INSPECTOR_NAME);
}
