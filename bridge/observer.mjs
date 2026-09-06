import net from 'node:net';
import { randomUUID } from 'node:crypto';
import { EventEmitter } from 'node:events';

export const PIPE = '\\\\.\\pipe\\codex-ipc';
const MAX_FRAME_BYTES = 256 * 1024 * 1024;
const STREAM_VERSION = 11;
const SAFE_FIELDS = new Set(['title', 'generatedTitle', 'cwd', 'latestModel', 'threadRuntimeStatus']);
const FORBIDDEN_KEYS = new Set(['__proto__', 'constructor', 'prototype']);

export function encodeFrame(message) {
  const body = Buffer.from(JSON.stringify(message));
  const header = Buffer.alloc(4);
  header.writeUInt32LE(body.length);
  return Buffer.concat([header, body]);
}

export class FrameDecoder {
  header = Buffer.alloc(4);
  headerBytes = 0;
  size = 0;
  received = 0;
  chunks = [];
  constructor(onMessage) { this.onMessage = onMessage; }
  push(chunk) {
    let offset = 0;
    while (offset < chunk.length) {
      if (this.size === 0) {
        const count = Math.min(4 - this.headerBytes, chunk.length - offset);
        chunk.copy(this.header, this.headerBytes, offset, offset + count);
        this.headerBytes += count;
        offset += count;
        if (this.headerBytes < 4) return;
        this.size = this.header.readUInt32LE(0);
        this.headerBytes = 0;
        if (this.size < 1 || this.size > MAX_FRAME_BYTES) throw new Error('Invalid IPC frame size');
      }
      const count = Math.min(this.size - this.received, chunk.length - offset);
      this.chunks.push(chunk.subarray(offset, offset + count));
      offset += count;
      this.received += count;
      if (this.received === this.size) {
        const bytes = this.size;
        const body = this.chunks.length === 1 ? this.chunks[0] : Buffer.concat(this.chunks, bytes);
        this.size = 0;
        this.received = 0;
        this.chunks = [];
        this.onMessage(JSON.parse(body.toString('utf8')), bytes);
      }
    }
  }
}

export function selectFields(state) {
  return Object.fromEntries([...SAFE_FIELDS].filter(key => Object.hasOwn(state, key))
    .map(key => [key, structuredClone(state[key])]));
}

export function applyMetadataPatches(fields, patches) {
  const next = structuredClone(fields);
  for (const patch of patches) {
    const path = patch.path;
    if (!Array.isArray(path) || path.length === 0) throw new Error('Unsupported patch path');
    if (!SAFE_FIELDS.has(path[0])) continue;
    if (path.some(part => FORBIDDEN_KEYS.has(String(part)))) throw new Error('Unsafe patch path');
    if (!['add', 'replace', 'remove'].includes(patch.op)) throw new Error('Unsupported patch operation');
    let parent = next;
    for (const key of path.slice(0, -1)) {
      if (parent === null || typeof parent !== 'object' || !Object.hasOwn(parent, key)) throw new Error('Missing patch parent');
      parent = parent[key];
    }
    if (parent === null || typeof parent !== 'object') throw new Error('Invalid patch parent');
    const key = path.at(-1);
    if (Array.isArray(parent) && key !== 'length') {
      if (!Number.isInteger(key) || key < 0 || key > parent.length) throw new Error('Invalid array patch');
      if (patch.op === 'add') parent.splice(key, 0, structuredClone(patch.value));
      else if (patch.op === 'remove') parent.splice(key, 1);
      else parent[key] = structuredClone(patch.value);
    } else if (patch.op === 'remove') delete parent[key];
    else parent[key] = structuredClone(patch.value);
  }
  return next;
}

export function displayStatus(runtime) {
  if (runtime?.type === 'active') {
    const flags = Array.isArray(runtime.activeFlags) ? runtime.activeFlags : [];
    if (flags.includes('waitingOnUserInput')) return { state: 'needsInput', label: 'Réponse attendue' };
    if (flags.includes('waitingOnApproval')) return { state: 'needsApproval', label: 'Approbation attendue' };
    return { state: 'active', label: 'En cours' };
  }
  if (runtime?.type === 'idle') return { state: 'idle', label: 'Au repos' };
  if (runtime?.type === 'systemError') return { state: 'error', label: 'Erreur' };
  if (runtime?.type === 'notLoaded') return { state: 'notLoaded', label: 'Non chargée' };
  return { state: 'unknown', label: 'État inconnu' };
}

