// Fallback for hosts where dotnet CLI/MSBuild cannot read process metadata.
// Compiles source directly; this does NOT verify MSBuild/project integration.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sdkRoot = process.argv[2];
if (!sdkRoot) throw new Error('Usage: node scripts/verify-direct.mjs <dotnet-root> [--content-zip <path>]');
const dotnet = path.join(path.resolve(sdkRoot), process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
const latest8 = directory => fs.readdirSync(directory).filter(n => /^8\.0\./.test(n))
  .sort((a, b) => a.localeCompare(b, undefined, { numeric: true })).at(-1)
  ?? (() => { throw new Error(`No .NET 8 directory in ${directory}`); })();
const compiler = path.join(sdkRoot, 'sdk', latest8(path.join(sdkRoot, 'sdk')), 'Roslyn', 'bincore', 'csc.dll');
const refVersion = latest8(path.join(sdkRoot, 'packs', 'Microsoft.NETCore.App.Ref'));
const runtimeVersion = latest8(path.join(sdkRoot, 'shared', 'Microsoft.NETCore.App'));
const out = fs.mkdtempSync(path.join(os.tmpdir(), 'building-rotation-verify-'));
const quote = s => { if (s.includes('"') || /[\r\n]/.test(s)) throw new Error('Invalid response-file path'); return `"${s}"`; };
const dlls = dir => fs.readdirSync(dir).filter(n => n.endsWith('.dll')).map(n => path.join(dir, n));
const netstd = dlls(path.join(sdkRoot, 'packs', 'NETStandard.Library.Ref', '2.1.0', 'ref', 'netstandard2.1'));
const net8 = dlls(path.join(sdkRoot, 'packs', 'Microsoft.NETCore.App.Ref', refVersion, 'ref', 'net8.0'));
const sources = dir => fs.readdirSync(path.join(root, dir)).filter(n => n.endsWith('.cs')).sort().map(n => path.join(root, dir, n));
const usings = path.join(out, 'ImplicitUsings.cs');
fs.writeFileSync(usings, ['System', 'System.Collections.Generic', 'System.IO', 'System.Linq', 'System.Net.Http', 'System.Threading', 'System.Threading.Tasks']
  .map(n => `global using global::${n};`).join('\n'));
function run(args) {
  const r = spawnSync(dotnet, args, { cwd: root, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024,
    env: { ...process.env, DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1' } });
  if (r.stdout) process.stdout.write(r.stdout);
  if (r.stderr) process.stderr.write(r.stderr);
  if (r.error) throw r.error;
  if (r.status !== 0) process.exit(r.status ?? 1);
}
function compile(name, dir, refs, exe = false, resources = []) {
  const dll = path.join(out, name + '.dll');
  const rsp = path.join(out, name + '.rsp');
  const args = ['-nologo', '-nostdlib+', '-nullable:enable', '-warnaserror+', '-deterministic+', '-optimize+',
    '-langversion:' + (exe ? '12' : '8'), '-target:' + (exe ? 'exe' : 'library'), '-out:' + quote(dll),
    ...refs.map(r => '-reference:' + quote(r)), ...resources,
    ...sources(dir).map(quote), ...(exe ? [quote(usings)] : [])];
  fs.writeFileSync(rsp, args.join('\n'));
  run([compiler, '@' + rsp]);
  if (exe) fs.writeFileSync(path.join(out, name + '.runtimeconfig.json'), JSON.stringify({ runtimeOptions: {
    tfm: 'net8.0', framework: { name: 'Microsoft.NETCore.App', version: runtimeVersion } } }));
  return dll;
}
const core = compile('BuildingRotation.Core', 'src/BuildingRotation.Core', netstd);
const data = compile('BuildingRotation.Data', 'src/BuildingRotation.Data', [...netstd, core]);
const audit = compile('BuildingRotation.ContentAudit', 'tools/BuildingRotation.ContentAudit', [...net8, core, data], true);
const tests = compile('BuildingRotation.Core.SelfTest', 'tests/BuildingRotation.Core.SelfTest', [...net8, core, data, audit], true,
  ['-resource:' + quote(path.join(root, 'docs/reference/content-building-facts.json')) + ',BuildingRotation.ContentFacts.json']);
run([tests, ...process.argv.slice(3)]);
process.stdout.write(`Direct compilation output: ${out}\n`);
