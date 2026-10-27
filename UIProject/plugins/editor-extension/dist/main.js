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

// editor-extension/src/main.ts
var main_exports = {};
__export(main_exports, {
  onDestroy: () => onDestroy
});
module.exports = __toCommonJS(main_exports);
var FairyEditor = CS.FairyEditor;
function forEach(lst, act) {
  if ("Count" in lst) {
    for (let i = 0; i < lst.Count; i++) {
      let ele = lst.get_Item(i);
      act(ele);
    }
  } else if ("Length" in lst) {
    for (let i = 0; i < lst.Length; i++) {
      let ele = lst.get_Item(i);
      act(ele);
    }
  }
}
function OnVisitComponent(comp) {
  forEach(comp.children, (c) => {
    if (c instanceof FairyEditor.FTextField) {
      if (c.varsEnabled) {
        let props = {};
        props["title"] = comp.text;
        forEach(comp.customProperties, (prop) => {
          props[prop.target] = prop.value;
        });
        var textField = c.displayObject["content"];
        textField.text = c.text.replaceAll(/\{([^=\}]+)=?([^\}]*)\}/g, (_, k, defaultVar) => {
          if (props[k] != void 0)
            return props[k];
          return defaultVar;
        });
        textField.Redraw();
      }
    }
  });
}
function RefreshDocView() {
  let doc = FairyEditor.App.activeDoc;
  if (!doc)
    return;
  doc.content.children.ForEach((c) => {
    if (c instanceof FairyEditor.FComponent) {
      OnVisitComponent(c);
    }
  });
}
function OnEditorUpdate(e) {
  RefreshDocView();
}
FairyEditor.App.On(FairyEditor.EditorEvents.SelectionChanged, OnEditorUpdate);
FairyEditor.App.On(FairyEditor.EditorEvents.DocumentActivated, OnEditorUpdate);
function onDestroy() {
  FairyEditor.App.Off(FairyEditor.EditorEvents.SelectionChanged, OnEditorUpdate);
  FairyEditor.App.Off(FairyEditor.EditorEvents.DocumentActivated, OnEditorUpdate);
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  onDestroy
});
