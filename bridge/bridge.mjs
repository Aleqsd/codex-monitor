import { DatabaseSync } from 'node:sqlite';
import { readdirSync, mkdirSync, writeFileSync, renameSync, existsSync, unlinkSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { createServer } from 'node:http';
import { CodexObserver } from './observer.mjs';
import { UsageObserver, findCodexExecutable } from './usage.mjs';
import { appendEvent } from './files.mjs';

const relayVersion = '0.10.0';

const args = process.argv.slice(2);
const value = (name, fallback) => { const index = args.indexOf(name); return index < 0 ? fallback : args[index + 1]; };
const once = args.includes('--once');
const duration = Number(value('--duration', once ? '8' : '0'));
const port = Number(value('--port', '43187'));
const limit = Number(value('--limit', '20'));
const parentPid = Number(value('--parent-pid', '0'));
if (!Number.isSafeInteger(parentPid) || parentPid < 0 || !Number.isInteger(port) || port < 1 || port > 65535 || !Number.isInteger(limit) || limit < 1 || limit > 100 || !Number.isFinite(duration) || duration < 0) {
  throw new Error('Invalid --port, --limit, or --duration');
}
const codexRoot = path.resolve(value('--codex-home', process.env.CODEX_HOME || path.join(os.homedir(), '.codex')));
const outputRoot = path.resolve(value('--output', path.join(path.dirname(fileURLToPath(import.meta.url)), 'runtime')));
mkdirSync(outputRoot, { recursive: true });
const stopFile = path.join(outputRoot, 'stop');
if (existsSync(stopFile)) unlinkSync(stopFile);
const databases = readdirSync(codexRoot).filter(name => /^state_\d+\.sqlite$/.test(name))
  .sort((a, b) => Number(b.match(/\d+/)[0]) - Number(a.match(/\d+/)[0]));
if (!databases.length) throw new Error('Codex state database not found');
const db = new DatabaseSync(path.join(codexRoot, databases[0]), { readOnly: true });
db.exec('PRAGMA query_only=ON; PRAGMA busy_timeout=1000;');
const columns = new Set(db.prepare('PRAGMA table_info(threads)').all().map(column => column.name));
if (!['id', 'title', 'cwd', 'updated_at', 'archived'].every(column => columns.has(column))) throw new Error('Unsupported Codex thread schema');
const title = columns.has('name') ? "COALESCE(NULLIF(name,''),title)" : 'title';
const model = columns.has('model') ? 'model' : 'NULL AS model';
const select = `SELECT id, ${title} AS title, cwd, ${model} FROM threads WHERE archived=0`;
const catalogQuery = db.prepare(`${select} ORDER BY updated_at DESC LIMIT ?`);
const idQuery = db.prepare(`${select} AND id=?`);
function catalog() {
  const rows = new Map(catalogQuery.all(limit).map(row => [row.id, row]));
  try {
    for (const name of readdirSync(path.join(codexRoot, 'thread-writer-locks'))) {
      if (!/^[0-9a-f-]{36}\.lock$/i.test(name)) continue;
      const row = idQuery.get(name.slice(0, -5));
      if (row) rows.set(row.id, row);
    }
  } catch (error) { if (error.code !== 'ENOENT') throw error; }
  return [...rows.values()];
}

const observer = new CodexObserver();
const usage = new UsageObserver({ executable: findCodexExecutable(value('--codex-exe', null)), codexHome: codexRoot });
const snapshot = () => ({ ...observer.snapshot(), relayVersion, usage: usage.snapshot(), quotaDiagnostic: usage.diagnostic() });
let saveTimer;
let stopping = false;
function save() {
  const temp = path.join(outputRoot, 'status.tmp');
  writeFileSync(temp, JSON.stringify(snapshot(), null, 2), 'utf8');
  renameSync(temp, path.join(outputRoot, 'status.json'));
}
observer.on('change', () => { saveTimer ??= setTimeout(() => { saveTimer = null; save(); }, 200); });
usage.on('change', () => { if (!stopping) saveTimer ??= setTimeout(() => { saveTimer = null; save(); }, 200); });
observer.on('status', event => {
  appendEvent(outputRoot, event);
  if (!once) console.log(JSON.stringify(event));
});
observer.setCatalog(catalog());
observer.connect();
usage.start();
const refreshTimer = setInterval(async () => {
  try { observer.setCatalog(catalog()); await observer.discover(); }
  catch (error) { observer.lastError = `Catalog read failed: ${error.message}`; observer.emit('change'); }
}, 10000);
const stopTimer = setInterval(() => { if (existsSync(stopFile)) shutdown(); }, 500);
// A game crash cannot leave the plugin-owned relay running indefinitely. Signal 0 only checks existence.
const parentTimer = parentPid ? setInterval(() => {
  try { process.kill(parentPid, 0); }
  catch (error) { if (error.code === 'ESRCH') shutdown(); }
}, 1000) : null;

const server = once ? null : createServer((req, res) => {
  if (req.headers.host !== `127.0.0.1:${port}` && req.headers.host !== `localhost:${port}`) { res.writeHead(403); res.end(); return; }
  if (req.headers.origin && req.headers.origin !== `http://${req.headers.host}`) { res.writeHead(403); res.end(); return; }
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  if (req.method !== 'GET') { res.writeHead(405, { Allow: 'GET' }); res.end(); return; }
  if (req.url !== '/api/threads' && req.url !== '/health') { res.writeHead(404); res.end(); return; }
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.end(JSON.stringify(req.url === '/health' ? { service: 'CodexMonitor', relayVersion, connected: observer.connected, schemaVersion: 1 } : snapshot()));
});
server?.on('error', error => { console.error(error.message); process.exitCode = 1; shutdown(); });
server?.listen(port, '127.0.0.1', () => console.log(`Local read-only endpoint: http://127.0.0.1:${port}/api/threads`));

function shutdown() {
  if (stopping) return;
  stopping = true;
  clearInterval(refreshTimer);
  clearInterval(stopTimer);
  clearInterval(parentTimer);
  clearTimeout(saveTimer);
  if (once) console.log(JSON.stringify(snapshot(), null, 2));
  usage.stop();
  observer.stop();
  server?.close();
  db.close();
  setTimeout(() => {
    observer.connected = false;
    for (const row of observer.rows.values()) row.availability = 'disconnected';
    clearTimeout(saveTimer);
    save();
    process.exit(process.exitCode ?? 0);
  }, 300);
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
if (duration > 0) setTimeout(shutdown, duration * 1000);