export class CodexObserver extends EventEmitter {
  rows = new Map();
  pending = new Map();
  connected = false;
  stopped = false;
  clientId = 'initializing-client';
  metrics = { receivedFrames: 0, receivedBytes: 0, snapshots: 0, patches: 0, statusChanges: 0, reconnects: 0 };
  lastError = null;
  constructor({ pipe = PIPE } = {}) { super(); this.pipe = pipe; }

  connect() {
    if (this.stopped || this.socket) return;
    const socket = net.createConnection(this.pipe);
    this.socket = socket;
    const decoder = new FrameDecoder((message, bytes) => {
      this.metrics.receivedFrames++;
      this.metrics.receivedBytes += bytes;
      this.handle(message);
    });
    socket.on('data', chunk => {
      try { decoder.push(chunk); } catch (error) { this.lastError = error.message; socket.destroy(); }
    });
    socket.on('error', error => { this.lastError = error.code ?? error.message; });
    socket.on('close', () => {
      if (this.socket !== socket) return;
      this.socket = null;
      this.connected = false;
      this.clientId = 'initializing-client';
      for (const pending of this.pending.values()) { clearTimeout(pending.timer); pending.reject(new Error('IPC disconnected')); }
      this.pending.clear();
      for (const row of this.rows.values()) { row.owner = null; row.revision = null; row.availability = 'disconnected'; }
      this.emit('change');
      if (!this.stopped) this.reconnectTimer = setTimeout(() => { this.metrics.reconnects++; this.connect(); }, 3000);
    });
    socket.once('connect', async () => {
      try {
        const init = await this.request('initialize', { clientType: 'dalamud-status-observer' }, 0);
        if (init.resultType !== 'success') throw new Error(init.error);
        this.clientId = init.result.clientId;
        this.connected = true;
        this.lastError = null;
        this.emit('change');
        await this.discover();
      } catch (error) { this.lastError = error.message; socket.destroy(); }
    });
  }

  send(message) {
    if (!this.socket?.writable) throw new Error('IPC disconnected');
    this.socket.write(encodeFrame(message));
  }

