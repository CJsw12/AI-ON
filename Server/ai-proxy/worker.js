// AI-ON AI mode relay (Cloudflare Worker).
// The game sends { system, user } here; the relay adds the Gemini key, which lives only in Cloudflare as the
// secret GEMINI_API_KEY, and returns Gemini's OpenAI-compatible reply unchanged, so the game parses it exactly
// as it would a direct call. The model and the output length are fixed here so the relay can't be used for
// anything else, and each IP is rate limited.
//
// Gemini refuses calls from some regions ("User location is not supported"), and a Worker near Korea may run
// in Hong Kong. So the call to Google is always made from a Durable Object pinned to eastern North America.

import { DurableObject } from "cloudflare:workers";

const UPSTREAM = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
const MODEL = "gemini-3.1-flash-lite";  // pinned: the "-latest" alias moved to a model that took 15-40 s per reply (2026-11-05)
const CLIENT = "dugeun-campus";        // The game's X-AION-Client header; turns away casual requests.
const MAX_SYSTEM = 12000;              // The game's prompts are about 3,000 characters.
const MAX_USER = 400;                  // A typed answer or question is at most 80 characters plus a label.
const REGION = "enam";                 // Durable Object location hint: eastern North America.
const UPSTREAM_TIMEOUT_MS = 25000;     // Ends before the game gives up at 40 s.

const CORS = {
  "Access-Control-Allow-Origin": "*",  // Lets a WebGL build call it too.
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, X-AION-Client",
};
const JSON_TYPE = { "Content-Type": "application/json; charset=utf-8" };

function reply(status, message) {
  return new Response(JSON.stringify({ error: { message } }), { status, headers: { ...CORS, ...JSON_TYPE } });
}

// Makes the Gemini call from where the object lives (pinned by REGION).
export class GeminiRelay extends DurableObject {
  async fetch(request) {
    const key = (this.env.GEMINI_API_KEY || "").trim();   // A pasted secret can carry a trailing line break.
    if (!key) return reply(500, "Server key is not set");
    const { system, user } = await request.json();
    try {
      const upstream = await fetch(UPSTREAM, {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${key}` },
        body: JSON.stringify({
          model: MODEL,
          messages: [{ role: "system", content: system }, { role: "user", content: user }],
          max_completion_tokens: 1200,
        }),
        signal: AbortSignal.timeout(UPSTREAM_TIMEOUT_MS),
      });
      return new Response(await upstream.text(), { status: upstream.status, headers: JSON_TYPE });
    } catch (e) {
      const timedOut = e && (e.name === "TimeoutError" || e.name === "AbortError");
      return reply(timedOut ? 504 : 502, timedOut ? "AI server timed out" : "Could not reach the AI service");
    }
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

    const relay = env.RELAY.get(env.RELAY.idFromName("gemini-" + REGION), { locationHint: REGION });
    const upstream = await relay.fetch("https://relay/", { method: "POST", body: JSON.stringify({ system, user }) });
    return new Response(upstream.body, { status: upstream.status, headers: { ...CORS, ...JSON_TYPE } });
}
