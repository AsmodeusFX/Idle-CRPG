#!/usr/bin/env node
/**
 * OpenFairyGUI MCP 连接自检。
 *
 * 以 MCP 客户端相同的方式（stdio）拉起 scripts/ofgui-mcp.cmd，
 * 完成 initialize / notifications/initialized / tools/list 握手，
 * 校验 stdout 是否为干净的 JSON-RPC 报文。
 *
 * 用法（由 scripts/ofgui-verify.cmd 调用，无需手工指定 node）：
 *   node scripts/verify-ofgui-mcp.mjs
 *   node scripts/verify-ofgui-mcp.mjs --launcher "D:\path\to\ofgui-mcp.cmd"
 */
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.dirname(here);

function parseArgs(argv) {
	const out = { launcher: path.join(here, 'ofgui-mcp.cmd'), timeoutMs: 45000 };
	for (let i = 0; i < argv.length; i++) {
		const a = argv[i];
		if (a === '--launcher') out.launcher = argv[++i];
		else if (a === '--timeout') out.timeoutMs = Number(argv[++i]);
	}
	return out;
}

const args = parseArgs(process.argv.slice(2));

function fail(message, extra) {
	console.error(`[verify] FAIL: ${message}`);
	if (extra) console.error(extra);
	process.exit(1);
}

if (!existsSync(args.launcher)) fail(`找不到启动器 ${args.launcher}`);

const shell = process.env.ComSpec || 'cmd.exe';
const child = spawn(shell, ['/c', args.launcher], {
	cwd: projectRoot,
	stdio: ['pipe', 'pipe', 'pipe'],
	windowsVerbatimArguments: false,
});

const pending = new Map();
const rawLines = [];
let nonProtocolLines = 0;
let buffer = '';
let stderrText = '';
let nextId = 1;

child.stdout.setEncoding('utf8');
child.stderr.setEncoding('utf8');

child.stdout.on('data', (chunk) => {
	buffer += chunk;
	let idx;
	while ((idx = buffer.indexOf('\n')) >= 0) {
		const line = buffer.slice(0, idx).replace(/\r$/, '');
		buffer = buffer.slice(idx + 1);
		if (!line.trim()) continue;
		rawLines.push(line);
		let msg;
		try {
			msg = JSON.parse(line);
		} catch {
			nonProtocolLines++;
			console.error(`[verify] 非 JSON-RPC 输出: ${line.slice(0, 200)}`);
			continue;
		}
		if (msg.id != null && pending.has(msg.id)) {
			const { resolve } = pending.get(msg.id);
			pending.delete(msg.id);
			resolve(msg);
		}
	}
});

child.stderr.on('data', (chunk) => {
	stderrText += chunk;
});

child.on('exit', (code) => {
	for (const { reject } of pending.values()) {
		reject(new Error(`MCP 进程提前退出, code=${code}`));
	}
	pending.clear();
});

function request(method, params) {
	const id = nextId++;
	const payload = { jsonrpc: '2.0', id, method };
	if (params !== undefined) payload.params = params;
	return new Promise((resolve, reject) => {
		const timer = setTimeout(() => {
			pending.delete(id);
			reject(new Error(`${method} 超时 (${args.timeoutMs}ms)`));
		}, args.timeoutMs);
		pending.set(id, {
			resolve: (msg) => {
				clearTimeout(timer);
				resolve(msg);
			},
			reject: (err) => {
				clearTimeout(timer);
				reject(err);
			},
		});
		child.stdin.write(`${JSON.stringify(payload)}\n`);
	});
}

function notify(method, params) {
	const payload = { jsonrpc: '2.0', method };
	if (params !== undefined) payload.params = params;
	child.stdin.write(`${JSON.stringify(payload)}\n`);
}

const started = Date.now();
const report = {
	launcher: args.launcher,
	nodeVersion: process.version,
	serverName: null,
	serverVersion: null,
	protocolVersion: null,
	toolCount: 0,
	sampleTools: [],
	stdoutClean: null,
	handshakeMs: null,
	ok: false,
};

try {
	const init = await request('initialize', {
		protocolVersion: '2024-11-05',
		capabilities: {},
		clientInfo: { name: 'ofgui-toolchain-selfcheck', version: '1.0.0' },
	});
	if (init.error) throw new Error(`initialize 返回错误: ${JSON.stringify(init.error)}`);
	report.serverName = init.result?.serverInfo?.name ?? null;
	report.serverVersion = init.result?.serverInfo?.version ?? null;
	report.protocolVersion = init.result?.protocolVersion ?? null;

	notify('notifications/initialized');

	const tools = await request('tools/list', {});
	if (tools.error) throw new Error(`tools/list 返回错误: ${JSON.stringify(tools.error)}`);
	const list = tools.result?.tools ?? [];
	report.toolCount = list.length;
	report.sampleTools = list.slice(0, 6).map((t) => t.name);
	report.handshakeMs = Date.now() - started;
	report.stdoutClean = nonProtocolLines === 0;
	report.ok = Boolean(report.serverName) && report.toolCount > 0 && report.stdoutClean === true;
} catch (err) {
	report.error = err instanceof Error ? err.message : String(err);
} finally {
	try {
		child.stdin.end();
	} catch {}
	setTimeout(() => {
		try {
			if (child.exitCode === null) child.kill();
		} catch {}
	}, 300).unref();
}

const stderrTail = stderrText.split(/\r?\n/).filter(Boolean).slice(-6);
if (stderrTail.length) report.stderrTail = stderrTail;

console.log(JSON.stringify(report, null, 2));

if (report.ok) {
	console.log('[verify] OK: MCP 服务可从外部安装目录启动并完成协议握手');
	process.exit(0);
}
console.error('[verify] FAIL: MCP 自检未通过');
process.exit(1);
