import { genCode } from './GenCode_CSharp';
import FairyEditor = CS.FairyEditor;

function onPublish(handler: FairyEditor.PublishHandler) {
    if (!handler.genCode) return;
    handler.genCode = false; //prevent default output

    console.log('Handling gen code in plugin ts');
    genCode(handler); //do it myself
}

function onDestroy() {

}

export { onPublish, onDestroy };
