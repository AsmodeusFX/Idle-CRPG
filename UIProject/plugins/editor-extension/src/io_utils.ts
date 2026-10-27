
import Path = CS.System.IO.Path;
import File = CS.System.IO.File;
import Directory = CS.System.IO.Directory;

function writeFile(path: string, content: string) {
    var dir = Path.GetDirectoryName(path);
    if (!Directory.Exists(dir)) {
        Directory.CreateDirectory(dir);
    }
    File.WriteAllText(path, content);
}

function readFile(path: string): string {
    return File.ReadAllText(path);
}


export { readFile, writeFile }
