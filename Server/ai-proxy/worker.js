// AI-ON AI mode relay (Cloudflare Worker).
// The game sends { system, user } here; the relay adds the API key, which lives only in Cloudflare as a secret,
// and returns an OpenAI-format chat reply, so the game parses it exactly as it would a direct call. The models
// and the output length are fixed here so the relay can't be used for anything else, and each IP is rate limited.
//
// Model order: GPT-6.1 Terra (secret OPENAI_API_KEY) first; if it fails, times out, is paused or has used up
// today's call budget, Gemini (secret GEMINI_API_KEY, free) answers instead, so the game keeps working when the
// OpenAI credit runs out. The OpenAI account is borrowed and we can't set a spending limit on it, so the daily
// budget below is the cost cap.
//
// Gemini refuses calls from some regions ("User location is not supported"), and a Worker near Korea may run
// in Hong Kong, which OpenAI doesn't serve either. So both calls are made from a Durable Object pinned to
// eastern North America.

import { DurableObject } from "cloudflare:workers";
import voices from "./voices.json" with { type: "json" };

// Canonical Python SAY order and patterns (checked offline).
export const SAY = [
  [String.raw`i\+\+`, "아이 플러스 플러스"],
  [String.raw`\bGit\b`, "깃"],
  [String.raw`\bCount\b`, "카운트"],
  [String.raw`\bOT\b`, "오티"],
  [String.raw`(?<![A-Za-z])LIFO(?![A-Za-z])`, "라이포"],
  [String.raw`(?<![A-Za-z])FIFO(?![A-Za-z])`, "파이포"],
  [String.raw`(?<![A-Za-z])target(?![A-Za-z])`, "타깃"],
  [String.raw`\bk번째`, "케이번째"],
  [String.raw`\b10진수`, "십진수"],
  [String.raw`\b2진수`, "이진수"],
  [String.raw`(?<!\d)1이`, "일이"],
];
// Python word boundaries include Korean; JavaScript's native \b does not.
const word = String.raw`[\p{L}\p{N}_]`;
const boundary = `(?:(?<=${word})(?!${word})|(?<!${word})(?=${word}))`;
const speechRules = SAY.map(([p, t]) => [new RegExp(p.replaceAll(String.raw`\b`, boundary).replaceAll(String.raw`\d`, String.raw`\p{Nd}`), "gu"), t]);
const xml = t => t.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&apos;");
export function buildSsml(text, config) {
  for (const [p, t] of speechRules) text = text.replace(p, t);
  text = text.replaceAll("…", "... ").trim().replace(/^(\.\.\.\s*)+/, "").replace(/\s+/gu, " ").trim();
  let body = xml(text);
  if (config.pause > 0) body = body.replace(/([,.?!…])(?=\s)/gu, `$1<break time='${config.pause}ms'/>`);
  return `<speak version='1.0' xml:lang='ko-KR'><voice name='${xml(config.voice)}'><prosody rate='${config.rate}%' pitch='${config.pitch}%' volume='${config.volume >= 0 ? "+" : ""}${config.volume}%'>${body}</prosody></voice></speak>`;
}
function ttsInput(body) {
  if (!body || typeof body.voice !== "string" || body.voice === "ShimHanmoe" || !Object.hasOwn(voices, body.voice) || !voices[body.voice]?.voice || typeof body.text !== "string") return null;
  const text = body.text.replace(/\s+/gu, " ").trim();
  return text.length > 0 && [...text].length <= 300 ? { voice: body.voice, text } : null;
}

const OPENAI_UPSTREAM = "https://api.openai.com/v1/chat/completions";
const OPENAI_MODEL = "gpt-6.1-terra";
const GEMINI_UPSTREAM = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
const GEMINI_MODEL = "gemini-3.1-flash-lite";  // pinned: the "-latest" alias moved to a model that took 15-40 s per reply (2026-11-05)
const CLIENT = "dugeun-campus";        // The game's X-AION-Client header; turns away casual requests.
const MAX_SYSTEM = 12000;              // The game's prompts are about 3,000 characters.
const MAX_USER = 400;                  // A typed answer or question is at most 80 characters plus a label.
const MAX_OUTPUT_TOKENS = 1200;        // A ceiling, not a reply-length limit: labels plus a reply of ~220 characters.
const REGION = "enam";                 // Durable Object location hint: eastern North America.
// Both timeouts together (12 + 22 s) end before the game gives up at 40 s.
const TERRA_TIMEOUT_MS = 12000;
const GEMINI_TIMEOUT_MS = 22000;
// Terra calls per UTC day; after that Gemini answers. At about $0.003 a call (worst case about $0.01),
// 500 calls cost at most about $5 a day. The TERRA_DAILY_LIMIT var overrides it; 0 turns Terra off.
const TERRA_DAILY_LIMIT = 500;
// After a key, permission or quota error (401/403/429), Terra is skipped this long so a call doesn't
// first wait for a refusal.
const TERRA_PAUSE_MS = 10 * 60 * 1000;

