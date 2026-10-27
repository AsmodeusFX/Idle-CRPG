import CodeWriter from './CodeWriter';
import FairyEditor = CS.FairyEditor;
import XML = CS.FairyGUI.Utils.XML;
import File = CS.System.IO.File;

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

function genCode(handler: FairyEditor.PublishHandler) {
    let settings = (<FairyEditor.GlobalPublishSettings>handler.project.GetSettings("Publish")).codeGeneration;
    let codePkgName = "UI_" + handler.ToFilename(handler.pkg.name); //convert chinese to pinyin, remove special chars etc.
    let exportCodePath = handler.exportCodePath + '/' + codePkgName;
    let namespaceName = codePkgName;
    let isMonoGame = handler.project.type == FairyEditor.ProjectType.MonoGame;

    if (settings.packageName)
        namespaceName = settings.packageName + '.' + namespaceName;

    //CollectClasses(stripeMemeber, stripeClass, fguiNamespace)
    let classes = handler.CollectClasses(settings.ignoreNoname, false, null);
    handler.SetupCodeFolder(exportCodePath, "cs"); //check if target folder exists, and delete old files

    let getMemberByName = settings.getMemberByName;
    let classCnt = classes.Count;
    let writer = new CodeWriter();
    for (let i: number = 0; i < classCnt; i++) {
        let classInfo = classes.get_Item(i);
        let members = classInfo.members;
        let customProperties = [] as { target: string, propertyId: number, label: string }[]
        let xmlFile = classInfo.res.file
        let xml = new XML(File.ReadAllText(xmlFile))
        let controllerDetails = {} as { [n: string]: { alias: string, pages: { name: string, remark: string }[] } };
        forEach(xml.Elements("customProperty"), prop => {
            customProperties.push({
                target: prop.GetAttribute("target"),
                propertyId: prop.GetAttributeInt("propertyId"),
                label: prop.GetAttribute("label"),
            })
        })

        forEach(xml.Elements("controller"), prop => {
            let item = controllerDetails[prop.GetAttribute("name")] = {
                alias: prop.GetAttribute("alias"),
                pages: prop.GetAttribute("pages").split(",").filter((v, i) => i % 2 == 1).map(v => ({
                    name: v,
                    remark: "",
                })),
                // target: prop.GetAttribute("target"),
                // propertyId: prop.GetAttributeInt("propertyId"),
                // label: prop.GetAttribute("label"),
            }

            forEach(prop.Elements("remark"), p => {
                item.pages[Number.parseInt(p.GetAttribute("page"))].remark = p.GetAttribute("value");
            })
        })

        writer.reset();

        writer.writeln('using System.Collections.Generic;');
        writer.writeln('using System.Linq;');
        writer.writeln('using System.Threading;');
        writer.writeln('using BBBirder.UnityVue;');
        writer.writeln('using Cysharp.Threading.Tasks;');
        writer.writeln('using FairyGUI;');
        writer.writeln('using FairyGUI.Utils;');
        writer.writeln();
        writer.writeln(`namespace ${namespaceName}`);
        writer.startBlock();
        let superClassName = classInfo.superClassName;
        if (classInfo.className.endsWith("Tips") || classInfo.className.endsWith("Window"))
            superClassName = 'CustomWindow';
        writer.writeln(`public partial class ${classInfo.className} : ${superClassName}, ICustomUI`);
        writer.startBlock();
        writer.writeln('public const string URL = "ui://%s%s";', handler.pkg.id, classInfo.resId);

        writer.writeln(`static ${classInfo.className}()`);
        writer.startBlock();
        writer.writeln(`PackageItemInfo.Register(typeof(${classInfo.className}),"${handler.pkg.name}","${classInfo.resName}");`);
        writer.endBlock();
        writer.writeln();

        writer.writeln(`public static ${classInfo.className} Instance { get; private set; }`);
        writer.writeln();
        writer.writeln(`private CancellationTokenSource cancellationTokenSource;`);
        writer.writeln(`public CancellationToken disposeCancellationToken => (cancellationTokenSource ??= new()).Token;`);
        writer.writeln(`public static implicit operator CancellationToken (${classInfo.className} self)`);
        writer.startBlock();
        writer.writeln(`return self.disposeCancellationToken;`);
        writer.endBlock();
        writer.writeln();

        writer.writeln('#pragma warning disable CS0108, CS0114');
        let memberCnt = members.Count
        for (let j: number = 0; j < memberCnt; j++) {
            let memberInfo = members.get_Item(j);
            // let varName = memberInfo.varName.charAt(0).toUpperCase() + memberInfo.varName.slice(1) // 首字母大写
            let varName = memberInfo.varName;
            let varType = memberInfo.type;
            let name = memberInfo.name;
            let index = memberInfo.index;

            // 如果可以，修正为其他包的类型
            if (memberInfo.res && memberInfo.res.exported) {
                if (memberInfo.res.owner != handler.pkg) {
                    let otherNs = "UI_" + handler.ToFilename(memberInfo.res.owner.name);
                    let otherType = settings.classNamePrefix + memberInfo.res.name;
                    varType = `${otherNs}.${otherType}`;
                }
            }

            writer.writeln(`private ${varType} @__backingField_${varName};`);

            if (memberInfo.group == 0) {
                if (getMemberByName)
                    writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})this.GetChild("${name}", ${index});`);
                else
                    writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})GetChildAt(${index});`);
            } else if (memberInfo.group == 1) {
                let details = controllerDetails[varName];

                if (details?.alias) {
                    writer.writeln(`/// <summary>`);
                    writer.writeln(`/// ${details.alias}`);
                    writer.writeln(`/// </summary>`);
                }

                // if (getMemberByName)
                //     writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})GetController("${name}");`);
                // else
                writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})GetControllerAt(${index});`);
            } else {
                // if (getMemberByName)
                //     writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})GetTransition("${name}");`);
                // else
                writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})GetTransitionAt(${index});`);
            }
        }
        writer.writeln('#pragma warning restore CS0108, CS0114');
        writer.writeln();

        writer.writeln(`public Dictionary<string, string> CustomProperties { get; set; } = new();`);
        for (let prop of customProperties) {
            let propTarget = prop.target.replace(".", "_");
            writer.writeln(`/// <summary>`);
            writer.writeln(`/// custom property \`${prop.label}\``);
            writer.writeln(`/// </summary>`);
            writer.writeln(`public string v_${propTarget}`);
            writer.startBlock();
            writer.writeln(`get => CustomProperties.GetValueOrDefault("${propTarget}");`);
            writer.writeln(`set`);
            writer.startBlock();
            writer.writeln(`if(value == v_${propTarget}) return;`);
            writer.writeln(`CustomProperties["${propTarget}"] = value;`);
            writer.writeln(`OnCustomPropertyChanged("${propTarget}", value);`);
            writer.writeln(`this.UpdateTemplateVars();`);
            writer.endBlock(); // setter
            writer.endBlock(); // accessors
            writer.writeln();
        }

        for (let name in controllerDetails) {
            if (name == "button") continue;

            var detail = controllerDetails[name];
            if (detail.pages.length == 2 && !detail.pages.find(p => p.name)) {
                // regard as bool state

                writer.writeln(`public bool ${name}_State`);
                writer.startBlock();
                writer.writeln(`get => ${name}.selectedIndex != 0;`);
                writer.writeln(`set => ${name}.selectedIndex = value ? 1 : 0;`);
                writer.endBlock();
                writer.writeln();
            } else {
                if (detail.pages.find(p => !p.name)) continue;
                // regard as enum state

                writer.writeln(`public enum E_${name}`);
                writer.startBlock();
                for (let p of detail.pages) {
                    if (p.remark) {
                        writer.writeln(`/// <summary>`);
                        writer.writeln(`/// ${p.remark}`);
                        writer.writeln(`/// </summary>`);
                    }
                    writer.writeln(`@${p.name},`);
                }
                writer.endBlock();
                writer.writeln();

                writer.writeln(`public E_${name} ${name}_State`);
                writer.startBlock();
                writer.writeln(`get => (E_${name})${name}.selectedIndex;`);
                writer.writeln(`set => ${name}.selectedIndex = (int)value;`);
                writer.endBlock();
                writer.writeln();
            }
        }

        writer.writeln('public static %s CreateInstance()', classInfo.className);
        writer.startBlock();
        writer.writeln('return (%s)UIPackage.CreateObject("%s", "%s");', classInfo.className, handler.pkg.name, classInfo.resName);
        writer.endBlock();
        writer.writeln();

        writer.writeln(`public override void Setup_AfterAdd(ByteBuffer buffer, int beginPos)`);
        writer.startBlock();
        writer.writeln(`base.Setup_AfterAdd(buffer, beginPos);`);
        writer.writeln(`this.UpdateTemplateVars();`);
        writer.endBlock();
        writer.writeln();

        if (isMonoGame) {
            writer.writeln("protected override void OnConstruct()");
            writer.startBlock();
        }
        else {
            writer.writeln('public override void ConstructFromXML(XML xml)');
            writer.startBlock();
            writer.writeln('base.ConstructFromXML(xml);');
            writer.writeln();
        }

        writer.writeln(`PreInit();`);
        writer.writeln(`UniTask.Post(OnInternalPostInit, PlayerLoopTiming.PostLateUpdate);`);
        writer.endBlock();
        writer.writeln();

        writer.writeln(`private void OnInternalPostInit()`);
        writer.startBlock();
        writer.writeln(`Instance = this;`);
        writer.writeln(`if (!isDisposed) Init();`);
        writer.endBlock();
        writer.writeln();

        writer.writeln(`public override void Dispose()`);
        writer.startBlock();
        writer.writeln(`Deinit();`)
        writer.writeln(`base.Dispose();`)
        writer.writeln();
        writer.writeln(`if (cancellationTokenSource != null)`)
        writer.startBlock();
        writer.writeln(`cancellationTokenSource.Cancel();`)
        writer.writeln(`cancellationTokenSource.Dispose();`)
        writer.writeln(`cancellationTokenSource = null;`)
        writer.endBlock(); // if (cancellationTokenSource != null)
        writer.writeln();
        writer.writeln(`if(ReferenceEquals(this, Instance))`)
        writer.startBlock();
        writer.writeln("Instance = null;")
        writer.endBlock(); // if (cancellationTokenSource != null)
        writer.endBlock(); // public override void Dispose()
        writer.writeln();

        writer.writeln(`partial void OnCustomPropertyChanged(string propertyName, string value);`);
        // writer.writeln(`new public partial void Init();`);
        writer.writeln('#pragma warning disable CS0108');
        writer.writeln(`partial void PreInit();`);
        writer.writeln(`partial void Init();`);
        writer.writeln(`partial void Deinit();`);
        writer.writeln('#pragma warning restore CS0108');

        writer.endBlock(); //class
        writer.endBlock(); //namepsace

        writer.save(exportCodePath + '/' + classInfo.className + '.cs');
    }

    writer.reset();

    let binderName = codePkgName + 'Binder';

    writer.writeln('using FairyGUI;');
    writer.writeln('using UnityEngine;');
    writer.writeln();
    writer.writeln('namespace %s', namespaceName);
    writer.startBlock();
    writer.writeln('partial class %s', binderName);
    writer.startBlock();

    writer.writeln('[RuntimeInitializeOnLoadMethod]');
    writer.writeln('public static void BindAll()');
    writer.startBlock();
    for (let i: number = 0; i < classCnt; i++) {
        let classInfo = classes.get_Item(i);
        writer.writeln('UIObjectFactory.SetPackageItemExtension(%s.URL, typeof(%s));', classInfo.className, classInfo.className);
    }
    writer.endBlock(); //bindall

    writer.endBlock(); //class
    writer.endBlock(); //namespace

    writer.save(exportCodePath + '/' + binderName + '.cs');

    // output manifest file
    writer.reset();

    writer.writeln("using System.Collections.Generic;")
    writer.writeln()
    writer.writeln("public enum UIPackages")
    writer.startBlock();
    FairyEditor.App.project.allPackages.ForEach(pkg => {
        writer.writeln(`Package_${pkg.name},`)
    });
    writer.endBlock();
    writer.writeln();

    writer.writeln("public enum UIComponents")
    writer.startBlock();
    let packageItems = [] as FairyEditor.FPackageItem[];
    FairyEditor.App.project.allPackages.ForEach(pkg => {
        pkg.items.ForEach(item => {
            if (item.type == "component")
                packageItems.push(item);
        })
    });
    for (let pkgItem of packageItems) {
        writer.writeln(`${pkgItem.name}_of_${pkgItem.owner.name},`)
    }
    writer.endBlock();
    writer.writeln();


    writer.writeln("public static class UI_ManifestExtension")
    writer.startBlock();
    writer.writeln("private static List<(string, string)> names = new() {")
    writer.incIndent();
    for (let pkgItem of packageItems) {
        writer.writeln(`("${pkgItem.owner.name}", "${pkgItem.name}"),`)
    }
    writer.decIndent();
    writer.writeln("};"); //names
    writer.writeln("public static (string pkg, string comp) GetUIName(this UIComponents comps)"); //names
    writer.startBlock();
    writer.writeln("return names[(int)comps];"); //names
    writer.endBlock(); //GetUIName

    writer.endBlock(); //UI_ManifestExtension

    writer.save(`${handler.exportCodePath}/UI_Manifest.cs`);

}

export { genCode };
