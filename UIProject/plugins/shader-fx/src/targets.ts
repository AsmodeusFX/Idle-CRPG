// 特效作用的网格（NGraphics）及其尺寸，与运行时 ShaderFx.GetGraphics 保持一致：
//   图片            图片网格，尺寸 = 对象尺寸
//   动画 / 装载器    实际显示内容（FairyGUI Image / MovieClip）的网格，尺寸 = 内容尺寸；装载器内容不是图片/动画时不支持
//   组件            绘画模式（由 main.ts 管理），尺寸 = 对象尺寸
import FairyEditor = CS.FairyEditor;
import FairyGUI = CS.FairyGUI;

const T = FairyEditor.FObjectType;

export interface Mesh {
    graphics: FairyGUI.NGraphics;
    width: number;
    height: number;
}

/** image, movieclip, loader */
export function isMeshType(obj: FairyEditor.FObject): boolean {
    let type = obj.objectType;
    return type === T.IMAGE || type === T.MOVIECLIP || type === T.LOADER;
}

/** content of movieclip / loader; null if not an Image (e.g. loader with component content) */
function getImageContent(obj: FairyEditor.FObject): FairyGUI.Image | null {
    let display = obj.displayObject;
    let content = display && display.content;
    return content instanceof FairyGUI.Image && content.graphics ? content : null;
}

/** mesh of non-component object; null if unsupported */
export function getMesh(obj: FairyEditor.FObject): Mesh | null {
    if (obj.objectType === T.IMAGE) {
        let bitmap = (obj as FairyEditor.FImage).bitmap;
        return bitmap && bitmap.graphics ? { graphics: bitmap.graphics, width: obj.width, height: obj.height } : null;
    }
    let content = getImageContent(obj);
    return content ? { graphics: content.graphics, width: content.width, height: content.height } : null;
}