const CORS = {
  "Access-Control-Allow-Origin": "*",  // Lets a WebGL build call it too.
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, X-AION-Client",
  "Access-Control-Expose-Headers": "X-AION-Model, X-AION-Volume",
};
const JSON_TYPE = { "Content-Type": "application/json; charset=utf-8" };

function reply(status, message) {
  return new Response(JSON.stringify({ error: { message } }), { status, headers: { ...CORS, ...JSON_TYPE } });
}

// One upstream call. ok = a 200 reply whose first choice has text, which is all the game reads.
async function callModel(url, key, body, timeoutMs) {
  try {
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${key}` },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(timeoutMs),
    });
    const text = await res.text();
    let ok = false;
    if (res.status === 200) {
      try {
        const content = JSON.parse(text)?.choices?.[0]?.message?.content;
        ok = typeof content === "string" && content.trim() !== "";
      } catch { /* not JSON: a failure */ }
    }
    return { ok, status: res.status, text };
  } catch (e) {
    const timedOut = !!e && (e.name === "TimeoutError" || e.name === "AbortError");
    return { ok: false, status: timedOut ? 504 : 502, text: null, timedOut };
  }
}

// Makes the model calls from where the object lives (pinned by REGION). Every request goes to this one object,
// so it also keeps the daily Terra count.
export class GeminiRelay extends DurableObject {
  async fetch(request) {
    if (new URL(request.url).pathname === "/tts") return this.tts(request);
    const { system, user } = await request.json();
    const messages = [{ role: "system", content: system }, { role: "user", content: user }];

    const openaiKey = (this.env.OPENAI_API_KEY || "").trim();   // A pasted secret can carry a trailing line break.
    if (openaiKey && await this.takeTerraSlot()) {
      const terra = await callModel(OPENAI_UPSTREAM, openaiKey,
        { model: OPENAI_MODEL, messages, max_completion_tokens: MAX_OUTPUT_TOKENS, reasoning_effort: "low" },
        TERRA_TIMEOUT_MS);
      if (terra.ok) return new Response(terra.text, { status: 200, headers: { ...JSON_TYPE, "X-AION-Model": OPENAI_MODEL } });
      if (terra.status === 401 || terra.status === 403 || terra.status === 429)
        await this.ctx.storage.put("terraPausedUntil", Date.now() + TERRA_PAUSE_MS);
      console.log(`Terra failed (${terra.status}${terra.timedOut ? ", timeout" : ""}): ${(terra.text || "").slice(0, 200)}`);
    }

    const geminiKey = (this.env.GEMINI_API_KEY || "").trim();
    if (!geminiKey) return reply(500, "Server key is not set");
    const gemini = await callModel(GEMINI_UPSTREAM, geminiKey,
      { model: GEMINI_MODEL, messages, max_completion_tokens: MAX_OUTPUT_TOKENS }, GEMINI_TIMEOUT_MS);
    if (gemini.text === null) return reply(gemini.status, gemini.timedOut ? "AI server timed out" : "Could not reach the AI service");
    return new Response(gemini.text, { status: gemini.status, headers: { ...JSON_TYPE, "X-AION-Model": GEMINI_MODEL } });
  }

  async tts(request) {
    let input;
    try { input = ttsInput(await request.json()); } catch { return reply(400, "Invalid JSON"); }
    if (!input) return reply(400, "Invalid speech request");
    const key = (this.env.AZURE_SPEECH_KEY || "").trim();
    const region = (this.env.AZURE_SPEECH_REGION || "").trim();
    if (!key || !/^[a-z0-9-]+$/.test(region)) return reply(503, "Speech service unavailable");
    if (!await this.takeTtsSlot()) return reply(429, "Speech daily limit reached");
    const config = voices[input.voice];
    try {
      const res = await fetch(`https://${region}.tts.speech.microsoft.com/cognitiveservices/v1`, {
        method: "POST", headers: { "Ocp-Apim-Subscription-Key": key, "Content-Type": "application/ssml+xml",
          "X-Microsoft-OutputFormat": "riff-24khz-16bit-mono-pcm", "User-Agent": "aion-ai-proxy" },
        body: buildSsml(input.text, config), signal: AbortSignal.timeout(6000),
      });
      if (!res.ok) return reply(res.status, "Speech service failed");
      const bytes = await res.arrayBuffer();
      return new Response(bytes, { headers: { ...CORS, "Content-Type": "audio/wav", "X-AION-Volume": String(config.volume) } });
    } catch { return reply(504, "Speech service unavailable"); }
  }
  async takeTtsSlot() {
    const n = Number(this.env.TTS_DAILY_LIMIT);
    const limit = this.env.TTS_DAILY_LIMIT !== undefined && Number.isFinite(n) ? n : 300;
    if (limit <= 0) return false;
    // Reserve atomically, including concurrent calls, before contacting Azure.
    return this.ctx.storage.transaction(async storage => {
      const day = "tts:" + new Date().toISOString().slice(0, 10);
      const used = (await storage.get(day)) || 0;
      if (used >= limit) return false;
      await storage.put(day, used + 1);
      return true;
    });
  }

  // Counts a Terra call against today's budget; false while Terra is paused or once the budget is used up.
  async takeTerraSlot() {
    const configured = Number(this.env.TERRA_DAILY_LIMIT);
    const limit = this.env.TERRA_DAILY_LIMIT !== undefined && Number.isFinite(configured) ? configured : TERRA_DAILY_LIMIT;
    if (limit <= 0) return false;
    if (Date.now() < ((await this.ctx.storage.get("terraPausedUntil")) || 0)) return false;
    const day = "terra:" + new Date().toISOString().slice(0, 10);
    const used = (await this.ctx.storage.get(day)) || 0;
    if (used >= limit) return false;
    await this.ctx.storage.put(day, used + 1);
    return true;
  }
}

