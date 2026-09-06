import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { once } from 'node:events';
import { randomUUID } from 'node:crypto';
import { CodexObserver, FrameDecoder, encodeFrame, displayStatus, applyMetadataPatches } from './observer.mjs';

test('framing supports fragmented UTF-8 messages and coalesced frames', () => {
  const decoded = [];
  const parser = new FrameDecoder(message => decoded.push(message));
  const expected = [{ title: 'Tâche terminée 🟢' }, { type: 'idle' }];
  const bytes = Buffer.concat(expected.map(encodeFrame));
  for (let offset = 0; offset < bytes.length; offset += 3) parser.push(bytes.subarray(offset, offset + 3));
  assert.deepEqual(decoded, expected);
  const parser2 = new FrameDecoder(message => decoded.push(message));
  parser2.push(bytes);
  assert.deepEqual(decoded.slice(2), expected);
});

test('invalid frame sizes fail before parsing the payload', () => {
  for (const size of [0, 268435457, 0xffffffff]) {
    const header = Buffer.alloc(4); header.writeUInt32LE(size);
    assert.throws(() => new FrameDecoder(() => assert.fail()).push(header), /frame size/);
  }
});

test('status mapping distinguishes idle, approval, input and unknown', () => {
  assert.equal(displayStatus({ type: 'idle' }).state, 'idle');
  assert.equal(displayStatus({ type: 'active', activeFlags: ['waitingOnApproval'] }).state, 'needsApproval');
  assert.equal(displayStatus({ type: 'active', activeFlags: ['waitingOnUserInput'] }).state, 'needsInput');
  assert.equal(displayStatus({ type: 'future-status' }).state, 'unknown');
});

test('only allowlisted metadata is retained; status array patches apply', () => {
  const state = { threadRuntimeStatus: { type: 'active', activeFlags: [] }, title: 'Example' };
  const next = applyMetadataPatches(state, [
    { op: 'add', path: ['threadRuntimeStatus', 'activeFlags', 0], value: 'waitingOnApproval' },
    { op: 'add', path: ['turnHistory'], value: { secretTranscript: 'never retain this' } },
  ]);
  assert.equal(displayStatus(next.threadRuntimeStatus).state, 'needsApproval');
  assert.equal('turnHistory' in next, false);
  assert.equal(state.threadRuntimeStatus.activeFlags.length, 0);
  assert.throws(() => applyMetadataPatches(state, [{ op: 'add', path: ['threadRuntimeStatus', '__proto__', 'polluted'], value: true }]));
  assert.equal({}.polluted, undefined);
});

function streamMessage(change, version = 11) {
  return { type: 'broadcast', method: 'thread-stream-state-changed', version, sourceClientId: 'owner',
    params: { hostId: 'local', conversationId: 'thread1', change } };
}

test('revision gaps and protocol drift suppress potentially stale active states', () => {
  const observer = new CodexObserver();
  observer.setCatalog([{ id: 'thread1', title: 'Example' }]);
  const row = observer.rows.get('thread1'); row.owner = 'owner';
  observer.handle(streamMessage({ type: 'snapshot', revision: 4, conversationState: { title: 'Example', threadRuntimeStatus: { type: 'active' } } }));
  assert.equal(observer.snapshot().threads[0].state, 'active');
  observer.handle(streamMessage({ type: 'patches', baseRevision: 5, revision: 6, patches: [] }));
  assert.equal(observer.snapshot().threads[0].state, 'resyncing');
  assert.equal(observer.snapshot().threads[0].runtimeStatus, null);
  observer.handle(streamMessage({ type: 'snapshot', revision: 7, conversationState: { threadRuntimeStatus: { type: 'active' } } }, 12));
  assert.equal(observer.snapshot().threads[0].state, 'incompatible');
});

test('real pipe flow observes transitions, owner loss, transport loss and reconnection', { timeout: 7000 }, async t => {
  const pipe = `\\\\.\\pipe\\dalamud-status-test-${randomUUID()}`;
  const sent = [];
  let peer;
  let ownerPresent = true;
  const server = net.createServer(socket => {
    peer = socket;
    const send = message => socket.write(encodeFrame(message));
    const decoder = new FrameDecoder(message => {
      sent.push(message);
      if (message.method === 'initialize') send({ type: 'response', requestId: message.requestId,
        method: message.method, resultType: 'success', result: { clientId: 'observer' } });
      if (message.method === 'thread-owner-discovery') send(ownerPresent ? { type: 'response', requestId: message.requestId,
        method: message.method, resultType: 'success', handledByClientId: 'owner' }
        : { type: 'response', requestId: message.requestId, resultType: 'error', error: 'no-client-found' });
      if (message.method === 'thread-stream-following-changed' && message.params.following) {
        send(streamMessage({ type: 'snapshot', revision: 1, conversationState: {
          title: 'Example', threadRuntimeStatus: { type: 'active', activeFlags: [] },
          turnHistory: { private: 'not part of the output' },
        } }));
      }
    });
    socket.on('data', chunk => decoder.push(chunk));
  });
  await new Promise(resolve => server.listen(pipe, resolve));
  const observer = new CodexObserver({ pipe });
  t.after(() => { observer.stop(); peer?.destroy(); server.close(); });
  observer.setCatalog([{ id: 'thread1', title: 'Example' }]);
  const initial = once(observer, 'status');
  observer.connect();
  assert.equal((await initial)[0].current, 'active');
  assert.equal(JSON.stringify(observer.snapshot()).includes('private'), false);
  const transition = once(observer, 'status');
  peer.write(encodeFrame(streamMessage({ type: 'patches', baseRevision: 1, revision: 2, patches: [
    { op: 'replace', path: ['threadRuntimeStatus'], value: { type: 'idle' } },
  ] })));
  assert.deepEqual((await transition)[0].current, 'idle');
  assert.equal(observer.metrics.statusChanges, 1);
  const disconnected = once(observer, 'change');
  peer.write(encodeFrame({ type: 'broadcast', method: 'client-status-changed', params: { clientId: 'owner', status: 'disconnected' } }));
  await disconnected;
  assert.equal(observer.snapshot().threads[0].state, 'disconnected');
  assert.equal(observer.snapshot().threads[0].runtimeStatus, null);
  const restored = once(observer, 'status');
  await observer.discover();
  await restored;
  ownerPresent = false;
  await observer.discover();
  assert.equal(observer.snapshot().threads[0].state, 'unobserved');
  assert.equal(observer.snapshot().threads[0].runtimeStatus, null);
  ownerPresent = true;
  const lostTransport = new Promise(resolve => {
    const handler = () => { if (!observer.connected) { observer.off('change', handler); resolve(); } };
    observer.on('change', handler);
  });
  peer.destroy();
  await lostTransport;
  assert.equal(observer.snapshot().threads[0].state, 'disconnected');
  const reconnected = once(observer, 'status');
  await reconnected;
  assert.equal(observer.snapshot().threads[0].state, 'active');
  assert.equal(observer.metrics.reconnects, 1);
  assert.ok(sent.filter(message => message.type === 'request').every(message => ['initialize', 'thread-owner-discovery'].includes(message.method)));
});
