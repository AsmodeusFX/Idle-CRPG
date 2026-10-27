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

// spine-loader/src/main.ts
var main_exports = {};
__export(main_exports, {
  onDestroy: () => onDestroy
});
module.exports = __toCommonJS(main_exports);

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

// shared/inspectorOrder.ts
var FairyGUI = CS.FairyGUI;
var TITLE_BAR_URL = "ui://2pshu6oiitme7iudp";
function isTitleBar(obj) {
  return obj.resourceURL === TITLE_BAR_URL;
}
function sectionTitle(item) {
  return isTitleBar(item) ? item.title : null;
}
function findList(panel) {
  for (let p = panel.parent; p; p = p.parent) {
    if (p instanceof FairyGUI.GList)
      return p;
  }
  return null;
}
function describeInspectorList(panel) {
  let ancestors = [];
  for (let p = panel.parent; p; p = p.parent) {
    ancestors.push((p.name || "?") + (p instanceof FairyGUI.GList ? "[GList]" : ""));
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

// shared/color.ts
function colorToHex(color) {
  let toHex = (channel) => ("0" + Math.round(Math.max(0, Math.min(1, channel)) * 255).toString(16)).slice(-2);
  return ("#" + toHex(color[0]) + toHex(color[1]) + toHex(color[2]) + toHex(color[3])).toUpperCase();
}

// spine-loader/src/panel.ts
var FairyEditor = CS.FairyEditor;
var FairyGUI2 = CS.FairyGUI;
var App = CS.FairyEditor.App;
var INSPECTOR_NAME = "Loader3DProps";
var URL_PANEL = "ui://2pshu6oitwdeixicvm";
var T = FairyEditor.FObjectType;
var FILL_ITEM_VALUES = ["none", "scale", "scaleMatchHeight", "scaleMatchWidth", "scaleFree", "scaleNoBorder"];
var FILL_ITEM_LABELS = ["\u65E0", "\u6BD4\u4F8B\u7F29\u653E(\u663E\u793A\u5168\u90E8)", "\u6BD4\u4F8B\u7F29\u653E(\u5339\u914D\u9AD8\u5EA6)", "\u6BD4\u4F8B\u7F29\u653E(\u5339\u914D\u5BBD\u5EA6)", "\u81EA\u7531\u7F29\u653E", "\u6BD4\u4F8B\u7F29\u653E(\u65E0\u8FB9\u6846)"];
var ALIGN_ITEM_VALUES = ["left", "center", "right"];
var VALIGN_ITEM_VALUES = ["top", "middle", "bottom"];
var MISSING_SUFFIX = " (\u7F3A\u5931)";
var NATIVE_SECTION_PATTERN = /^loader3d$/i;
var PLACE_BEFORE = "gear";
var hiddenNativeName = null;
function getSkeletonInfo(loader) {
  try {
    let res = loader.contentRes;
    let item = res && res.packageItem;
    let asset = item && item.GetAsset();
    if (asset instanceof FairyEditor.SkeletonAsset) {
      let data = asset.isLoaded ? asset.data : null;
      return {
        animations: data ? toArray(data.GetAnimations()) : [],
        skins: data ? toArray(data.GetSkins()) : [],
        asset
      };
    }
  } catch (e) {
  }
  return { animations: [], skins: [], asset: null };
}
function getStateFromLoader(loader) {
  return {
    url: loader.url || "",
    clearOnPublish: !!loader.clearOnPublish,
    autoSize: !!loader.autoSize,
    fill: loader.fill || "none",
    shrinkOnly: !!loader.shrinkOnly,
    align: loader.align || "left",
    verticalAlign: loader.verticalAlign || "top",
    color: colorToHex([loader.color.r, loader.color.g, loader.color.b, loader.color.a]),
    animationName: loader.animationName || "",
    skinName: loader.skinName || "",
    playing: !!loader.playing,
    loop: !!loader.loop,
    frame: loader.frame
  };
}
function register(logger2) {
  const panel = FairyGUI2.UIPackage.CreateObjectFromURL(URL_PANEL).asCom;
  const panelControls = {
    url: panel.GetChild("url"),
    clearOnPublish: panel.GetChild("clearOnPublish"),
    autoSize: panel.GetChild("autoSize"),
    fill: panel.GetChild("fill"),
    shrinkOnly: panel.GetChild("shrinkOnly"),
    align: panel.GetController("align"),
    verticalAlign: panel.GetController("verticalAlign"),
    color: panel.GetChild("color"),
    animationName: panel.GetChild("animationName"),
    skinName: panel.GetChild("skinName"),
    playing: panel.GetChild("playing"),
    loop: panel.GetChild("loop"),
    frame: panel.GetChild("frame")
  };
  setComboBoxItems(panelControls.fill, FILL_ITEM_LABELS);
  panelControls.frame.min = 0;
  panelControls.frame.max = 1e5;
  panelControls.frame.step = 1;
  panelControls.frame.fractionDigits = 0;
  let target = null;
  let prevStateKey = null;
  let animComboBoxItems = [""];
  let skinComboBoxItems = [""];
  let pendingAsset = null;
  let nativeChecked = false;
  let orderLogged = false;
  function hideNativeSection() {
    if (nativeChecked)
      return;
    let list = findList(panel);
    if (!list)
      return;
    nativeChecked = true;
    for (let i = 0; i < list.numChildren; i++) {
      let item = list.GetChildAt(i);
      if (item !== panel && NATIVE_SECTION_PATTERN.test(item.name || "")) {
        hiddenNativeName = item.name;
        break;
      }
    }
    if (!hiddenNativeName) {
      logger2.warn("native loader3d section not found: " + describeInspectorList(panel));
      return;
    }
    App.docFactory.disconnectInspector(hiddenNativeName);
    logger2.log("native inspector disconnected: " + hiddenNativeName);
    if (App.activeDoc && target) {
      App.activeDoc.SetSelection(target);
    }
  }
  let updating = false;
  const warned = /* @__PURE__ */ new Set();
  function getSelectedLoader() {
    let doc = App.activeDoc;
    let list = doc ? toArray(doc.inspectingTargets) : [];
    if (list.length !== 1)
      return null;
    let obj = list[0];
    return obj.objectType === T.Loader3D ? obj : null;
  }
  function setComboBoxItems(combo, labels) {
    combo.items = toStringArray(labels);
    combo.visibleItemCount = 15;
  }
  function setComboBoxValue(combo, values, labels, value) {
    let index = values.indexOf(value);
    if (index < 0 && value) {
      values.push(value);
      labels.push(value + MISSING_SUFFIX);
      index = values.length - 1;
    }
    setComboBoxItems(combo, labels);
    combo.selectedIndex = Math.max(0, index);
  }
  function setControlsState(inputState) {
    logger2.trace("fill");
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
    pendingAsset = skeletonInfo.asset && !skeletonInfo.asset.isLoaded ? skeletonInfo.asset : null;
    if (pendingAsset && !pendingAsset.isLoading) {
      pendingAsset.Load();
    }
    logger2.trace(`skeletonInfo ${skeletonInfo.animations.length} animations, ${skeletonInfo.skins.length} skins, pending=${!!pendingAsset}`);
    animComboBoxItems = [""].concat(skeletonInfo.animations);
    skinComboBoxItems = [""].concat(skeletonInfo.skins);
    setComboBoxValue(panelControls.animationName, animComboBoxItems, ["(\u65E0)"].concat(skeletonInfo.animations), inputState.animationName);
    setComboBoxValue(panelControls.skinName, skinComboBoxItems, ["(\u9ED8\u8BA4)"].concat(skeletonInfo.skins), inputState.skinName);
  }
  function readControl_Url() {
    let text = inputText(panelControls.url) || "";
    return text === "" || text.indexOf("ui://") === 0 ? text : target ? target.url || "" : "";
  }
  function readControl_Frame() {
    let v = parseInt(inputText(panelControls.frame), 10);
    return isNaN(v) ? panelControls.frame.value : Math.max(0, v);
  }
  function getStateFromControls() {
    return {
      url: readControl_Url(),
      clearOnPublish: panelControls.clearOnPublish.selected,
      autoSize: panelControls.autoSize.selected,
      fill: FILL_ITEM_VALUES[panelControls.fill.selectedIndex] || "none",
      shrinkOnly: panelControls.shrinkOnly.selected,
      align: ALIGN_ITEM_VALUES[panelControls.align.selectedIndex] || "left",
      verticalAlign: VALIGN_ITEM_VALUES[panelControls.verticalAlign.selectedIndex] || "top",
      color: colorToHex([panelControls.color.colorValue.r, panelControls.color.colorValue.g, panelControls.color.colorValue.b, panelControls.color.colorValue.a]),
      animationName: animComboBoxItems[panelControls.animationName.selectedIndex] || "",
      skinName: skinComboBoxItems[panelControls.skinName.selectedIndex] || "",
      playing: panelControls.playing.selected,
      loop: panelControls.loop.selected,
      frame: readControl_Frame()
    };
  }
  function updateUI() {
    if (updating) {
      logger2.trace("updateUI re-entered, skipped");
      return !!target;
    }
    updating = true;
    try {
      target = getSelectedLoader();
      if (!target)
        return false;
      setControlsState(getStateFromLoader(target));
      prevStateKey = JSON.stringify(getStateFromControls());
      return true;
    } finally {
      updating = false;
    }
  }
  function setLoaderProperty(name, value) {
    try {
      target.docElement.SetProperty(name, value);
    } catch (e) {
      target[name] = value;
      App.activeDoc.SetModified(true);
      if (!warned.has(name)) {
        warned.add(name);
        logger2.warn(`SetProperty(${name}) failed, assigned directly: ${e?.message || e}`);
      }
    }
  }
  function poll() {
    if (!target || !panel.onStage)
      return;
    hideNativeSection();
    if (!placeSectionBefore(panel, PLACE_BEFORE) && !orderLogged) {
      orderLogged = true;
      logger2.log(`inspector order: ${describeInspectorList(panel)}`);
    }
    if (pendingAsset && pendingAsset.isLoaded) {
      pendingAsset = null;
      updateUI();
      return;
    }
    if (isEditing(panelControls.url) || isEditing(panelControls.frame))
      return;
    let controlsState = getStateFromControls();
    let key = JSON.stringify(controlsState);
    if (key === prevStateKey)
      return;
    prevStateKey = key;
    let loaderState = getStateFromLoader(target);
    for (const name of Object.keys(controlsState)) {
      if (controlsState[name] === loaderState[name])
        continue;
      logger2.trace(`write ${name}: ${JSON.stringify(loaderState[name])} -> ${JSON.stringify(controlsState[name])}`);
      setLoaderProperty(name, name === "color" ? panelControls.color.colorValue : controlsState[name]);
    }
  }
  App.inspectorView.AddInspector(() => {
    let inspector = new FairyEditor.View.PluginInspector();
    inspector.panel = panel;
    inspector.updateAction = updateUI;
    return inspector;
  }, INSPECTOR_NAME, "3D\u88C5\u8F7D\u5668");
  App.docFactory.ConnectInspector(INSPECTOR_NAME, T.Loader3D, false, false);
  logger2.log("loader3d inspector registered");
  return { poll };
}
function unregister() {
  if (hiddenNativeName) {
    App.docFactory.ConnectInspector(hiddenNativeName, T.Loader3D, false, false);
    hiddenNativeName = null;
  }
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

// spine-loader/src/main.ts
var App3 = CS.FairyEditor.App;
var TRACE = false;
var logger = createLogger("spine-loader", "Loader3D", TRACE);
var disposed = false;
var panelApi = null;
var reportedError = false;
function onUpdate() {
  if (disposed || !panelApi)
    return;
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
  App3.remove_onUpdate(onUpdate);
  try {
    unregister();
  } catch (e) {
    logger.warn("unregister: " + errorText(e));
  }
}
try {
  panelApi = register(logger);
} catch (e) {
  logger.error("register: " + errorText(e));
}
App3.add_onUpdate(onUpdate);
logger.log("ready");
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  onDestroy
});