export default {
  // Any unexpected failure still answers with JSON and CORS, never Cloudflare's HTML error page.
  async fetch(request, env) {
    try { return await handle(request, env); }
    catch { return reply(500, "Server error"); }
  },
};

async function handle(request, env) {
    if (request.method === "OPTIONS") return new Response(null, { headers: CORS });
    if (request.method !== "POST") return reply(405, "POST only");
    if (request.headers.get("X-AION-Client") !== CLIENT) return reply(403, "Unknown client");

    if (env.LIMITER) {
      const ip = request.headers.get("CF-Connecting-IP") || "unknown";
      const { success } = await env.LIMITER.limit({ key: ip });
      if (!success) return reply(429, "Too many requests. Try again in a minute.");
    }

    let body;
    try { body = await request.json(); } catch { return reply(400, "Invalid JSON"); }
    if (!body || typeof body !== "object" || Array.isArray(body)) return reply(400, "Invalid request");
    if (new URL(request.url).pathname === "/tts") {
      const input = ttsInput(body);
      if (!input) return reply(400, "Invalid speech request");
      const relay = env.RELAY.get(env.RELAY.idFromName("gemini-" + REGION), { locationHint: REGION });
      const upstream = await relay.fetch("https://relay/tts", { method: "POST", body: JSON.stringify(input) });
      return new Response(upstream.body, { status: upstream.status, headers: { ...CORS,
        "Content-Type": upstream.headers.get("Content-Type") || "application/json",
        "X-AION-Volume": upstream.headers.get("X-AION-Volume") || "0" } });
    }
    const system = typeof body.system === "string" ? body.system : "";
    const user = typeof body.user === "string" ? body.user : "";
    if (!system || !user || system.length > MAX_SYSTEM || user.length > MAX_USER) return reply(400, "Invalid request");

    // Same object name as before, so the deployed Durable Object (and its v1 migration) is reused.
    const relay = env.RELAY.get(env.RELAY.idFromName("gemini-" + REGION), { locationHint: REGION });
    const upstream = await relay.fetch("https://relay/", { method: "POST", body: JSON.stringify({ system, user }) });
    const headers = { ...CORS, ...JSON_TYPE };
    const model = upstream.headers.get("X-AION-Model");
    if (model) headers["X-AION-Model"] = model;
    return new Response(upstream.body, { status: upstream.status, headers });
}
