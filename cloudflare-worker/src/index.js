// ============================================================
// ThemeParkGame - Cloudflare Worker API
// Leaderboard + Cloud Save
// Uses KV if available, falls back to Cache API
// ============================================================

const CORS_HEADERS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type',
  'Content-Type': 'application/json',
};

function jsonResponse(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: CORS_HEADERS });
}

// Cache-based KV fallback (persists within Cloudflare edge cache)
const CACHE_NAME = 'themeparkgame-store';

async function kvGet(env, key) {
  if (env.GAME_KV) {
    return await env.GAME_KV.get(key, { type: 'json' });
  }
  const cache = await caches.open(CACHE_NAME);
  const cacheKey = new Request(`https://store.local/${encodeURIComponent(key)}`);
  const cached = await cache.match(cacheKey);
  if (!cached) return null;
  try { return await cached.json(); } catch { return null; }
}

async function kvPut(env, key, value, ctx) {
  const json = JSON.stringify(value);
  if (env.GAME_KV) {
    await env.GAME_KV.put(key, json);
    return;
  }
  const cache = await caches.open(CACHE_NAME);
  const cacheKey = new Request(`https://store.local/${encodeURIComponent(key)}`);
  const resp = new Response(json, {
    headers: { 'Content-Type': 'application/json', 'Cache-Control': 'max-age=2592000' },
  });
  if (ctx) ctx.waitUntil(cache.put(cacheKey, resp));
  else await cache.put(cacheKey, resp);
}

export default {
  async fetch(request, env, ctx) {
    if (request.method === 'OPTIONS') {
      return new Response(null, { status: 204, headers: CORS_HEADERS });
    }

    const url = new URL(request.url);
    const path = url.pathname;

    try {
      // ---- Leaderboard API ----
      if (path === '/api/leaderboard/submit' && request.method === 'POST') {
        return await handleLeaderboardSubmit(request, env, ctx);
      }
      if (path === '/api/leaderboard/top') {
        return await handleLeaderboardTop(url, env);
      }

      // ---- Cloud Save API ----
      if (path === '/api/cloudsave/upload' && request.method === 'POST') {
        return await handleCloudSaveUpload(request, env, ctx);
      }
      if (path === '/api/cloudsave/download' && request.method === 'POST') {
        return await handleCloudSaveDownload(request, env);
      }
      if (path === '/api/cloudsave/list' && request.method === 'POST') {
        return await handleCloudSaveList(request, env);
      }

      // ---- Health Check ----
      if (path === '/api/health') {
        return jsonResponse({
          status: 'ok',
          service: 'themeparkgame-api',
          version: '1.0',
          storage: env.GAME_KV ? 'kv' : 'cache',
        });
      }

      return jsonResponse({ error: 'Not Found' }, 404);
    } catch (err) {
      return jsonResponse({ error: err.message }, 500);
    }
  },
};

// ================================================================
// Leaderboard
// ================================================================

async function handleLeaderboardSubmit(request, env, ctx) {
  const body = await request.json();
  const { playerName, score, difficulty, parkRating, totalVisitors, playTimeMinutes } = body;

  if (!playerName || score === undefined) {
    return jsonResponse({ error: 'playerName and score are required' }, 400);
  }

  const entry = {
    playerName: String(playerName).substring(0, 20),
    score: Number(score),
    difficulty: difficulty || 'Normal',
    parkRating: Number(parkRating) || 0,
    totalVisitors: Number(totalVisitors) || 0,
    playTimeMinutes: Number(playTimeMinutes) || 0,
    timestamp: Date.now(),
  };

  const board = (await kvGet(env, 'leaderboard')) || [];
  board.push(entry);
  board.sort((a, b) => b.score - a.score);
  const trimmed = board.slice(0, 100);

  await kvPut(env, 'leaderboard', trimmed, ctx);

  const rank = trimmed.findIndex(
    (e) => e.playerName === entry.playerName && e.timestamp === entry.timestamp
  );

  return jsonResponse({ success: true, rank: rank + 1, totalEntries: trimmed.length });
}

async function handleLeaderboardTop(url, env) {
  const limit = Math.min(Number(url.searchParams.get('limit')) || 20, 100);
  const board = (await kvGet(env, 'leaderboard')) || [];
  return jsonResponse({ entries: board.slice(0, limit), total: board.length });
}

// ================================================================
// Cloud Save
// ================================================================

async function handleCloudSaveUpload(request, env, ctx) {
  const body = await request.json();
  const { playerId, slot, saveData } = body;

  if (!playerId || slot === undefined || !saveData) {
    return jsonResponse({ error: 'playerId, slot, and saveData are required' }, 400);
  }

  const pid = String(playerId).substring(0, 32);
  const s = Number(slot);
  const key = `save:${pid}:${s}`;
  const savedAt = Date.now();

  await kvPut(env, key, saveData, ctx);

  // Update index
  const indexKey = `save-index:${pid}`;
  const index = (await kvGet(env, indexKey)) || {};
  index[String(s)] = {
    slot: s,
    savedAt,
    version: saveData.SaveVersion || '?',
    year: saveData.CurrentYear || 0,
    month: saveData.CurrentMonth || 0,
    balance: saveData.CurrentBalance || 0,
  };
  await kvPut(env, indexKey, index, ctx);

  return jsonResponse({ success: true, savedAt });
}

async function handleCloudSaveDownload(request, env) {
  const body = await request.json();
  const { playerId, slot } = body;

  if (!playerId || slot === undefined) {
    return jsonResponse({ error: 'playerId and slot are required' }, 400);
  }

  const key = `save:${String(playerId).substring(0, 32)}:${Number(slot)}`;
  const data = await kvGet(env, key);

  if (!data) {
    return jsonResponse({ error: 'Save not found' }, 404);
  }

  return jsonResponse({ success: true, saveData: data });
}

async function handleCloudSaveList(request, env) {
  const body = await request.json();
  const { playerId } = body;

  if (!playerId) {
    return jsonResponse({ error: 'playerId is required' }, 400);
  }

  const indexKey = `save-index:${String(playerId).substring(0, 32)}`;
  const index = (await kvGet(env, indexKey)) || {};

  return jsonResponse({ success: true, slots: index });
}
