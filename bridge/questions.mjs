import { createHash } from 'node:crypto';

// Retain structure needed to apply the stream's array patches, never message bodies,
// questions, answer text, reasoning or tool output. Only explicit async questions count.
const scalar = value => typeof value === 'string' ? value.slice(0, 512) : null;
const hasText = value => typeof value === 'string' && value.trim().length > 0;
const openReply = '<send_user_message_question_reply>';
const closeReply = '</send_user_message_question_reply>';
function replyIds(text) {
  if (typeof text !== 'string') return [];
  const value = text.trim();
  if (!value.startsWith(openReply) || !value.endsWith(closeReply)) return [];
  try {
    const parsed = JSON.parse(value.slice(openReply.length, -closeReply.length));
    const rows = Array.isArray(parsed) ? parsed : [parsed];
    if (!rows.every(row => row && typeof row.questionItemId === 'string' && typeof row.question === 'string' && typeof row.answer === 'string')) return [];
    return rows.slice(0, 100).map(row => row.questionItemId.slice(0, 2048));
  } catch { return []; }
}
const content = { type: scalar, text: replyIds };
const item = { id: scalar, type: scalar, delivery: scalar, status: scalar,
  text: hasText, questions: [{ title: hasText }], content: [content], input: [content] };
const turn = { turnId: scalar, status: scalar, items: [item] };
const fields = {
  requests: [{ id: value => typeof value === 'string' || typeof value === 'number' ? String(value).slice(0, 512) : null,
    method: scalar, params: { isBlocking: value => value === true, questions: [{ id: scalar }] } }],
  turns: [turn],
  turnHistory: { kind: scalar, history: { entitiesByKey: { '*': turn },
    islands: [{ entries: [{ value: scalar }], newerBoundary: { status: scalar } }] } },
};
const forbidden = new Set(['__proto__', 'constructor', 'prototype']);
function schemaAt(path) {
  let schema = fields;
  for (const key of path) {
    if (!schema || typeof schema === 'function' || forbidden.has(String(key))) return null;
    schema = Array.isArray(schema) ? (key === 'length' ? value => {
      if (!Number.isInteger(value) || value < 0 || value > 200000) throw new Error('Invalid question array length');
      return value;
    } : Number.isInteger(key) && key >= 0 ? schema[0] : null) : Object.hasOwn(schema, key) ? schema[key] : schema['*'];
  }
  return schema;
}
function project(value, schema) {
  if (typeof schema === 'function') return schema(value);
  if (Array.isArray(schema)) return Array.isArray(value) ? value.map(entry => project(entry, schema[0])) : [];
  if (!value || typeof value !== 'object') return {};
  return Object.fromEntries(Object.entries(value).filter(([key]) => !forbidden.has(key) && (Object.hasOwn(schema, key) || Object.hasOwn(schema, '*')))
    .map(([key, entry]) => [key, project(entry, schema[key] ?? schema['*'])]));
}

export function projectQuestions(state) { return project(state, fields); }
export function projectQuestionPatch(patch) {
  const schema = schemaAt(patch.path);
  return schema ? { ...patch, value: patch.op === 'remove' ? undefined : project(patch.value, schema) } : null;
}

export function pendingQuestionIds(state) {
  const hash = id => createHash('sha256').update(id).digest('hex').slice(0, 32);
  const requests = (state.requests ?? []).filter(request => request.id && request.method === 'item/tool/requestUserInput' && !request.params?.isBlocking)
    .slice(0, 100).map(request => hash(`request:${request.id}`));
  let latest;
  if (state.turnHistory?.kind === 'canonical') {
    const history = state.turnHistory.history;
    const island = history?.islands?.at(-1);
    // Do not mistake a paginated historical fragment for the current turn.
    if (island?.newerBoundary?.status !== 'exhausted') return requests;
    const key = island.entries?.at(-1)?.value;
    latest = history.entitiesByKey?.[key];
  } else latest = state.turns?.at(-1);
  if (!latest?.turnId || !Array.isArray(latest.items)) return requests;
  const answered = new Set();
  const questions = new Set();
  for (const entry of latest.items) {
    if (entry.type === 'agentMessage' && entry.delivery === 'async' && entry.id) {
      if (entry.questions?.length) entry.questions.forEach((question, index) => {
        if (question.title) questions.add(JSON.stringify(['request_user_input_async', entry.id, index]));
      });
      else if (entry.text) questions.add(entry.id);
    }
    const parts = entry.type === 'userMessage' ? entry.content
      : entry.type === 'steeringUserMessage' && entry.status === 'accepted' ? entry.input : null;
    if (parts?.length === 1 && parts[0]?.type === 'text') for (const id of parts[0].text ?? []) answered.add(id);
  }
  return [...requests, ...[...questions].filter(id => !answered.has(id))
    .map(id => hash(JSON.stringify([latest.turnId, id])))].slice(0, 100);
}
