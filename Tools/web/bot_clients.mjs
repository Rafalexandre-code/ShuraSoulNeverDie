// Load test for online co-op: N fake browser players join the host and fight.
// Usage: node Tools/web/bot_clients.mjs <count> <seconds> [host:port]
// Each bot walks to the nearest enemy, attacks, parries when the timing ring closes,
// and revives fallen teammates by standing over them holding the revive button.
const count = Number(process.argv[2] || 7);
const seconds = Number(process.argv[3] || 60);
const host = process.argv[4] || "localhost:7777";
const WEAPONS = ["katana", "montante", "gemeas", "naginata", "kusarigama", "manoplas"];

const stats = [];

// ── Path-finding on the arena grid the host sends with each room ──────────────
const STEPS = [[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
function makeNav(g) {
  const floor = (x, y) => x >= 0 && y >= 0 && x < g.w && y < g.h && g.c[y * g.w + x] === "1";
  const cellAt = (px, py) => [Math.floor(px - g.ox), Math.floor(py - g.oy)];
  const center = (x, y) => [g.ox + x + 0.5, g.oy + y + 0.5];
  const onFloor = (px, py) => { const [x, y] = cellAt(px, py); return floor(x, y); };
  const walk = (x, y, sx, sy) => floor(x + sx, y + sy) && (sx === 0 || sy === 0 || (floor(x + sx, y) && floor(x, y + sy)));
  const clear = (ax, ay, bx, by, r) => {
    const dx = bx - ax, dy = by - ay, len = Math.hypot(dx, dy);
    if (len < 1e-3) return true;
    const ux = dx / len, uy = dy / len, sx = -uy * r, sy = ux * r;
    for (let t = 0; t <= len; t += 0.3) {
      const px = ax + ux * t, py = ay + uy * t;
      if (!onFloor(px, py) || !onFloor(px + sx, py + sy) || !onFloor(px - sx, py - sy)) return false;
    }
    return onFloor(bx, by);
  };
  const fields = new Map();
  const field = (gx, gy) => {
    const key = gy * g.w + gx;
    if (fields.has(key)) return fields.get(key);
    const f = new Int32Array(g.w * g.h).fill(-1);
    f[key] = 0;
    const q = [[gx, gy]];
    for (let i = 0; i < q.length; i++) {
      const [x, y] = q[i];
      for (const [sx, sy] of STEPS) {
        if (!walk(x, y, sx, sy)) continue;
        const ni = (y + sy) * g.w + x + sx;
        if (f[ni] >= 0) continue;
        f[ni] = f[y * g.w + x] + 1;
        q.push([x + sx, y + sy]);
      }
    }
    if (fields.size > 48) fields.clear();
    fields.set(key, f);
    return f;
  };
  // Same rule as the host: straight when the way is open, else down the shortest path.
  return function steer(fx, fy, tx, ty) {
    const d = Math.hypot(tx - fx, ty - fy);
    if (d < 1e-3) return [0, 0];
    if (clear(fx, fy, tx, ty, 0.36)) return [(tx - fx) / d, (ty - fy) / d];
    const [gx, gy] = cellAt(tx, ty), [hx, hy] = cellAt(fx, fy);
    if (!floor(gx, gy) || !floor(hx, hy)) return [(tx - fx) / d, (ty - fy) / d];
    const f = field(gx, gy);
    let cx = hx, cy = hy, aim = center(hx, hy);
    for (let hop = 0; hop < 6; hop++) {
      const cur = f[cy * g.w + cx];
      if (cur <= 0) break;
      let best = cur, nx = cx, ny = cy;
      for (const [sx, sy] of STEPS) {
        if (!walk(cx, cy, sx, sy)) continue;
        const v = f[(cy + sy) * g.w + cx + sx];
        if (v >= 0 && v < best) { best = v; nx = cx + sx; ny = cy + sy; }
      }
      if (nx === cx && ny === cy) break;
      const c = center(nx, ny);
      if (hop > 0 && !clear(fx, fy, c[0], c[1], 0.36)) break;
      aim = c; cx = nx; cy = ny;
    }
    const ax = aim[0] - fx, ay = aim[1] - fy, m = Math.hypot(ax, ay);
    return m > 1e-3 ? [ax / m, ay / m] : [(tx - fx) / d, (ty - fy) / d];
  };
}

function bot(i) {
  return new Promise((resolve) => {
    const st = { name: "Bot" + (i + 1), snaps: 0, rooms: new Set(), deaths: 0, revives: 0, minHp: 1, joined: false, wasDead: false, bytes: 0, errors: 0 };
    stats.push(st);
    let ws = null, done = false;
    let myId = null, input = { t: "in", mx: 0, my: 0, ax: 1, ay: 0, p: 0, r: 0, atk: 0, par: 0, dod: 0, swp: 0 };
    let lastRing = new Map(), attackCd = 0, steer = null;
    const dodgedBlast = new Set();
    // The host may still be starting: keep trying until it answers.
    function connect() {
    ws = new WebSocket("ws://" + host + "/ws");
    ws.onopen = () => {
      const a = WEAPONS[i % WEAPONS.length], b = WEAPONS[(i + 2) % WEAPONS.length];
      ws.send(JSON.stringify({ t: "join", name: st.name, a, b }));
    };
    ws.onerror = () => { if (st.joined) st.errors++; };
    ws.onclose = () => { if (!done) setTimeout(connect, st.joined ? 2000 : 1000); };
    ws.onmessage = (ev) => {
      st.bytes += ev.data.length;
      let m;
      try { m = JSON.parse(ev.data); } catch { st.errors++; return; }
      if (m.t === "welcome") { myId = m.id; st.joined = true; return; }
      if (m.t === "room") { steer = m.grid ? makeNav(m.grid) : null; return; }
      if (m.t === "ping") { ws.send(JSON.stringify({ t: "pong", c: m.c })); return; }
      if (m.t !== "s" || myId == null) return;
      st.snaps++;
      if (m.room) st.rooms.add(m.room);
      const me = (m.P || []).find((p) => p[0] === myId);
      if (!me) return;
      const alive = me[21] === 1;
      if (!alive && !st.wasDead) st.deaths++;
      if (alive && st.wasDead) st.revives++;
      st.wasDead = !alive;
      if (alive) st.minHp = Math.min(st.minHp, me[17]);
      think(m, me, alive);
      ws.send(JSON.stringify(input));
    };
    }
    connect();
    function moveTo(me, tx, ty) {
      const d = Math.hypot(tx - me[3], ty - me[4]) || 1;
      const [mx, my] = steer ? steer(me[3], me[4], tx, ty) : [(tx - me[3]) / d, (ty - me[4]) / d];
      input.mx = mx; input.my = my;
    }
    function think(m, me, alive) {
      input.mx = 0; input.my = 0; input.p = 0; input.r = 0;
      if (!alive || m.m !== "play") return;
      // Step out of bomb and talisman circles; dodge through the blast if it is about to go off.
      for (const bl of m.B || []) {
        const dx = me[3] - bl[0], dy = me[4] - bl[1], d = Math.hypot(dx, dy);
        if (d > bl[2] + 0.7) continue;
        input.mx = d > 0.01 ? dx / d : 1; input.my = d > 0.01 ? dy / d : 0;
        if (bl[3] > 0.85 && !dodgedBlast.has(bl[0] + "," + bl[1])) { input.dod++; dodgedBlast.add(bl[0] + "," + bl[1]); }
        return;
      }
      // A fallen teammate nearby comes first.
      const fallen = (m.P || []).find((p) => p[0] !== myId && p[21] !== 1);
      if (fallen) {
        const dx = fallen[3] - me[3], dy = fallen[4] - me[4], d = Math.hypot(dx, dy);
        if (d < 8) {
          if (d > 1.0) moveTo(me, fallen[3], fallen[4]); else input.r = 1;
          return;
        }
      }
      let best = null, bd = 1e9;
      for (const e of m.E || []) {
        if (e[32]) continue;
        const d = Math.hypot(e[2] - me[3], e[3] - me[4]);
        if (d < bd) { bd = d; best = e; }
        // Answer closing rings of enemies near us (that is how a player reads the blow).
        if (e[29] === 2 && d < 4 && lastRing.get(e[0]) !== 2) {
          input.par++;
          input.ax = (e[2] - me[3]) / Math.max(0.01, d); input.ay = (e[3] - me[4]) / Math.max(0.01, d);
        }
        lastRing.set(e[0], e[29]);
      }
      if (!best) return;
      const dx = best[2] - me[3], dy = best[3] - me[4];
      input.ax = dx / Math.max(0.01, bd); input.ay = dy / Math.max(0.01, bd);
      if (bd > 1.4) moveTo(me, best[2], best[3]);
      else if (--attackCd <= 0) { input.atk++; attackCd = 6; }
    }
    setTimeout(() => { done = true; try { ws.close(); } catch {} resolve(); }, seconds * 1000);
  });
}

const all = [];
for (let i = 0; i < count; i++) {
  all.push(bot(i));
  await new Promise((r) => setTimeout(r, 150));
}
await Promise.all(all);
for (const s of stats) {
  console.log(`${s.name}: entrou=${s.joined} snapshots=${s.snaps} salas=${s.rooms.size} quedas=${s.deaths} revividas=${s.revives} vidaMin=${(s.minHp * 100).toFixed(0)}% KB=${(s.bytes / 1024).toFixed(0)} erros=${s.errors}`);
}
const avg = stats.reduce((a, s) => a + s.snaps, 0) / stats.length / seconds;
console.log(`média ${avg.toFixed(1)} snapshots/s por cliente; maior sala vista: ${Math.max(...stats.map((s) => s.rooms.size))}`);
process.exit(0);
