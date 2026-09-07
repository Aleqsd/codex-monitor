import { createServer } from 'node:http';
import { existsSync, writeFileSync } from 'node:fs';
import path from 'node:path';
const value = flag => process.argv[process.argv.indexOf(flag) + 1];
const output = value('--output');
const server = createServer((req, res) => {
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify({ schemaVersion: 1, connected: false }));
});
server.listen(Number(value('--port')), '127.0.0.1', () => writeFileSync(path.join(output, 'started'), String(process.pid)));
const timer = setInterval(() => {
  if (existsSync(path.join(output, 'stop'))) { clearInterval(timer); server.close(); }
}, 50);
