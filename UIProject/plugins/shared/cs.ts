type CsList<T> = {
    Count?: number,
    Length?: number,
    get_Item(index: number): T
};

/** 遍历 C# 的 List（Count）或数组（Length） */
export function forEach<T>(list: CsList<T>, act: (value: T, index: number) => unknown): void {
    if (!list) return;

    let count = 'Count' in list ? list.Count : list.Length;
    for (let i = 0; i < count; i++) {
        act(list.get_Item(i), i);
    }
}

/** C# 的 List / 数组转成 JS 数组 */
export function toArray<T>(list: CsList<T>): T[] {
    let result: T[] = [];
    forEach(list, value => result.push(value));
    return result;
}

/** JS 字符串数组转成 C# string[] */
export function toStringArray(list: string[]): CS.System.Array$1<string> {
    let array = CS.System.Array.CreateInstance(puerts.$typeof(CS.System.String), list.length);
    list.forEach((value, i) => array.SetValue(value, i));
    return array as CS.System.Array$1<string>;
}
