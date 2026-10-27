// 特效注册表。面板（inspector.ts）与预览（main.ts）都由这里的声明自动生成。
//
// 新增一种特效（以 xxx 为例）：
//   1. Shader（游戏与编辑器共用）：在 Packages/com.bbbirder.fairygui-shaderfx/Runtime/Shaders/ 下新建 Fx_Xxx.cginc
//      （uniform + ApplyXxx(col, fxPos)，参考 Fx_Sweep.cginc），并新建游戏用 shader
//      Runtime/Resources/Shaders/FairyGUI-Fx-Xxx.shader（复制扫光的，换 include 与 FX_FRAG）。
//   2. 编辑器补丁：在 BundleBuilder/Assets/Shaders/FairyGUI-Image.shader 里加变量、include，并在 ApplyAllFx 中按强度调用，
//      保证“关闭值”（off）下完全不生效；重新打包、用 patcher/patch.py 生成补丁并安装。
//   3. 运行时：在包的 Runtime/Effects/ 下加 XxxEffect.cs（参考 SweepEffect.cs），并在 ShaderFx 静态构造里 Register。
//   4. 在下面的 EFFECTS 里加一项（面板和预览不需要改代码）。
import { parseColor } from '../../shared/color';

export type Vec4 = [number, number, number, number];
export type Uniforms = Record<string, Vec4>;

export interface FieldDef {
    key: string;
    label: string;
    type: 'color' | 'number';
    default: string | number;
    min?: number;
    max?: number;
    step?: number;
    digits?: number;
}

export interface EffectDef {
    /** 写入自定义数据的 fx 值，运行时也按它识别 */
    id: string;
    /** 下拉框里显示的名字 */
    label: string;
    /** 面板参数 */
    fields: FieldDef[];
    /** 参数 → shader 变量；color 参数已转换为 [r, g, b, a]（0~1），width/height 为对象尺寸 */
    uniforms(params: Record<string, any>, width: number, height: number): Uniforms;
    /** 关闭该特效时写入的 shader 变量（切换或删除特效时使用） */
    off: Uniforms;
    /** 由 fields 生成的默认值 */
    defaults?: Record<string, string | number>;
}

export const EFFECTS: EffectDef[] = [
    {
        id: 'sweep',
        label: '扫光',
        fields: [
            { key: 'color', label: '颜色', type: 'color', default: '#FFFFFFCC' },
            { key: 'width', label: '宽度', type: 'number', min: 0.01, max: 1, step: 0.01, digits: 2, default: 0.15 },
            { key: 'angle', label: '角度', type: 'number', min: -180, max: 180, step: 1, digits: 0, default: 30 },
            { key: 'duration', label: '扫过时长', type: 'number', min: 0.05, max: 10, step: 0.05, digits: 2, default: 0.8 },
            { key: 'interval', label: '间隔', type: 'number', min: 0, max: 10, step: 0.1, digits: 2, default: 1.5 },
            { key: 'intensity', label: '亮度', type: 'number', min: 0, max: 5, step: 0.1, digits: 2, default: 1 },
            { key: 'softness', label: '柔和度', type: 'number', min: 0, max: 1, step: 0.05, digits: 2, default: 1 },
        ],
        uniforms: (params, width, height) => ({
            _FxRect: [width, height, 0, 0],
            _SweepColor: params.color,
            _SweepParam: [params.width, params.angle, params.duration, params.interval],
            _SweepParam2: [params.intensity, params.softness, 0, 0],
        }),
        off: { _SweepColor: [0, 0, 0, 0] },
    },
];

export const BY_ID: Record<string, EffectDef> = {};
for (const effect of EFFECTS) {
    effect.defaults = {};
    for (const field of effect.fields) {
        effect.defaults[field.key] = field.default;
    }
    BY_ID[effect.id] = effect;
}

/** 自定义数据里属于特效的键（切换/关闭特效时清除，其余键保留） */
export const ALL_KEYS: string[] = ['fx'];
for (const effect of EFFECTS) {
    for (const field of effect.fields) {
        if (ALL_KEYS.indexOf(field.key) < 0) {
            ALL_KEYS.push(field.key);
        }
    }
}

/** 合并默认值，并把 color 参数转为 [r, g, b, a]，供 uniforms() 使用 */
export function resolveParams(effect: EffectDef, data: Record<string, any>): Record<string, any> {
    let params: Record<string, any> = Object.assign({}, effect.defaults);
    for (const field of effect.fields) {
        let value = data[field.key];
        if (value === undefined) continue;

        params[field.key] = field.type === 'number' ? Number(value) : value;
    }
    for (const field of effect.fields) {
        if (field.type === 'color') params[field.key] = parseColor(params[field.key]);
    }
    return params;
}
