import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { appendEvent } from './files.mjs';

test('relay journals rotate within two bounded files and retain recent complete events', t => {
  const directory = mkdtempSync(path.join(os.tmpdir(), 'codex-monitor-journal-'));
  t.after(() => rmSync(directory, { recursive: true }));
  for (let sequence = 0; sequence < 100; sequence++) appendEvent(directory, { sequence, state: 'idle' }, 128);
  const files = readdirSync(directory); assert.equal(files.length, 2);
  for (const file of files) {
    const bytes = readFileSync(path.join(directory, file)); assert.ok(bytes.length <= 128);
    for (const line of bytes.toString().trim().split('\n')) assert.equal(JSON.parse(line).state, 'idle');
  }
  assert.equal(JSON.parse(readFileSync(path.join(directory, 'events.jsonl'), 'utf8').trim().split('\n').at(-1)).sequence, 99);
  appendEvent(directory, { content: 'x'.repeat(1000) }, 128);
  assert.ok(readFileSync(path.join(directory, 'events.jsonl')).length <= 128);
});
