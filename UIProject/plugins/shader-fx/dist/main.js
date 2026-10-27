var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// shader-fx/src/main.ts
var main_exports = {};
__export(main_exports, {
  onDestroy: () => onDestroy
});
module.exports = __toCommonJS(main_exports);

// shared/color.ts
function parseColor(s) {
  let hex = String(s || "").replace("#", "");
  if (hex.length === 6) {
    hex += "FF";
  }
  let value = parseInt(hex, 16);
  if (hex.length !== 8 || isNaN(value))
    return [1, 1, 1, 1];
  return [(value >>> 24 & 255) / 255, (value >>> 16 & 255) / 255, (value >>> 8 & 255) / 255, (value & 255) / 255];
}
function colorToHex(color) {
  let toHex = (channel) => ("0" + Math.round(Math.max(0, Math.min(1, channel)) * 255).toString(16)).slice(-2);
  return ("#" + toHex(color[0]) + toHex(color[1]) + toHex(color[2]) + toHex(color[3])).toUpperCase();
}

// shader-fx/src/effects.ts
var EFFECTS = [
  {
    id: "sweep",
    label: "\u626B\u5149",
    fields: [
      { key: "color", label: "\u989C\u8272", type: "color", default: "#FFFFFFCC" },
      { key: "width", label: "\u5BBD\u5EA6", type: "number", min: 0.01, max: 1, step: 0.01, digits: 2, default: 0.15 },
      { key: "angle", label: "\u89D2\u5EA6", type: "number", min: -180, max: 180, step: 1, digits: 0, default: 30 },
      { key: "duration", label: "\u626B\u8FC7\u65F6\u957F", type: "number", min: 0.05, max: 10, step: 0.05, digits: 2, default: 0.8 },
      { key: "interval", label: "\u95F4\u9694", type: "number", min: 0, max: 10, step: 0.1, digits: 2, default: 1.5 },
      { key: "intensity", label: "\u4EAE\u5EA6", type: "number", min: 0, max: 5, step: 0.1, digits: 2, default: 1 },
      { key: "softness", label: "\u67D4\u548C\u5EA6", type: "number", min: 0, max: 1, step: 0.05, digits: 2, default: 1 }
    ],
    uniforms: (params, width, height) => ({
      _FxRect: [width, height, 0, 0],
      _SweepColor: params.color,
      _SweepParam: [params.width, params.angle, params.duration, params.interval],
      _SweepParam2: [params.intensity, params.softness, 0, 0]
    }),
    off: { _SweepColor: [0, 0, 0, 0] }
  }
];
var BY_ID = {};
for (const effect of EFFECTS) {
  effect.defaults = {};
  for (const field of effect.fields) {
    effect.defaults[field.key] = field.default;
  }
  BY_ID[effect.id] = effect;
}
var ALL_KEYS = ["fx"];
for (const effect of EFFECTS) {
  for (const field of effect.fields) {
    if (ALL_KEYS.indexOf(field.key) < 0) {
      ALL_KEYS.push(field.key);
    }
  }
}
function resolveParams(effect, data) {
  let params = Object.assign({}, effect.defaults);
  for (const field of effect.fields) {
    let value = data[field.key];
    if (value === void 0)
      continue;
    params[field.key] = field.type === "number" ? Number(value) : value;
  }
  for (const field of effect.fields) {
    if (field.type === "color")
      params[field.key] = parseColor(params[field.key]);
  }
  return params;
}

// shared/cs.ts
function forEach(list, act) {
  if (!list)
    return;
  let count = "Count" in list ? list.Count : list.Length;
  for (let i = 0; i < count; i++) {
    act(list.get_Item(i), i);
  }
}
function toArray(list) {
  let result = [];
  forEach(list, (value) => result.push(value));
  return result;
}
function toStringArray(list) {
  let array = CS.System.Array.CreateInstance(puerts.$typeof(CS.System.String), list.length);
  list.forEach((value, i) => array.SetValue(value, i));
  return array;
}

