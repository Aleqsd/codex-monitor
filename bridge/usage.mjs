import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { EventEmitter } from 'node:events';
import path from 'node:path';

export const USAGE_MAX_AGE_MS = 120_000;

// Only the main Codex allowance. Never substitute Spark or expose account/credit data.
export function normalizeUsage(result, now = Date.now()) {
  const bucket = result?.rateLimitsByLimitId?.codex ?? result?.rateLimits;
  if (!bucket || (bucket.limitId && bucket.limitId !== 'codex')) return null;
  const windows = [bucket.primary, bucket.secondary].flatMap(window => {
    if (!window || typeof window.usedPercent !== 'number' || !Number.isFinite(window.usedPercent)) return [];
    const duration = window.windowDurationMins;
    const reset = window.resetsAt;
    return [{ remainingPercent: Math.max(0, Math.min(100, 100 - window.usedPercent)),
      windowDurationMins: Number.isSafeInteger(duration) && duration > 0 ? duration : null,
      resetsAt: Number.isSafeInteger(reset) && reset > 0 && reset < 253402300800 ? reset : null }];
  });
  return windows.length ? { fetchedAt: new Date(now).toISOString(), windows } : null;
}

export function findCodexExecutable(explicit, env = process.env) {
  if (explicit) return path.resolve(explicit);
  const candidates = (env.PATH ?? env.Path ?? '').split(path.delimiter).filter(Boolean)
    .map(directory => path.join(directory, process.platform === 'win32' ? 'codex.exe' : 'codex'));
  if (env.APPDATA) candidates.push(path.join(env.APPDATA, 'npm', 'node_modules', '@openai', 'codex', 'node_modules',
    '@openai', 'codex-win32-x64', 'vendor', 'x86_64-pc-windows-msvc', 'bin', 'codex.exe'));
  return candidates.find(candidate => existsSync(candidate)) ?? null;
}

// One owned stdio helper, no listener and no task/turn/login/reset commands.
export class UsageObserver extends EventEmitter {
  constructor({ executable, codexHome, spawnProcess = spawn, now = Date.now, pollMs = 60_000, timeoutMs = 15_000 } = {}) {
    super(); Object.assign(this, { executable, codexHome, spawnProcess, now, pollMs, timeoutMs });
    this.value = null; this.child = null; this.pending = null; this.sequence = 0; this.stopped = false;
  }
  snapshot() {
    return this.value && Math.abs(this.now() - Date.parse(this.value.fetchedAt)) <= USAGE_MAX_AGE_MS ? this.value : null;
  }
  start() {
    if (this.interval || this.stopped) return;
    this.tick(); this.interval = setInterval(() => this.tick(), this.pollMs);
  }
  tick() {
    if (this.stopped || this.pending || !this.executable) return;
    if (this.child) { this.send('account/rateLimits/read'); return; }
    try {
      const child = this.spawnProcess(this.executable, ['app-server', '--listen', 'stdio://'], {
        windowsHide: true, stdio: ['pipe', 'pipe', 'ignore'],
        env: { ...process.env, ...(this.codexHome ? { CODEX_HOME: this.codexHome } : {}) },
      });
      this.child = child; let buffer = '';
      child.on('error', () => { if (this.child === child) this.fail(); });
      child.on('close', () => { if (this.child === child) this.fail(); });
      child.stdin.on('error', () => { if (this.child === child) this.fail(); });
      child.stdout.on('data', data => {
        if (this.child !== child) return;
        buffer += data.toString('utf8');
        if (buffer.length > 1024 * 1024) { this.fail(); return; }
        let end;
        while ((end = buffer.indexOf('\n')) >= 0) {
          const line = buffer.slice(0, end); buffer = buffer.slice(end + 1);
          try { this.receive(JSON.parse(line)); } catch { this.fail(); return; }
        }
      });
      this.send('initialize', { clientInfo: { name: 'dalamud_quota_observer', title: 'Codex Monitor', version: '0.5.0' } });
    } catch { this.fail(); }
  }
  send(method, params) {
    if (!['initialize', 'account/rateLimits/read'].includes(method)) throw new Error('Unsupported quota request');
    const id = ++this.sequence;
    this.pending = { id, method };
    this.timeout = setTimeout(() => this.fail(), this.timeoutMs);
    this.child.stdin.write(JSON.stringify({ id, method, ...(params ? { params } : {}) }) + '\n');
  }
  receive(message) {
    if (!this.pending || message.id !== this.pending.id) return;
    const { method } = this.pending; this.pending = null; clearTimeout(this.timeout);
    if (message.error || !message.result) { this.fail(); return; }
    if (method === 'initialize') {
      this.child.stdin.write(JSON.stringify({ method: 'initialized' }) + '\n');
      this.send('account/rateLimits/read');
    } else { this.value = normalizeUsage(message.result, this.now()); this.emit('change'); }
  }
  fail() {
    clearTimeout(this.timeout); this.pending = null; this.value = null;
    const child = this.child; this.child = null;
    child?.kill(); this.emit('change');
  }
  stop() { this.stopped = true; clearInterval(this.interval); this.interval = null; this.fail(); }
}
