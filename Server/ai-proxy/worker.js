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
  "Access-Control-Expose-Headers": "X-AION-Model",
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