// shared/ui.ts
function inputText(input) {
  let titleField = input.GetChild("title");
  let display = titleField && titleField.displayObject;
  if (display instanceof CS.FairyGUI.RichTextField || display instanceof CS.FairyGUI.TextField)
    return display.text;
  return input.title;
}
function isEditing(input) {
  let titleField = input.GetChild("title");
  return !!(titleField && titleField.focused);
}
function parseCustomData(customData) {
  if (!customData)
    return {};
  try {
    let obj = JSON.parse(customData);
    return obj && typeof obj === "object" && !Array.isArray(obj) ? obj : null;
  } catch (e) {
    return null;
  }
}

// shader-fx/src/targets.ts
var FairyEditor = CS.FairyEditor;
var FairyGUI = CS.FairyGUI;
var T = FairyEditor.FObjectType;
function isMeshType(obj) {
  let type = obj.objectType;
  return type === T.IMAGE || type === T.MOVIECLIP || type === T.LOADER;
}
function getImageContent(obj) {
  let display = obj.displayObject;
  let content = display && display.content;
  return content instanceof FairyGUI.Image && content.graphics ? content : null;
}
function getMesh(obj) {
  if (obj.objectType === T.IMAGE) {
    let bitmap = obj.bitmap;
    return bitmap && bitmap.graphics ? { graphics: bitmap.graphics, width: obj.width, height: obj.height } : null;
  }
  let content = getImageContent(obj);
  return content ? { graphics: content.graphics, width: content.width, height: content.height } : null;
}

// shared/inspectorOrder.ts
var FairyGUI2 = CS.FairyGUI;
var TITLE_BAR_URL = "ui://2pshu6oiitme7iudp";
function isTitleBar(obj) {
  return obj.resourceURL === TITLE_BAR_URL;
}
function sectionTitle(item) {
  return isTitleBar(item) ? item.title : null;
}
function findList(panel) {
  for (let p = panel.parent; p; p = p.parent) {
    if (p instanceof FairyGUI2.GList)
      return p;
  }
  return null;
}
function describeInspectorList(panel) {
  let ancestors = [];
  for (let p = panel.parent; p; p = p.parent) {
    ancestors.push((p.name || "?") + (p instanceof FairyGUI2.GList ? "[GList]" : ""));
  }
  let list = findList(panel);
  if (!list)
    return "no list; ancestors: " + ancestors.join(" < ");
  let parts = ["ancestors: " + ancestors.join(" < "), "items:"];
  for (let i = 0; i < list.numChildren; i++) {
    let item = list.GetChildAt(i);
    let title = sectionTitle(item);
    parts.push(`${i}:${item.name || "?"}${title !== null ? "\u300C" + title + "\u300D" : ""}`);
  }
  return parts.join(" ");
}
function placeSectionBefore(panel, beforeName) {
  let list = panel.onStage ? findList(panel) : null;
  if (!list)
    return false;
  let item = panel;
  while (item && item.parent && item.parent !== list) {
    item = item.parent;
  }
  if (!item || item.parent !== list)
    return false;
  let index = list.GetChildIndex(item);
  let prev = index > 0 ? list.GetChildAt(index - 1) : null;
  let items = prev && isTitleBar(prev) ? [prev, item] : [item];
  let targetPanel = list.GetChild(beforeName);
  if (!targetPanel)
    return false;
  let target = list.GetChildIndex(targetPanel);
  if (target > 0 && isTitleBar(list.GetChildAt(target - 1))) {
    target--;
  }
  if (list.GetChildIndex(items[items.length - 1]) === target - 1)
    return true;
  for (const it of items) {
    let from = list.GetChildIndex(it);
    let to = from < target ? target - 1 : target;
    list.SetChildIndex(it, to);
    target = to + 1;
  }
  return true;
}

