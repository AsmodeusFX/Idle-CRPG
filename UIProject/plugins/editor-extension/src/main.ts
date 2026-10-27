
import FairyEditor = CS.FairyEditor;
import FairyGUI = CS.FairyGUI;
import Vector3 = CS.UnityEngine.Vector3;

function forEach<T>(lst: { Count?: number, Length?: number, get_Item: (i: number) => T }, act: (v: T) => any) {
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

// function Visit(comp: FairyGUI.GComponent, act: (c: FairyGUI.GObject, path: string) => any) {
//     let path = comp.name
//     forEach(comp.GetChildren(), c => {
//         act(c, path);
//         if (c instanceof FairyGUI.GComponent) {
//             Visit(c, (c, p) => act(c, path + "/" + p));
//         }
//     });
// }
function OnVisitComponent(comp: FairyEditor.FComponent) {
    forEach(comp.children, c => {
        if (c instanceof FairyEditor.FTextField) {
            // 模板属性支持title和自定义属性
            if (c.varsEnabled) {
                let props = {}
                props["title"] = comp.text

                forEach(comp.customProperties, prop => {
                    props[prop.target] = prop.value
                });

                var textField = c.displayObject["content"] as FairyGUI.TextField
                textField.text = c.text.replaceAll(/\{([^=\}]+)=?([^\}]*)\}/g, (_, k, defaultVar) => {
                    if (props[k] != undefined) return props[k]
                    return defaultVar
                })
                textField.Redraw()
            }

        }
    });
}

function RefreshDocView() {
    let doc = FairyEditor.App.activeDoc;
    if (!doc) return;

    doc.content.children.ForEach(c => {
        if (c instanceof FairyEditor.FComponent) {
            OnVisitComponent(c)
        }
    })
}

function OnEditorUpdate(e: FairyGUI.EventContext) {
    RefreshDocView();
}

FairyEditor.App.On(FairyEditor.EditorEvents.SelectionChanged, OnEditorUpdate)
FairyEditor.App.On(FairyEditor.EditorEvents.DocumentActivated, OnEditorUpdate)
function onDestroy() {
    FairyEditor.App.Off(FairyEditor.EditorEvents.SelectionChanged, OnEditorUpdate)
    FairyEditor.App.Off(FairyEditor.EditorEvents.DocumentActivated, OnEditorUpdate)
}

export { onDestroy };
