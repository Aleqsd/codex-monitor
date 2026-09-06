import { DatabaseSync } from 'node:sqlite';
import { readdirSync, mkdirSync, writeFileSync, renameSync, appendFileSync, existsSync, unlinkSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { createServer } from 'node:http';
import { CodexObserver } from './observer.mjs';

const args = process.argv.slice(2);
const value = (name, fallback) => { const index = args.indexOf(name); return index < 0 ? fallback : args[index + 1]; };
const once = args.includes('--once');
const duration = Number(value('--duration', once ? '8' : '0'));
const port = Number(value('--port', '43187'));
const limit = Number(value('--limit', '20'));
if (!Number.isInteger(port) || port < 1 || port > 65535 || !Number.isInteger(limit) || limit < 1 || limit > 100 || !Number.isFinite(duration) || duration < 0) {
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
let saveTimer;
let stopping = false;
function save() {
  const temp = path.join(outputRoot, 'status.tmp');
  writeFileSync(temp, JSON.stringify(observer.snapshot(), null, 2), 'utf8');
  renameSync(temp, path.join(outputRoot, 'status.json'));
}
observer.on('change', () => { saveTimer ??= setTimeout(() => { saveTimer = null; save(); }, 200); });
observer.on('status', event => {
  appendFileSync(path.join(outputRoot, 'events.jsonl'), JSON.stringify(event) + '\n', 'utf8');
  if (!once) console.log(JSON.stringify(event));
});
observer.setCatalog(catalog());
observer.connect();
const refreshTimer = setInterval(async () => {
  try { observer.setCatalog(catalog()); await observer.discover(); }
  catch (error) { observer.lastError = `Catalog read failed: ${error.message}`; observer.emit('change'); }
}, 10000);
const stopTimer = setInterval(() => { if (existsSync(stopFile)) shutdown(); }, 500);

const server = once ? null : createServer((req, res) => {
  if (req.headers.host !== `127.0.0.1:${port}` && req.headers.host !== `localhost:${port}`) { res.writeHead(403); res.end(); return; }
  if (req.headers.origin && req.headers.origin !== `http://${req.headers.host}`) { res.writeHead(403); res.end(); return; }
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  if (req.method !== 'GET') { res.writeHead(405, { Allow: 'GET' }); res.end(); return; }
  if (req.url !== '/api/threads' && req.url !== '/health') { res.writeHead(404); res.end(); return; }
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.end(JSON.stringify(req.url === '/health' ? { connected: observer.connected, schemaVersion: 1 } : observer.snapshot()));
});
server?.on('error', error => { console.error(error.message); process.exitCode = 1; shutdown(); });
server?.listen(port, '127.0.0.1', () => console.log(`Local read-only endpoint: http://127.0.0.1:${port}/api/threads`));

function shutdown() {
  if (stopping) return;
  stopping = true;
  clearInterval(refreshTimer);
  clearInterval(stopTimer);
  clearTimeout(saveTimer);
  if (once) console.log(JSON.stringify(observer.snapshot(), null, 2));
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