// shader-fx/src/inspector.ts
var FairyEditor2 = CS.FairyEditor;
var FairyGUI3 = CS.FairyGUI;
var UnityEngine = CS.UnityEngine;
var App = CS.FairyEditor.App;
var INSPECTOR_NAME = "ShaderFx";
var URL_COMBO = "ui://nk9ejx23gcza18";
var URL_COLOR = "ui://nk9ejx23au3n5y";
var URL_NUMERIC = "ui://nk9ejx23au3n69";
var LABEL_X = 4;
var LABEL_W = 80;
var INPUT_X = 87;
var ROW_H = 26;
var TOP = 6;
var PANEL_W = 270;
var NUMERIC_W = 60;
var COMBO_W = 152;
var LABEL_COLOR = new UnityEngine.Color(189 / 255, 189 / 255, 189 / 255, 1);
var PANEL_TITLE = "Shader \u7279\u6548";
var PLACE_BEFORE = "etc";
var T2 = FairyEditor2.FObjectType;
var SUPPORTED_TYPES = [T2.IMAGE, T2.MOVIECLIP, T2.LOADER, T2.COMPONENT, T2.EXT_BUTTON, T2.EXT_LABEL, T2.EXT_COMBOBOX, T2.EXT_PROGRESS_BAR, T2.EXT_SLIDER];
var HINT_COLOR = new UnityEngine.Color(224 / 255, 176 / 255, 64 / 255, 1);
var UNSUPPORTED_CONTENT_HINT = "\u5F53\u524D\u5185\u5BB9\u4E0D\u662F\u56FE\u7247/\u52A8\u753B\uFF0C\u7279\u6548\u4E0D\u4F1A\u751F\u6548";
var COMMIT_DELAY = 300;
function createLabel(parent, text, y) {
  const label = new FairyGUI3.GTextField();
  label.autoSize = FairyGUI3.AutoSizeType.None;
  label.SetXY(LABEL_X, y);
  label.SetSize(LABEL_W, 19);
  const textFormat = label.textFormat;
  textFormat.size = 12;
  textFormat.color = LABEL_COLOR;
  label.textFormat = textFormat;
  label.align = FairyGUI3.AlignType.Center;
  label.text = text;
  parent.AddChild(label);
  return label;
}
function createControl(parent, url, y, width) {
  const obj = FairyGUI3.UIPackage.CreateObjectFromURL(url);
  obj.SetXY(INPUT_X, y);
  if (width) {
    obj.SetSize(width, obj.height);
  }
  parent.AddChild(obj);
  return obj;
}
function createEffectSelector(parent, y) {
  const effectIds = [""].concat(EFFECTS.map((effect) => effect.id));
  const labels = ["\u65E0"].concat(EFFECTS.map((effect) => effect.label));
  const combo = createControl(parent, URL_COMBO, y, COMBO_W);
  combo.items = toStringArray(labels);
  combo.visibleItemCount = 10;
  return {
    getEffectId: () => effectIds[combo.selectedIndex] || "",
    setEffectId: (effectId) => {
      combo.selectedIndex = Math.max(0, effectIds.indexOf(effectId));
    }
  };
}
function createParamGroup(effect) {
  const component = new FairyGUI3.GComponent();
  const controls = {};
  effect.fields.forEach((field, row) => {
    let y = row * ROW_H;
    createLabel(component, field.label, y);
    if (field.type === "color") {
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
function getValueFromControl(control) {
  const { field, input } = control;
  if (field.type === "color") {
    let color = input.colorValue;
    return colorToHex([color.r, color.g, color.b, color.a]);
  }
  const value = parseFloat(inputText(input));
  if (isNaN(value))
    return input.value;
  return Math.min(field.max, Math.max(field.min, value));
}
function setControlValue(control, value) {
  if (control.field.type === "color") {
    let color = parseColor(value);
    control.input.colorValue = new UnityEngine.Color(color[0], color[1], color[2], color[3]);
  } else {
    control.input.value = Number(value);
  }
}
function register(logger2) {
  const panel = new FairyGUI3.GComponent();
  createLabel(panel, "\u7279\u6548", TOP);
  const effectSelector = createEffectSelector(panel, TOP);
  const paramGroups = {};
  for (const effect of EFFECTS) {
    paramGroups[effect.id] = createParamGroup(effect);
  }
  const hint = createLabel(panel, UNSUPPORTED_CONTENT_HINT, 0);
  hint.SetSize(PANEL_W - LABEL_X * 2, 19);
  hint.align = FairyGUI3.AlignType.Left;
  hint.color = HINT_COLOR;
  hint.visible = false;
  let shownEffectId = null;
  let prevStateKey = null;
  let liveState = null;
  let liveTargets = [];
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
  function showParamGroup(effectId) {
    if (effectId === shownEffectId)
      return;
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
  function updateHint(targets) {
    let unsupported = targets.some((obj) => obj.objectType === T2.LOADER && !getMesh(obj));
    if (hint.visible !== unsupported) {
      hint.visible = unsupported;
      updateLayout();
    }
  }
  function getSelectedTargets() {
    let doc = App.activeDoc;
    let list = doc && toArray(doc.inspectingTargets);
    return (list || []).filter((obj) => SUPPORTED_TYPES.indexOf(obj.objectType) >= 0);
  }
  function getStateFromControls() {
    let effectId = effectSelector.getEffectId();
    let state = { fx: effectId };
    let group = paramGroups[effectId];
    if (group) {
      for (const key in group.controls) {
        state[key] = getValueFromControl(group.controls[key]);
      }
    }
    return state;
  }
  function isAnyControlEditing() {
    let group = paramGroups[shownEffectId];
    if (!group)
      return false;
    for (const key in group.controls) {
      if (isEditing(group.controls[key].input))
        return true;
    }
    return false;
  }
  function updateUI() {
    flush();
    let targets = getSelectedTargets();
    if (targets.length === 0)
      return false;
    let data = parseCustomData(targets[0].customData) || {};
    let effectId = BY_ID[data.fx] ? data.fx : "";
    for (const effect of EFFECTS) {
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
  function setTargetsState(state, targets) {
    for (const obj of targets) {
      let data = parseCustomData(obj.customData);
      if (data === null) {
        App.Alert("\u5BF9\u8C61\u201C" + obj.name + "\u201D\u7684\u81EA\u5B9A\u4E49\u6570\u636E\u4E0D\u662F JSON\uFF0C\u5DF2\u88AB\u5176\u5B83\u7528\u9014\u5360\u7528\uFF0C\u65E0\u6CD5\u5199\u5165\u7279\u6548\u53C2\u6570\u3002");
        continue;
      }
      for (const key of ALL_KEYS) {
        delete data[key];
      }
      if (state.fx) {
        Object.assign(data, state);
      }
      let text = Object.keys(data).length ? JSON.stringify(data) : "";
      if (text !== (obj.customData || "")) {
        obj.docElement.SetProperty("customData", text);
      }
    }
  }
  function flush() {
    if (!liveState)
      return;
    let state = liveState, targets = liveTargets;
    liveState = null;
    liveTargets = [];
    setTargetsState(state, targets);
  }
  function poll() {
    if (panel.onStage && !placeSectionBefore(panel, PLACE_BEFORE) && !orderLogged) {
      orderLogged = true;
      logger2.log(`inspector order: ${describeInspectorList(panel)}`);
    }
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
    if (liveState && Date.now() - changedAt >= COMMIT_DELAY && !isAnyControlEditing()) {
      flush();
    }
  }
  function override(obj) {
    if (!liveState)
      return void 0;
    for (const target of liveTargets) {
      if (target === obj || target.Equals(obj))
        return liveState;
    }
    return void 0;
  }
  App.inspectorView.AddInspector(() => {
    let inspector = new FairyEditor2.View.PluginInspector();
    inspector.panel = panel;
    inspector.updateAction = updateUI;
    return inspector;
  }, INSPECTOR_NAME, PANEL_TITLE);
  for (const type of SUPPORTED_TYPES) {
    App.docFactory.ConnectInspector(INSPECTOR_NAME, type, false, false);
  }
  logger2.log("inspector registered");
  return { poll, override, flush };
}
function unregister() {
  App.docFactory.disconnectInspector(INSPECTOR_NAME);
  App.inspectorView.RemoveInspector(INSPECTOR_NAME);
}

// shared/diag.ts
var App2 = CS.FairyEditor.App;
var Path = CS.System.IO.Path;
var File = CS.System.IO.File;
var Directory = CS.System.IO.Directory;
function pluginFolder(pluginName) {
  let dir = Path.Combine(App2.pluginManager.projectPluginFolder, pluginName);
  if (Directory.Exists(dir))
    return dir;
  return null;
}
function errorText(e) {
  return String(e && (e.stack || e.message) || e);
}
function createLogger(pluginName, prefix, traceEnabled = false) {
  let diagPath = null;
  function write(level, msg) {
    if (process.env.DEV_USER != "BBBIRDER")
      return;
    try {
      if (!diagPath) {
        let dir = pluginFolder(pluginName);
        if (!dir)
          return;
        diagPath = Path.Combine(dir, "diag.log");
      }
      File.AppendAllText(diagPath, (/* @__PURE__ */ new Date()).toISOString() + " " + level + " " + msg + "\n");
    } catch (e) {
    }
  }
  return {
    log(msg) {
      App2.consoleView.Log(`[${prefix}] ${msg}`);
      write("INFO", msg);
    },
    warn(msg) {
      App2.consoleView.LogWarning(`[${prefix}] ${msg}`);
      write("WARN", msg);
    },
    error(msg) {
      App2.consoleView.LogError(`[${prefix}] ${msg}`);
      write("ERROR", msg);
    },
    trace(msg) {
      if (traceEnabled) {
        write("TRACE", msg);
      }
    }
  };
}

// shader-fx/src/main.ts
var FairyEditor3 = CS.FairyEditor;
var FairyGUI4 = CS.FairyGUI;
var UnityEngine2 = CS.UnityEngine;
var App3 = CS.FairyEditor.App;
var logger = createLogger("shader-fx", "ShaderFx");
var SHADER_NAME = "FairyGUI/Image";
var FRAME_RATE_TESTING = 512;
var PAINTING_REQUESTOR = 64;
var disposed = false;
var appliedEffects = /* @__PURE__ */ new Map();
var customDataCache = /* @__PURE__ */ new Map();
var paintingComponents = /* @__PURE__ */ new Set();
var fastFrameRate = false;
var reportedError = false;
var panelApi = null;
function setUniforms(graphics, uniforms) {
  let propertyBlock = graphics.materialPropertyBlock;
  for (const name in uniforms) {
    let value = uniforms[name];
    propertyBlock.SetVector(name, new UnityEngine2.Vector4(value[0], value[1], value[2], value[3]));
  }
}
function getActiveEffectFromData(data) {
  let effect = data && BY_ID[data.fx];
  return effect ? { effect, params: resolveParams(effect, data) } : null;
}
function getActiveEffectFromCustomData(customData) {
  if (!customData || customData.indexOf('"fx"') < 0)
    return null;
  if (customDataCache.has(customData))
    return customDataCache.get(customData);
  let result = null;
  try {
    result = getActiveEffectFromData(JSON.parse(customData));
  } catch (e) {
  }
  customDataCache.set(customData, result);
  return result;
}
function getActiveEffect(obj) {
  let liveState = panelApi ? panelApi.override(obj) : void 0;
  return liveState === void 0 ? getActiveEffectFromCustomData(obj.customData) : getActiveEffectFromData(liveState);
}
function collectTargets(component, out) {
  let count = component.numChildren;
  for (let i = 0; i < count; i++) {
    let child = component.GetChildAt(i);
    if (isMeshType(child)) {
      out.push(child);
    } else if (child instanceof FairyEditor3.FComponent) {
      out.push(child);
      collectTargets(child, out);
    }
  }
}
var testRoot = null;
var testRootLogged = false;
function findTestRoot() {
  let view = App3.testView;
  if (!view || !view.running) {
    testRoot = null;
    testRootLogged = false;
    return null;
  }
  if (testRoot && testRoot.displayObject && testRoot.displayObject.parent)
    return testRoot;
  testRoot = null;
  let queue = [view.displayObject];
  for (let visited = 0; queue.length && visited < 5e3; visited++) {
    let display = queue.shift();
    let obj = null;
    try {
      obj = FairyEditor3.FObject.cast(display);
    } catch (e) {
    }
    if (obj instanceof FairyEditor3.FComponent) {
      testRoot = obj;
      break;
    }
    if (display instanceof FairyGUI4.Container) {
      for (let i = 0; i < display.numChildren; i++) {
        queue.push(display.GetChildAt(i));
      }
    }
  }
  if (testRoot && !testRootLogged) {
    testRootLogged = true;
    logger.log("test view root: " + testRoot.name);
  }
  return testRoot;
}
var loggedTypes = /* @__PURE__ */ new Set();
function getEffectMesh(obj) {
  if (!(obj instanceof FairyEditor3.FComponent)) {
    let mesh = getMesh(obj);
    if (!loggedTypes.has(obj.objectType)) {
      loggedTypes.add(obj.objectType);
      logger.log(`mesh for ${obj.objectType}: ${mesh ? mesh.width + "x" + mesh.height : "unsupported content"}`);
    }
    return mesh;
  }
  let display = obj.displayObject;
  if (!display)
    return null;
  if (!paintingComponents.has(obj) || !display.paintingMode) {
    display.EnterPaintingMode(PAINTING_REQUESTOR, null);
    paintingComponents.add(obj);
  }
  return { graphics: display.paintingGraphics, width: obj.width, height: obj.height };
}
function turnOffEffect(obj, effect) {
  try {
    if (obj instanceof FairyEditor3.FComponent) {
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
    paintingComponents.delete(obj);
  }
}
function onUpdate() {
  if (disposed)
    return;
  try {
    if (panelApi) {
      panelApi.poll();
    }
    let doc = App3.activeDoc;
    let root = doc && doc.content;
    let targets = [];
    if (root) {
      collectTargets(root, targets);
    }
    let testRootObj = findTestRoot();
    if (testRootObj) {
      targets.push(testRootObj);
      collectTargets(testRootObj, targets);
    }
    let nextAppliedEffects = /* @__PURE__ */ new Map();
    for (const obj of targets) {
      let activeEffect = getActiveEffect(obj);
      if (!activeEffect)
        continue;
      let mesh = getEffectMesh(obj);
      if (!mesh)
        continue;
      setUniforms(mesh.graphics, activeEffect.effect.uniforms(activeEffect.params, mesh.width, mesh.height));
      nextAppliedEffects.set(obj, activeEffect.effect);
    }
    for (const [obj, effect] of appliedEffects) {
      if (nextAppliedEffects.get(obj) !== effect)
        turnOffEffect(obj, effect);
    }
    appliedEffects = nextAppliedEffects;
    let wantFast = nextAppliedEffects.size > 0;
    if (wantFast !== fastFrameRate) {
      fastFrameRate = wantFast;
      App3.SetFrameRateFactor(FRAME_RATE_TESTING, wantFast);
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
function logShaderPatchState() {
  try {
    let shader = UnityEngine2.Shader.Find(SHADER_NAME);
    if (!shader) {
      logger.warn(SHADER_NAME + " not found");
      return;
    }
    let patched = "unknown";
    try {
      patched = shader.FindPropertyIndex("_SweepColor") >= 0;
    } catch (e) {
    }
    logger.log(SHADER_NAME + " found, patched=" + patched);
  } catch (e) {
    logger.warn("logShaderPatchState: " + errorText(e));
  }
}
logShaderPatchState();
App3.add_onUpdate(onUpdate);
try {
  panelApi = register(logger);
} catch (e) {
  logger.error("inspector: " + errorText(e));
}
logger.log("ready");
function onDestroy() {
  disposed = true;
  App3.remove_onUpdate(onUpdate);
  try {
    if (panelApi) {
      panelApi.flush();
    }
    unregister();
  } catch (e) {
    logger.warn("inspector unregister: " + errorText(e));
  }
  for (const [obj, effect] of appliedEffects) {
    turnOffEffect(obj, effect);
  }
  appliedEffects.clear();
  if (fastFrameRate) {
    App3.SetFrameRateFactor(FRAME_RATE_TESTING, false);
  }
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  onDestroy
});
