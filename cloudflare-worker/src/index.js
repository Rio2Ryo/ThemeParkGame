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

      // ---- Co-op Room API ----
      if (path === '/api/coop/create' && request.method === 'POST') {
        return await handleCoopCreate(request, env, ctx);
      }
      if (path === '/api/coop/join' && request.method === 'POST') {
        return await handleCoopJoin(request, env, ctx);
      }
      if (path === '/api/coop/state' && request.method === 'POST') {
        return await handleCoopState(request, env, ctx);
      }
      if (path === '/api/coop/sync' && request.method === 'POST') {
        return await handleCoopSync(request, env, ctx);
      }
      if (path === '/api/coop/leave' && request.method === 'POST') {
        return await handleCoopLeave(request, env, ctx);
      }
      if (path === '/api/coop/list') {
        return await handleCoopList(env);
      }

      // ---- Health Check ----
      if (path === '/api/health') {
        return jsonResponse({
          status: 'ok',
          service: 'themeparkgame-api',
          version: '1.1',
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

// ================================================================
// Co-op Rooms
// ================================================================

function generateRoomCode() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let code = '';
  for (let i = 0; i < 6; i++) {
    code += chars[Math.floor(Math.random() * chars.length)];
  }
  return code;
}

async function handleCoopCreate(request, env, ctx) {
  const body = await request.json();
  const { playerId, playerName, parkData } = body;

  if (!playerId || !playerName) {
    return jsonResponse({ error: 'playerId and playerName are required' }, 400);
  }

  const roomCode = generateRoomCode();
  const room = {
    roomCode,
    hostId: String(playerId).substring(0, 32),
    hostName: String(playerName).substring(0, 20),
    players: [
      { id: String(playerId).substring(0, 32), name: String(playerName).substring(0, 20), joinedAt: Date.now() },
    ],
    maxPlayers: 4,
    createdAt: Date.now(),
    lastActivity: Date.now(),
    parkState: parkData || null,
    actions: [],
  };

  await kvPut(env, `coop:${roomCode}`, room, ctx);

  // Add to room index
  const index = (await kvGet(env, 'coop-rooms')) || {};
  index[roomCode] = { hostName: room.hostName, playerCount: 1, createdAt: room.createdAt };
  await kvPut(env, 'coop-rooms', index, ctx);

  return jsonResponse({ success: true, roomCode, room });
}

async function handleCoopJoin(request, env, ctx) {
  const body = await request.json();
  const { roomCode, playerId, playerName } = body;

  if (!roomCode || !playerId || !playerName) {
    return jsonResponse({ error: 'roomCode, playerId, and playerName are required' }, 400);
  }

  const room = await kvGet(env, `coop:${roomCode}`);
  if (!room) {
    return jsonResponse({ error: 'Room not found' }, 404);
  }

  if (room.players.length >= room.maxPlayers) {
    return jsonResponse({ error: 'Room is full' }, 400);
  }

  // Check if already in room
  const pid = String(playerId).substring(0, 32);
  if (!room.players.find((p) => p.id === pid)) {
    room.players.push({
      id: pid,
      name: String(playerName).substring(0, 20),
      joinedAt: Date.now(),
    });
  }

  room.lastActivity = Date.now();
  await kvPut(env, `coop:${roomCode}`, room, ctx);

  // Update index
  const index = (await kvGet(env, 'coop-rooms')) || {};
  if (index[roomCode]) {
    index[roomCode].playerCount = room.players.length;
  }
  await kvPut(env, 'coop-rooms', index, ctx);

  return jsonResponse({ success: true, room });
}

async function handleCoopState(request, env, ctx) {
  const body = await request.json();
  const { roomCode, since } = body;

  if (!roomCode) {
    return jsonResponse({ error: 'roomCode is required' }, 400);
  }

  const room = await kvGet(env, `coop:${roomCode}`);
  if (!room) {
    return jsonResponse({ error: 'Room not found' }, 404);
  }

  // Filter actions since timestamp
  const sinceTs = Number(since) || 0;
  const newActions = (room.actions || []).filter((a) => a.timestamp > sinceTs);

  return jsonResponse({
    success: true,
    players: room.players,
    parkState: room.parkState,
    actions: newActions,
    lastActivity: room.lastActivity,
  });
}

async function handleCoopSync(request, env, ctx) {
  const body = await request.json();
  const { roomCode, playerId, action, parkState } = body;

  if (!roomCode || !playerId) {
    return jsonResponse({ error: 'roomCode and playerId are required' }, 400);
  }

  const room = await kvGet(env, `coop:${roomCode}`);
  if (!room) {
    return jsonResponse({ error: 'Room not found' }, 404);
  }

  // Add action if provided
  if (action) {
    if (!room.actions) room.actions = [];
    room.actions.push({
      playerId: String(playerId).substring(0, 32),
      type: action.type || 'unknown',
      data: action.data || {},
      timestamp: Date.now(),
    });
    // Keep only last 200 actions
    if (room.actions.length > 200) {
      room.actions = room.actions.slice(-200);
    }
  }

  // Update park state if host
  if (parkState && String(playerId).substring(0, 32) === room.hostId) {
    room.parkState = parkState;
  }

  room.lastActivity = Date.now();
  await kvPut(env, `coop:${roomCode}`, room, ctx);

  return jsonResponse({ success: true, actionCount: (room.actions || []).length });
}

async function handleCoopLeave(request, env, ctx) {
  const body = await request.json();
  const { roomCode, playerId } = body;

  if (!roomCode || !playerId) {
    return jsonResponse({ error: 'roomCode and playerId are required' }, 400);
  }

  const room = await kvGet(env, `coop:${roomCode}`);
  if (!room) {
    return jsonResponse({ error: 'Room not found' }, 404);
  }

  const pid = String(playerId).substring(0, 32);
  room.players = room.players.filter((p) => p.id !== pid);
  room.lastActivity = Date.now();

  if (room.players.length === 0) {
    // Delete empty room
    await kvPut(env, `coop:${roomCode}`, null, ctx);
    const index = (await kvGet(env, 'coop-rooms')) || {};
    delete index[roomCode];
    await kvPut(env, 'coop-rooms', index, ctx);
    return jsonResponse({ success: true, roomDeleted: true });
  }

  // Transfer host if host left
  if (pid === room.hostId && room.players.length > 0) {
    room.hostId = room.players[0].id;
    room.hostName = room.players[0].name;
  }

  await kvPut(env, `coop:${roomCode}`, room, ctx);

  const index = (await kvGet(env, 'coop-rooms')) || {};
  if (index[roomCode]) {
    index[roomCode].playerCount = room.players.length;
    index[roomCode].hostName = room.hostName;
  }
  await kvPut(env, 'coop-rooms', index, ctx);

  return jsonResponse({ success: true, room });
}

async function handleCoopList(env) {
  const index = (await kvGet(env, 'coop-rooms')) || {};

  // Filter out stale rooms (older than 2 hours)
  const cutoff = Date.now() - 2 * 60 * 60 * 1000;
  const rooms = [];
  for (const [code, info] of Object.entries(index)) {
    if (info.createdAt > cutoff) {
      rooms.push({ roomCode: code, ...info });
    }
  }

  return jsonResponse({ success: true, rooms });
}
