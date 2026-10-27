var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
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
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// code-generator/src/main.ts
var main_exports = {};
__export(main_exports, {
  onDestroy: () => onDestroy,
  onPublish: () => onPublish
});
module.exports = __toCommonJS(main_exports);

// code-generator/src/GenCode_CSharp.ts
var import_CodeWriter = __toESM(require("./CodeWriter"));
var FairyEditor = CS.FairyEditor;
var XML = CS.FairyGUI.Utils.XML;
var File = CS.System.IO.File;
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
function genCode(handler) {
  let settings = handler.project.GetSettings("Publish").codeGeneration;
  let codePkgName = "UI_" + handler.ToFilename(handler.pkg.name);
  let exportCodePath = handler.exportCodePath + "/" + codePkgName;
  let namespaceName = codePkgName;
  let isMonoGame = handler.project.type == FairyEditor.ProjectType.MonoGame;
  if (settings.packageName)
    namespaceName = settings.packageName + "." + namespaceName;
  let classes = handler.CollectClasses(settings.ignoreNoname, false, null);
  handler.SetupCodeFolder(exportCodePath, "cs");
  let getMemberByName = settings.getMemberByName;
  let classCnt = classes.Count;
  let writer = new import_CodeWriter.default();
  for (let i = 0; i < classCnt; i++) {
    let classInfo = classes.get_Item(i);
    let members = classInfo.members;
    let customProperties = [];
    let xmlFile = classInfo.res.file;
    let xml = new XML(File.ReadAllText(xmlFile));
    let controllerDetails = {};
    forEach(xml.Elements("customProperty"), (prop) => {
      customProperties.push({
        target: prop.GetAttribute("target"),
        propertyId: prop.GetAttributeInt("propertyId"),
        label: prop.GetAttribute("label")
      });
    });
    forEach(xml.Elements("controller"), (prop) => {
      let item = controllerDetails[prop.GetAttribute("name")] = {
        alias: prop.GetAttribute("alias"),
        pages: prop.GetAttribute("pages").split(",").filter((v, i2) => i2 % 2 == 1).map((v) => ({
          name: v,
          remark: ""
        }))
        // target: prop.GetAttribute("target"),
        // propertyId: prop.GetAttributeInt("propertyId"),
        // label: prop.GetAttribute("label"),
      };
      forEach(prop.Elements("remark"), (p) => {
        item.pages[Number.parseInt(p.GetAttribute("page"))].remark = p.GetAttribute("value");
      });
    });
    writer.reset();
    writer.writeln("using System.Collections.Generic;");
    writer.writeln("using System.Linq;");
    writer.writeln("using System.Threading;");
    writer.writeln("using BBBirder.UnityVue;");
    writer.writeln("using Cysharp.Threading.Tasks;");
    writer.writeln("using FairyGUI;");
    writer.writeln("using FairyGUI.Utils;");
    writer.writeln();
    writer.writeln(`namespace ${namespaceName}`);
    writer.startBlock();
    let superClassName = classInfo.superClassName;
    if (classInfo.className.endsWith("Tips") || classInfo.className.endsWith("Window"))
      superClassName = "CustomWindow";
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
    writer.writeln("#pragma warning disable CS0108, CS0114");
    let memberCnt = members.Count;
    for (let j = 0; j < memberCnt; j++) {
      let memberInfo = members.get_Item(j);
      let varName = memberInfo.varName;
      let varType = memberInfo.type;
      let name = memberInfo.name;
      let index = memberInfo.index;
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
        writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})GetControllerAt(${index});`);
      } else {
        writer.writeln(`public ${varType} ${varName} => @__backingField_${varName} ??= (${varType})GetTransitionAt(${index});`);
      }
    }
    writer.writeln("#pragma warning restore CS0108, CS0114");
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
      writer.endBlock();
      writer.endBlock();
      writer.writeln();
    }
    for (let name in controllerDetails) {
      if (name == "button")
        continue;
      var detail = controllerDetails[name];
      if (detail.pages.length == 2 && !detail.pages.find((p) => p.name)) {
        writer.writeln(`public bool ${name}_State`);
        writer.startBlock();
        writer.writeln(`get => ${name}.selectedIndex != 0;`);
        writer.writeln(`set => ${name}.selectedIndex = value ? 1 : 0;`);
        writer.endBlock();
        writer.writeln();
      } else {
        if (detail.pages.find((p) => !p.name))
          continue;
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
    writer.writeln("public static %s CreateInstance()", classInfo.className);
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
    } else {
      writer.writeln("public override void ConstructFromXML(XML xml)");
      writer.startBlock();
      writer.writeln("base.ConstructFromXML(xml);");
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
    writer.writeln(`Deinit();`);
    writer.writeln(`base.Dispose();`);
    writer.writeln();
    writer.writeln(`if (cancellationTokenSource != null)`);
    writer.startBlock();
    writer.writeln(`cancellationTokenSource.Cancel();`);
    writer.writeln(`cancellationTokenSource.Dispose();`);
    writer.writeln(`cancellationTokenSource = null;`);
    writer.endBlock();
    writer.writeln();
    writer.writeln(`if(ReferenceEquals(this, Instance))`);
    writer.startBlock();
    writer.writeln("Instance = null;");
    writer.endBlock();
    writer.endBlock();
    writer.writeln();
    writer.writeln(`partial void OnCustomPropertyChanged(string propertyName, string value);`);
    writer.writeln("#pragma warning disable CS0108");
    writer.writeln(`partial void PreInit();`);
    writer.writeln(`partial void Init();`);
    writer.writeln(`partial void Deinit();`);
    writer.writeln("#pragma warning restore CS0108");
    writer.endBlock();
    writer.endBlock();
    writer.save(exportCodePath + "/" + classInfo.className + ".cs");
  }
  writer.reset();
  let binderName = codePkgName + "Binder";
  writer.writeln("using FairyGUI;");
  writer.writeln("using UnityEngine;");
  writer.writeln();
  writer.writeln("namespace %s", namespaceName);
  writer.startBlock();
  writer.writeln("partial class %s", binderName);
  writer.startBlock();
  writer.writeln("[RuntimeInitializeOnLoadMethod]");
  writer.writeln("public static void BindAll()");
  writer.startBlock();
  for (let i = 0; i < classCnt; i++) {
    let classInfo = classes.get_Item(i);
    writer.writeln("UIObjectFactory.SetPackageItemExtension(%s.URL, typeof(%s));", classInfo.className, classInfo.className);
  }
  writer.endBlock();
  writer.endBlock();
  writer.endBlock();
  writer.save(exportCodePath + "/" + binderName + ".cs");
  writer.reset();
  writer.writeln("using System.Collections.Generic;");
  writer.writeln();
  writer.writeln("public enum UIPackages");
  writer.startBlock();
  FairyEditor.App.project.allPackages.ForEach((pkg) => {
    writer.writeln(`Package_${pkg.name},`);
  });
  writer.endBlock();
  writer.writeln();
  writer.writeln("public enum UIComponents");
  writer.startBlock();
  let packageItems = [];
  FairyEditor.App.project.allPackages.ForEach((pkg) => {
    pkg.items.ForEach((item) => {
      if (item.type == "component")
        packageItems.push(item);
    });
  });
  for (let pkgItem of packageItems) {
    writer.writeln(`${pkgItem.name}_of_${pkgItem.owner.name},`);
  }
  writer.endBlock();
  writer.writeln();
  writer.writeln("public static class UI_ManifestExtension");
  writer.startBlock();
  writer.writeln("private static List<(string, string)> names = new() {");
  writer.incIndent();
  for (let pkgItem of packageItems) {
    writer.writeln(`("${pkgItem.owner.name}", "${pkgItem.name}"),`);
  }
  writer.decIndent();
  writer.writeln("};");
  writer.writeln("public static (string pkg, string comp) GetUIName(this UIComponents comps)");
  writer.startBlock();
  writer.writeln("return names[(int)comps];");
  writer.endBlock();
  writer.endBlock();
  writer.save(`${handler.exportCodePath}/UI_Manifest.cs`);
}

// code-generator/src/main.ts
function onPublish(handler) {
  if (!handler.genCode)
    return;
  handler.genCode = false;
  console.log("Handling gen code in plugin ts");
  genCode(handler);
}
function onDestroy() {
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  onDestroy,
  onPublish
});
