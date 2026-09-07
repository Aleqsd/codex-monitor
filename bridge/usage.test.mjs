import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { PassThrough, Writable } from 'node:stream';
import { UsageObserver, normalizeUsage } from './usage.mjs';

const now = Date.now();
const window = { usedPercent: 52, windowDurationMins: 10080, resetsAt: Math.floor(now / 1000) + 600 };
const result = { rateLimitsByLimitId: { codex: { primary: window }, codex_bengalfox: { primary: { ...window, usedPercent: 0 } } },
  accountId: 'private-account', rateLimitResetCredits: { availableCount: 3 } };

test('main Codex bucket wins over Spark and legacy; private account/credit data excluded', () => {
  const value = normalizeUsage({ ...result, rateLimits: { primary: { ...window, usedPercent: 90 } } }, now);
  assert.equal(value.windows[0].remainingPercent, 48);
  assert.deepEqual(Object.keys(value), ['fetchedAt', 'windows']);
  assert.equal(JSON.stringify(value).includes('private-account'), false);
  assert.equal(normalizeUsage({ rateLimits: { limitId: 'codex_bengalfox', primary: window } }), null);
});
test('missing percentage is unknown, real zero survives, and out-of-range values clamp', () => {
  const read = w => normalizeUsage({ rateLimits: { primary: w } }, now);
  for (const usedPercent of [undefined, null, '52', NaN, Infinity]) assert.equal(read({ ...window, usedPercent }), null);
  assert.equal(read({ ...window, usedPercent: 100 }).windows[0].remainingPercent, 0);
  assert.equal(read({ ...window, usedPercent: -5 }).windows[0].remainingPercent, 100);
  assert.equal(read({ ...window, usedPercent: 105 }).windows[0].remainingPercent, 0);
  assert.equal(read({ usedPercent: 52 }).windows[0].windowDurationMins, null);
});

function fixture(t, options = {}) {
  const sent = []; const children = [];
  const observer = new UsageObserver({ executable: 'codex.exe', now: () => now, ...options, spawnProcess(exe, args, config) {
    assert.deepEqual(args, ['app-server', '--listen', 'stdio://']);
    assert.equal(config.windowsHide, true); assert.equal(config.stdio[2], 'ignore');
    const child = new EventEmitter(); child.stdout = new PassThrough();
    child.stdin = new Writable({ write(data, _, done) { sent.push(JSON.parse(data.toString())); done(); } });
    child.kill = () => { child.killed = true; child.emit('close'); };
    children.push(child); return child;
  } });
  t.after(() => observer.stop());
  const reply = (value, error) => children.at(-1).stdout.write(JSON.stringify({ id: sent.at(-1).id, result: value, error }) + '\n');
  const initialize = () => { observer.tick(); reply({ userAgent: 'test' }); };
  return { observer, sent, children, reply, initialize };
}
test('stdio handshake is ordered and only quota reads follow initialization', t => {
  const f = fixture(t); f.initialize();
  assert.deepEqual(f.sent.map(m => m.method), ['initialize', 'initialized', 'account/rateLimits/read']);
  f.reply(result); assert.equal(f.observer.snapshot().windows[0].remainingPercent, 48);
  f.observer.tick(); f.observer.tick();
  assert.equal(f.sent.length, 4); assert.equal(f.children.length, 1);
  assert.throws(() => f.observer.send('turn/start'), /Unsupported/);
});
test('read failure clears previous quota, kills owned helper, and next poll reconnects', t => {
  const f = fixture(t); f.initialize(); f.reply(result); f.observer.tick(); f.reply(null, { code: -1 });
  assert.equal(f.observer.snapshot(), null); assert.equal(f.children[0].killed, true);
  f.initialize(); f.reply(result); assert.equal(f.children.length, 2); assert.ok(f.observer.snapshot());
});
test('stale data expires and helper shutdown stops further requests', t => {
  let clock = now; const f = fixture(t, { now: () => clock }); f.initialize(); f.reply(result);
  clock += 120001; assert.equal(f.observer.snapshot(), null);
  f.observer.stop(); f.observer.tick(); assert.equal(f.children.length, 1); assert.equal(f.children[0].killed, true);
});
test('timeouts release the helper and permit recovery', async t => {
  const f = fixture(t, { timeoutMs: 15 }); f.observer.tick();
  await new Promise(resolve => setTimeout(resolve, 35));
  assert.equal(f.observer.pending, null); assert.equal(f.children[0].killed, true);
  f.initialize(); f.reply(result); assert.ok(f.observer.snapshot());
});
test('fragmented responses work; malformed or oversized output closes the helper', t => {
  const f = fixture(t); f.observer.tick();
  f.children[0].stdout.write('{"id":1,"result":'); f.children[0].stdout.write('{} }\n');
  f.reply(result); assert.ok(f.observer.snapshot());
  f.children[0].stdout.write('invalid\n'); assert.equal(f.observer.snapshot(), null);
  f.observer.tick(); f.children[1].stdout.write('x'.repeat(1024 * 1024 + 1)); assert.equal(f.children[1].killed, true);
});
test('absent CLI and spawn errors are non-fatal to task monitoring', t => {
  const f = fixture(t, { executable: null }); f.observer.tick(); assert.equal(f.children.length, 0);
  const observer = new UsageObserver({ executable: 'missing.exe', spawnProcess() { throw new Error('ENOENT'); } });
  t.after(() => observer.stop()); assert.doesNotThrow(() => observer.tick()); assert.equal(observer.snapshot(), null);
});

test('quota diagnostics distinguish missing CLI, authentication, protocol, stale data and recovery without leaking error text', t => {
  const missing = fixture(t, { executable: null }); assert.equal(missing.observer.diagnostic().status, 'cliMissing');
  let clock = now; const f = fixture(t, { now: () => clock });
  f.initialize(); f.reply(null, { code: 401, message: 'private-account expired token' });
  assert.deepEqual(f.observer.diagnostic(), { status: 'authRequired' });
  f.initialize(); f.reply(null, { code: -32601, message: 'private protocol details' });
  assert.equal(f.observer.diagnostic().status, 'protocolError');
  f.initialize(); f.reply({}); assert.equal(f.observer.diagnostic().status, 'unsupported');
  f.observer.tick(); f.reply(result); assert.equal(f.observer.diagnostic().status, 'ready');
  clock += 120001; assert.equal(f.observer.diagnostic().status, 'stale');
  f.observer.stop(); assert.equal(f.observer.diagnostic().status, 'stopped');
});
