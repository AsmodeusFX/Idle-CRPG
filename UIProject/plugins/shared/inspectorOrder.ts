// every inspector section composed of: TitleBar + panel

import FairyGUI = CS.FairyGUI;

const TITLE_BAR_URL = 'ui://2pshu6oiitme7iudp'; // Builder/basic/TitleBar.xml

/** Is target a TitleBar */
function isTitleBar(obj: FairyGUI.GObject): boolean {
    return obj.resourceURL === TITLE_BAR_URL;
}

/** 列表项是标题栏时返回其标题，否则 null */
function sectionTitle(item: FairyGUI.GObject): string | null {
    return isTitleBar(item) ? (item as FairyGUI.GButton).title : null;
}

/** 面板所在的区块列表：从面板向上最近的 GList */
export function findList(panel: FairyGUI.GComponent): FairyGUI.GList | null {
    for (let p = panel.parent; p; p = p.parent) {
        if (p instanceof FairyGUI.GList) return p;
    }
    return null;
}

/** 返回描述面板祖先层级与区块列表结构的字符串（调试用） */
export function describeInspectorList(panel: FairyGUI.GComponent): string {
    let ancestors: string[] = [];
    for (let p = panel.parent; p; p = p.parent) {
        ancestors.push((p.name || '?') + (p instanceof FairyGUI.GList ? '[GList]' : ''));
    }

    let list = findList(panel);
    if (!list) return 'no list; ancestors: ' + ancestors.join(' < ');

    let parts: string[] = ['ancestors: ' + ancestors.join(' < '), 'items:'];
    for (let i = 0; i < list.numChildren; i++) {
        let item = list.GetChildAt(i);
        let title = sectionTitle(item);
        parts.push(`${i}:${item.name || '?'}${title !== null ? '「' + title + '」' : ''}`);
    }
    return parts.join(' ');
}

/**
 * 把 panel 所在区块（标题栏 + 面板）移到内部名为 beforeName 的区块之前。
 * @returns 是否找到目标并处于（或已移到）正确位置
 */
export function placeSectionBefore(panel: FairyGUI.GComponent, beforeName: string): boolean {
    let list = panel.onStage ? findList(panel) : null;
    if (!list) return false;

    // 区块在列表中的项：从面板向上找到列表的直接子对象，前一项若是标题栏则一并移动
    let item: FairyGUI.GObject = panel;
    while (item && item.parent && item.parent !== list) {
        item = item.parent;
    }
    if (!item || item.parent !== list) return false;

    let index = list.GetChildIndex(item);
    let prev = index > 0 ? list.GetChildAt(index - 1) : null;
    let items = prev && isTitleBar(prev) ? [prev, item] : [item];

    // 目标区块的起始下标（其标题栏，若有）
    let targetPanel = list.GetChild(beforeName);
    if (!targetPanel) return false;

    let target = list.GetChildIndex(targetPanel);
    if (target > 0 && isTitleBar(list.GetChildAt(target - 1))) {
        target--;
    }

    // 已紧挨在目标区块之前
    if (list.GetChildIndex(items[items.length - 1]) === target - 1) return true;

    // 逐个插到目标之前（移出后下标会变化，每次重新计算）
    for (const it of items) {
        let from = list.GetChildIndex(it);
        let to = from < target ? target - 1 : target;
        list.SetChildIndex(it, to);
        target = to + 1;
    }
    return true;
}
