#!/usr/bin/env node
// Local hotfixes for @openfairygui/* 0.7.0. Idempotent; runs on `npm install` (postinstall).
// Each patch fails loudly when its anchor is missing so an upgraded package gets re-reviewed.
"use strict";
const fs = require("node:fs");
const path = require("node:path");

const root = path.join(__dirname, "..", "node_modules", "@openfairygui");

const patches = [
  {
    // Bug: hasFullUamFidelity() creates the first ProjectWriter.write() promise, then
    // materializeUamProject() can throw synchronously before Promise.all() subscribes.
    // The orphaned rejection crashes the whole MCP server (e.g. duplicate resource entries
    // in package.xml after the FGUI editor auto-registers files). Run the writes sequentially.
    id: "backend-fidelity-unhandled-rejection",
    files: ["backend/dist/runtime-gStewf1p.mjs", "backend/dist/runtime-DYqG77ak.cjs"],
    find: /await Promise\.all\(\[(new (?:_openfairygui_core_project_io\.)?ProjectWriter\(createCaptureFileSystem\(sourceFiles, sourceDirectories\)\)\.write\(document, "Project\.fairy"\)), (new (?:_openfairygui_core_project_io\.)?ProjectWriter\(createCaptureFileSystem\(materializedFiles, materializedDirectories\)\)\.write\(.+?\(project\), "Project\.fairy"\))\]\);/,
    replace: "await $1; await $2;",
    applied: /await new (?:_openfairygui_core_project_io\.)?ProjectWriter\(createCaptureFileSystem\(sourceFiles, sourceDirectories\)\)\.write\(document, "Project\.fairy"\); await /,
  },
  {
    // Bug: the FGUI editor writes settings/Publish.json codeGeneration.packageName = null,
    // but the readSessionState output schema only allows string, so every
    // openfairygui_backend_read_session_state call fails with backend_unhandled_error.
    id: "read-schema-codegen-packagename-null",
    files: ["backend/dist/docs.cjs", "backend/dist/docs.mjs"],
    find: /("__type_d4a3b6df84_read": \{[\s\S]*?"packageName": )\{ "type": "string" \}/,
    replace: '$1{ "type": ["string", "null"] }',
    applied: /"__type_d4a3b6df84_read": \{[\s\S]*?"packageName": \{ "type": \["string", "null"\] \}/,
  },
];

let failed = false;
for (const patch of patches) {
  for (const rel of patch.files) {
    const file = path.join(root, rel);
    if (!fs.existsSync(file)) {
      console.error(`[ofgui-patch] ${patch.id}: missing ${rel} (package version changed?)`);
      failed = true;
      continue;
    }
    const src = fs.readFileSync(file, "utf8");
    if (patch.applied.test(src)) {
      console.log(`[ofgui-patch] ${patch.id}: already applied to ${rel}`);
      continue;
    }
    if (!patch.find.test(src)) {
      console.error(`[ofgui-patch] ${patch.id}: anchor not found in ${rel} (package version changed?)`);
      failed = true;
      continue;
    }
    fs.writeFileSync(file, src.replace(patch.find, patch.replace));
    console.log(`[ofgui-patch] ${patch.id}: patched ${rel}`);
  }
}
process.exitCode = failed ? 1 : 0;
