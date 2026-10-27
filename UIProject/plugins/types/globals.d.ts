// 编辑器 puerts 环境在运行时注入、但 editor.d.ts / puerts.d.ts 未声明的全局对象
// （console 由编辑器 Scripts/Puerts/Main.js 注入，输出到编辑器控制台）
declare const console: {
    log(...args: any[]): void;
    info(...args: any[]): void;
    warn(...args: any[]): void;
    error(...args: any[]): void;
};