  request(method, params, version) {
    if (!['initialize', 'thread-owner-discovery'].includes(method)) throw new Error('Read-only request allowlist');
    const requestId = randomUUID();
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(requestId); reject(new Error('IPC request timeout')); }, 6000);
      this.pending.set(requestId, { resolve, reject, timer });
      try { this.send({ type: 'request', requestId, sourceClientId: this.clientId, method, params, version, timeoutMs: 5000 }); }
      catch (error) { clearTimeout(timer); this.pending.delete(requestId); reject(error); }
    });
  }

  follow(row, following) {
    if (!row.owner || !this.connected) return;
    this.send({ type: 'broadcast', sourceClientId: this.clientId, method: 'thread-stream-following-changed', version: 1,
      targetClientIds: [row.owner], params: { hostId: 'local', conversationId: row.id, following } });
  }

  setCatalog(catalog) {
    for (const entry of catalog) {
      const existing = this.rows.get(entry.id);
      if (existing) existing.catalog = entry;
      else this.rows.set(entry.id, { id: entry.id, catalog: entry, owner: null, revision: null,
        availability: this.connected ? 'discovering' : 'disconnected', fields: {}, lastEventAt: null });
    }
    const ids = new Set(catalog.map(row => row.id));
    for (const [id, row] of this.rows) if (!ids.has(id)) { this.follow(row, false); this.rows.delete(id); }
  }

  async discover() {
    if (!this.connected || this.discovering) return;
    this.discovering = true;
    try {
      for (const row of this.rows.values()) {
        if (!this.connected) break;
        try {
          const result = await this.request('thread-owner-discovery', { hostId: 'local', conversationId: row.id }, 1);
          if (result.resultType === 'success') {
            row.lastConfirmedAt = new Date().toISOString();
            if (row.owner !== result.handledByClientId || row.availability !== 'live') {
              this.follow(row, false);
              row.owner = result.handledByClientId;
              row.availability = 'awaitingSnapshot';
              this.follow(row, true);
            }
          } else {
            this.follow(row, false);
            row.owner = null;
            row.revision = null;
            row.availability = result.error === 'no-client-found' ? 'unobserved' : 'unknown';
          }
        } catch {
          row.owner = null;
          row.revision = null;
          row.availability = this.connected ? 'unknown' : 'disconnected';
        }
      }
    } finally { this.discovering = false; this.emit('change'); }
  }

  handle(message) {
    if (message.type === 'client-discovery-request') {
      this.send({ type: 'client-discovery-response', requestId: message.requestId, response: { canHandle: false } });
      return;
    }
    if (message.type === 'request') {
      this.send({ type: 'response', requestId: message.requestId, resultType: 'error', error: 'read-only-observer' });
      return;
    }
    if (message.type === 'response') {
      const pending = this.pending.get(message.requestId);
      if (pending) { this.pending.delete(message.requestId); clearTimeout(pending.timer); pending.resolve(message); }
      return;
    }
    if (message.type !== 'broadcast' || (message.targetClientIds && !message.targetClientIds.includes(this.clientId))) return;
    if (message.method === 'client-status-changed' && message.params.status === 'disconnected') {
      for (const row of this.rows.values()) if (row.owner === message.params.clientId) {
        row.owner = null; row.revision = null; row.availability = 'disconnected';
      }
      this.emit('change');
      return;
    }
    if (message.method === 'thread-stream-following-status-requested') {
      const row = this.rows.get(message.params.conversationId);
      if (row && message.params.hostId === 'local' && row.owner === message.sourceClientId) this.follow(row, true);
      return;
    }
    if (message.method !== 'thread-stream-state-changed' || message.params.hostId !== 'local') return;
    const row = this.rows.get(message.params.conversationId);
    if (!row || row.owner !== message.sourceClientId) return;
    if (message.version !== STREAM_VERSION) {
      row.availability = 'incompatible';
      this.lastError = `Unsupported stream protocol ${message.version}`;
      this.emit('change');
      return;
    }
    const change = message.params.change;
    const wasLive = row.availability === 'live';
    const before = displayStatus(row.fields.threadRuntimeStatus).state;
    try {
      if (change.type === 'snapshot') {
        row.fields = selectFields(change.conversationState);
        row.availability = 'live';
        this.metrics.snapshots++;
      } else if (change.type === 'patches' && row.availability === 'live' && change.baseRevision === row.revision) {
        row.fields = applyMetadataPatches(row.fields, change.patches);
        this.metrics.patches++;
      } else throw new Error('Stream revision gap');
      row.revision = change.revision;
      row.lastEventAt = new Date().toISOString();
      const after = displayStatus(row.fields.threadRuntimeStatus).state;
      if (!wasLive || before !== after) {
        if (wasLive) this.metrics.statusChanges++;
        this.emit('status', { id: row.id, title: row.fields.title ?? row.catalog.title,
          previous: wasLive ? before : null, current: after, initial: !wasLive, at: row.lastEventAt });
      }
    } catch (error) {
      row.availability = 'resyncing';
      row.revision = null;
      this.lastError = error.message;
      this.follow(row, false);
      this.follow(row, true);
    }
    this.emit('change');
  }

  snapshot() {
    const labels = { disconnected: 'Déconnecté', unobserved: 'Non observée', discovering: 'Recherche…',
      awaitingSnapshot: 'Connexion…', unknown: 'État inconnu', incompatible: 'Version incompatible', resyncing: 'Resynchronisation…' };
    return { schemaVersion: 1, generatedAt: new Date().toISOString(), connected: this.connected,
      source: 'Codex desktop IPC (internal, experimental)', protocolVersion: STREAM_VERSION,
      lastError: this.lastError, metrics: { ...this.metrics }, threads: [...this.rows.values()].map(row => ({
        id: row.id, title: row.fields.generatedTitle || row.fields.title || row.catalog.title,
        project: row.fields.cwd ?? row.catalog.cwd, model: row.fields.latestModel ?? row.catalog.model,
        availability: row.availability,
        ...(row.availability === 'live' ? displayStatus(row.fields.threadRuntimeStatus)
          : { state: row.availability, label: labels[row.availability] ?? 'État inconnu' }),
        runtimeStatus: row.availability === 'live' ? row.fields.threadRuntimeStatus ?? null : null,
        lastEventAt: row.lastEventAt, lastConfirmedAt: row.lastConfirmedAt ?? null, revision: row.revision,
      })) };
  }

  stop() {
    this.stopped = true;
    clearTimeout(this.reconnectTimer);
    for (const row of this.rows.values()) this.follow(row, false);
    this.socket?.end();
  }
}
