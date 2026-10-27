// 颜色与 "#RRGGBBAA" 字符串互转（自定义数据、控件比较用）

/** 解析 "#RRGGBB" / "#RRGGBBAA"（# 可省略），返回 [r, g, b, a]（0~1）；无效时返回白色 */
export function parseColor(s: string): [number, number, number, number] {
    let hex = String(s || '').replace('#', '');
    if (hex.length === 6) {
        hex += 'FF';
    }

    let value = parseInt(hex, 16);
    if (hex.length !== 8 || isNaN(value)) return [1, 1, 1, 1];

    return [(value >>> 24 & 255) / 255, (value >>> 16 & 255) / 255, (value >>> 8 & 255) / 255, (value & 255) / 255];
}

/** [r, g, b, a]（0~1）转 "#RRGGBBAA" */
export function colorToHex(color: ArrayLike<number>): string {
    let toHex = (channel: number) => ('0' + Math.round(Math.max(0, Math.min(1, channel)) * 255).toString(16)).slice(-2);
    return ('#' + toHex(color[0]) + toHex(color[1]) + toHex(color[2]) + toHex(color[3])).toUpperCase();
}
