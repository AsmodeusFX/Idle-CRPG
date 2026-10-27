// 编辑器 UI 控件辅助函数

/**
 * 读取输入框（NumericInput / ResourceInput 等 GLabel 派生控件）正在编辑的文字。
 *
 * 注意：editor.d.ts 中只声明了 setter 的属性（如 ResourceInput.text、NumericInput.text）在 puerts 中没有 getter 绑定，
 * 读取它们会使编辑器原生崩溃（不会抛 JS 异常），必须读内部 title 文本框。
 * 另外 NumericInput.value 只在失焦时更新，输入/拖动过程中只有文字在变。
 */
export function inputText(input: CS.FairyGUI.GComponent): string {
    let titleField = input.GetChild('title');
    let display = titleField && titleField.displayObject;
    if (display instanceof CS.FairyGUI.RichTextField || display instanceof CS.FairyGUI.TextField) return display.text;

    return (input as CS.FairyGUI.GLabel).title;
}

/** 输入框是否有焦点（正在输入时不应写回数据，否则面板刷新会打断输入） */
export function isEditing(input: CS.FairyGUI.GComponent): boolean {
    let titleField = input.GetChild('title');
    return !!(titleField && titleField.focused);
}

/** 自定义数据解析为对象；空字符串为 {}，不是 JSON 对象时为 null */
export function parseCustomData(customData: string): Record<string, any> | null {
    if (!customData) return {};

    try {
        let obj = JSON.parse(customData);
        return obj && typeof obj === 'object' && !Array.isArray(obj) ? obj : null;
    } catch (e) {
        return null;
    }
}
