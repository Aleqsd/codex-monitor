import { appendFileSync, statSync, renameSync, unlinkSync } from 'node:fs';
import path from 'node:path';

export function appendEvent(directory, event, maxBytes = 1024 * 1024) {
  const file = path.join(directory, 'events.jsonl');
  const previous = path.join(directory, 'events.1.jsonl');
  const line = JSON.stringify(event) + '\n';
  const bytes = Buffer.byteLength(line);
  if (bytes > Math.min(64 * 1024, maxBytes)) return;
  let size = 0;
  try { size = statSync(file).size; } catch (error) { if (error.code !== 'ENOENT') throw error; }
  if (size + bytes > maxBytes) {
    try { unlinkSync(previous); } catch (error) { if (error.code !== 'ENOENT') throw error; }
    renameSync(file, previous);
  }
  appendFileSync(file, line, 'utf8');
}
