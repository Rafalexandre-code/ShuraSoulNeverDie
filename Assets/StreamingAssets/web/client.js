// Shura — browser client for online co-op.
// The host's game runs the simulation; this page draws the state it streams (~30/s),
// plays the sounds, and sends this player's keys/mouse/gamepad back.
"use strict";

const WEAPONS = [
  { id: "katana", name: "Katana", art: "weapon_Katana" },
  { id: "montante", name: "Montante", art: "weapon_BigSword" },
  { id: "gemeas", name: "Lâminas Gêmeas", art: "weapon_Sai" },
  { id: "naginata", name: "Naginata", art: "weapon_Lance2" },
  { id: "kusarigama", name: "Kusarigama", art: "weapon_AxeTool" },
  { id: "manoplas", name: "Manoplas de Ferro", art: "weapon_Hammer" },
];
const GRIP = { weapon_Lance: 0.3, weapon_Lance2: 0.3, weapon_Stick: 0.3, weapon_Club: 0.25, weapon_Hammer: 0.25, weapon_AxeTool: 0.25 };
const DANGER = "#ff5a3c", ACCENT = "#6fc3df", GOLD = "#f2c14e";
const VIEW_UNITS = 14;          // world units visible vertically
const INTERP_DELAY = 45;        // ms behind the newest snapshot (smooth motion)

// ── State ─────────────────────────────────────────────────────────────────────────
const S = {
  ws: null, myId: null, joined: false, room: null, arena: null, arenaVersion: -1,
  snaps: [], last: null, picks: ["katana", "naginata"],
  particles: [], texts: [], stains: [], rings: [], shake: 0, flash: 0, flashColor: "#fff",
  cam: { x: 0, y: 0 }, input: { atk: 0, par: 0, dod: 0, swp: 0 }, lastSent: 0, lastInput: "",
};
const canvas = document.getElementById("game");
const ctx = canvas.getContext("2d");
let W = 0, H = 0, PPU = 32;

function resize() {
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  W = canvas.width = Math.floor(innerWidth * dpr);
  H = canvas.height = Math.floor(innerHeight * dpr);
  PPU = H / VIEW_UNITS;
  ctx.imageSmoothingEnabled = false;
}
addEventListener("resize", resize);
resize();

// ── Images ────────────────────────────────────────────────────────────────────────
const images = {};
function img(path) {
  let e = images[path];
  if (!e) {
    e = images[path] = { img: new Image(), ok: false, bottom: 0 };
    e.img.onload = () => { e.ok = true; e.bottom = emptyBottom(e.img); };
    e.img.src = path;
  }
  return e.ok ? e : null;
}
function emptyBottom(im) {
  // Feet anchor: how many transparent rows sit under the first 16x16 frame.
  try {
    const c = document.createElement("canvas"); c.width = 16; c.height = 16;
    const g = c.getContext("2d"); g.drawImage(im, 0, 0, 16, 16, 0, 0, 16, 16);
    const d = g.getImageData(0, 0, 16, 16).data;
    for (let y = 15; y >= 0; y--) for (let x = 0; x < 16; x++) if (d[(y * 16 + x) * 4 + 3] > 8) return 15 - y;
  } catch (e) { /* tainted canvas: assume none */ }
  return 0;
}
const tint = document.createElement("canvas"); tint.width = 64; tint.height = 64;
const tintCtx = tint.getContext("2d");

// ── Sound ─────────────────────────────────────────────────────────────────────────
const AudioCtx = window.AudioContext || window.webkitAudioContext;
let audio = null, manifest = {}, buffers = {}, music = {}, musicGain = null;
async function startAudio() {
  if (audio) return;
  try { audio = new AudioCtx(); } catch (e) { return; }
  try { manifest = await (await fetch("sfx/manifest.json")).json(); } catch (e) { manifest = {}; }
  musicGain = audio.createGain(); musicGain.gain.value = 0.55; musicGain.connect(audio.destination);
  for (const name of ["calm", "fight", "intense"]) loadMusic(name);
}
async function loadBuffer(url) {
  if (buffers[url] !== undefined) return buffers[url];
  buffers[url] = null;
  try {
    const data = await (await fetch(url)).arrayBuffer();
    buffers[url] = await audio.decodeAudioData(data);
  } catch (e) { buffers[url] = null; }
  return buffers[url];
}
async function loadMusic(name) {
  const buf = await loadBuffer("music/" + name + ".ogg");
  if (!buf) return;
  const src = audio.createBufferSource(); src.buffer = buf; src.loop = true;
  const g = audio.createGain(); g.gain.value = 0; src.connect(g); g.connect(musicGain); src.start();
  music[name] = g;
}
function sfx(cat, volume, x, y, pitch) {
  if (!audio || !manifest[cat]) return;
  const files = manifest[cat];
  const url = "sfx/" + cat + "/" + files[(Math.random() * files.length) | 0];
  const me = myPlayer();
  let v = volume;
  if (me && x !== undefined) v *= Math.max(0.12, 1 - Math.hypot(x - me.x, y - me.y) / 18);
  loadBuffer(url).then((buf) => {
    if (!buf) return;
    const s = audio.createBufferSource(); s.buffer = buf;
    s.playbackRate.value = (pitch || 1) * (0.94 + Math.random() * 0.12);
    const g = audio.createGain(); g.gain.value = v;
    s.connect(g); g.connect(audio.destination); s.start();
  });
}
function updateMusic(snap) {
  if (!audio) return;
  const combat = snap.cb === 1 && snap.m === "play";
  const intense = combat && (snap.fl || 0) >= 2;
  const target = { calm: combat ? 0 : 0.8, fight: combat && !intense ? 0.75 : 0, intense: intense ? 0.8 : 0 };
  for (const k in music) {
    const g = music[k].gain;
    g.value += (target[k] - g.value) * 0.05;
  }
}

// ── Lobby ─────────────────────────────────────────────────────────────────────────
const nameInput = document.getElementById("name");
try { nameInput.value = localStorage.getItem("shura.name") || ""; } catch (e) { /* private mode */ }
const weaponsBox = document.getElementById("weapons");
function renderWeapons() {
  weaponsBox.innerHTML = "";
  for (const w of WEAPONS) {
    const d = document.createElement("div");
    const pick = S.picks.indexOf(w.id);
    d.className = "wpn" + (pick >= 0 ? " on" : "");
    d.innerHTML = '<img src="art/' + w.art + '.png" alt="">' + w.name + (pick >= 0 ? '<span class="n">' + (pick + 1) + "</span>" : "");
    d.onclick = () => {
      const i = S.picks.indexOf(w.id);
      if (i >= 0) S.picks.splice(i, 1);
      else if (S.picks.length < 2) S.picks.push(w.id);
      else S.picks[1] = w.id;
      renderWeapons();
    };
    weaponsBox.appendChild(d);
  }
  document.getElementById("join").disabled = !(S.ws && S.ws.readyState === 1 && S.picks.length === 2);
}
renderWeapons();
document.getElementById("join").onclick = () => {
  const name = nameInput.value.trim() || "Ronin";
  try { localStorage.setItem("shura.name", name); } catch (e) { /* ignore */ }
  send({ t: "join", name, a: S.picks[0], b: S.picks[1] });
  startAudio();
  setStatus("Entrando…");
};
function setStatus(t) { document.getElementById("status").textContent = t; }

// ── Network ───────────────────────────────────────────────────────────────────────
function connect() {
  const ws = new WebSocket("ws://" + location.host + "/ws");
  S.ws = ws;
  ws.onopen = () => { setStatus("Conectado ao anfitrião. Escolha e entre!"); renderWeapons(); };
  ws.onclose = () => {
    setStatus("Sem conexão com o anfitrião. Tentando de novo…");
    S.joined = false;
    document.getElementById("lobby").hidden = false;
    document.getElementById("hud").hidden = true;
    setTimeout(connect, 2000);
  };
  ws.onmessage = (ev) => {
    let m;
    try { m = JSON.parse(ev.data); } catch (e) { return; }
    switch (m.t) {
      case "s": onSnapshot(m); break;
      case "room": onRoom(m); break;
      case "welcome":
        S.myId = m.id; S.joined = true;
        document.getElementById("lobby").hidden = true;
        document.getElementById("hud").hidden = false;
        break;
      case "full": setStatus("A sala está cheia (8 duelistas)."); break;
      case "ping": send({ t: "pong", c: m.c }); break;
    }
  };
}
function send(o) { if (S.ws && S.ws.readyState === 1) S.ws.send(JSON.stringify(o)); }
connect();

function onRoom(m) {
  S.room = m;
  const im = new Image();
  im.onload = () => { S.arena = im; };
  im.src = "arena.png?v=" + m.v;
  S.stains.length = 0;
}

function onSnapshot(m) {
  m.time = performance.now();
  S.snaps.push(m);
  if (S.snaps.length > 6) S.snaps.shift();
  S.last = m;
  for (const e of m.F || []) handleEvent(e);
  updateMusic(m);
  if (!S.joined && m.P) {
    const names = m.P.map((p) => p[1]).join(", ");
    setStatus(S.ws && S.ws.readyState === 1 ? "Na sala: " + names : "");
  }
}

// ── Interpolation ─────────────────────────────────────────────────────────────────
function lerp(a, b, t) { return a + (b - a) * t; }
function lerpAngle(a, b, t) { let d = ((b - a + 540) % 360) - 180; return a + d * t; }
function frameState() {
  const snaps = S.snaps;
  if (snaps.length === 0) return null;
  const rt = performance.now() - INTERP_DELAY;
  let a = snaps[0], b = snaps[snaps.length - 1];
  for (let i = snaps.length - 1; i > 0; i--) {
    if (snaps[i - 1].time <= rt) { a = snaps[i - 1]; b = snaps[i]; break; }
  }
  const t = b.time > a.time ? Math.min(1, Math.max(0, (rt - a.time) / (b.time - a.time))) : 1;
  return { a, b, t };
}
function interpList(a, b, t, key, idIndex, fields) {
  const prev = new Map();
  for (const r of a[key] || []) prev.set(r[idIndex], r);
  return (b[key] || []).map((r) => {
    const p = prev.get(r[idIndex]);
    if (!p) return r;
    const out = r.slice();
    for (const f of fields) out[f] = lerp(p[f], r[f], t);
    return out;
  });
}

function myPlayer() {
  const s = S.last;
  if (!s || S.myId == null) return null;
  for (const p of s.P || []) if (p[0] === S.myId) return { x: p[3], y: p[4], alive: p[21] === 1, raw: p };
  return null;
}

// ── Input ─────────────────────────────────────────────────────────────────────────
const keys = new Set();
let mouse = { x: 0, y: 0, left: false, right: false };
addEventListener("keydown", (e) => {
  if (!S.joined) return;
  const k = e.key.toLowerCase();
  if (!keys.has(k)) {
    if (k === " " || k === "l") S.input.dod++;
    if (k === "q") S.input.swp++;
    if (k === "j") S.input.atk++;
    if (k === "k") S.input.par++;
  }
  keys.add(k);
  if ([" ", "arrowup", "arrowdown", "arrowleft", "arrowright"].includes(k)) e.preventDefault();
});
addEventListener("keyup", (e) => keys.delete(e.key.toLowerCase()));
addEventListener("blur", () => { keys.clear(); mouse.left = mouse.right = false; });
canvas.addEventListener("mousemove", (e) => { const dpr = W / innerWidth; mouse.x = e.clientX * dpr; mouse.y = e.clientY * dpr; });
canvas.addEventListener("mousedown", (e) => {
  if (e.button === 0) { mouse.left = true; S.input.atk++; }
  if (e.button === 2) { mouse.right = true; S.input.par++; }
  if (e.button === 1) { S.input.swp++; e.preventDefault(); }
  startAudio();
});
addEventListener("mouseup", (e) => { if (e.button === 0) mouse.left = false; if (e.button === 2) mouse.right = false; });
canvas.addEventListener("contextmenu", (e) => e.preventDefault());

const pad = { a: false, x: false, y: false, rb: false, lb: false };
function readInput() {
  let mx = 0, my = 0, ax = 0, ay = 0, parry = mouse.right || keys.has("k"), revive = keys.has("e");
  if (keys.has("w") || keys.has("arrowup")) my += 1;
  if (keys.has("s") || keys.has("arrowdown")) my -= 1;
  if (keys.has("a") || keys.has("arrowleft")) mx -= 1;
  if (keys.has("d") || keys.has("arrowright")) mx += 1;
  const me = myPlayer();
  if (me) {
    const wx = S.cam.x + (mouse.x - W / 2) / PPU, wy = S.cam.y - (mouse.y - H / 2) / PPU;
    ax = wx - me.x; ay = wy - me.y;
  }
  const gp = navigator.getGamepads ? [...navigator.getGamepads()].find((g) => g) : null;
  if (gp) {
    const dz = (v) => (Math.abs(v) > 0.2 ? v : 0);
    if (dz(gp.axes[0]) || dz(gp.axes[1])) { mx = dz(gp.axes[0]); my = -dz(gp.axes[1]); }
    if (Math.hypot(gp.axes[2] || 0, gp.axes[3] || 0) > 0.35) { ax = gp.axes[2]; ay = -gp.axes[3]; }
    else if (mx || my) { ax = mx; ay = my; }
    const b = (i) => gp.buttons[i] && gp.buttons[i].pressed;
    if (b(0) && !pad.a) S.input.dod++;
    if (b(3) && !pad.y) S.input.swp++;
    if (b(5) && !pad.rb) S.input.atk++;
    if (b(4) && !pad.lb) S.input.par++;
    pad.a = b(0); pad.y = b(3); pad.rb = b(5); pad.lb = b(4); pad.x = b(2);
    parry = parry || b(4); revive = revive || b(2);
  }
  const len = Math.hypot(mx, my);
  if (len > 1) { mx /= len; my /= len; }
  return { t: "in", mx, my, ax, ay, p: parry ? 1 : 0, r: revive ? 1 : 0, atk: S.input.atk, par: S.input.par, dod: S.input.dod, swp: S.input.swp };
}
function sendInput(now) {
  if (!S.joined) return;
  const m = readInput();
  const key = JSON.stringify(m);
  if (key !== S.lastInput || now - S.lastSent > 100) {
    send(m);
    S.lastInput = key;
    S.lastSent = now;
  }
}

// ── Events → effects & sound ──────────────────────────────────────────────────────
function handleEvent(e) {
  const kind = e[0];
  const me = myPlayer();
  const near = (x, y, r) => me && Math.hypot(x - me.x, y - me.y) < r;
  if (kind === "fx") {
    const [, type, x, y, dx, dy, mag, color] = e;
    switch (type) {
      case "HitEnemy":
        sparks(x, y, dx, dy, 5, "#fff"); blood(x, y, dx, dy, 10, color);
        text(String(Math.round(mag)), x, y + 0.6, "#fff", 0.7, false);
        sfx("flesh", 0.55, x, y);
        break;
      case "PlayerHurt":
        blood(x, y, dx, dy, 14, "#8e0f1e"); sfx("flesh", 0.8, x, y, 0.85);
        if (near(x, y, 0.6)) { S.flash = 0.45; S.flashColor = DANGER; S.shake = 0.5; }
        break;
      case "PerfectParry": case "SwapParry":
        star(x, y, type === "SwapParry" ? GOLD : ACCENT); ring(x, y, 0.2, 2.2, type === "SwapParry" ? GOLD : ACCENT, 0.3);
        sparks(x, y, dx, dy, 18, GOLD);
        if (near(x, y, 3)) text(type === "SwapParry" ? "TROCA PERFEITA" : "PERFEITO", x, y + 0.5, type === "SwapParry" ? GOLD : ACCENT, 1, true);
        sfx("parry", 0.9, x, y); sfx("clash", 0.5, x, y);
        if (near(x, y, 1.6)) { S.flash = 0.25; S.flashColor = "#ffffff"; S.shake = 0.35; }
        break;
      case "PlayerBlock": case "EnemyBlocked":
        star(x, y, "#ffe08a", 0.6); sparks(x, y, dx, dy, 8, "#ffe08a"); sfx("clash", 0.6, x, y); sfx("block", 0.4, x, y);
        break;
      case "PlayerSwing": sfx("swing", 0.32, x, y); break;
      case "EnemySwing": sfx(mag > 1 && mag < 2 ? "slash" : "swing", 0.3, x, y, mag > 1 ? 0.85 : 1); break;
      case "SwapStrike": sfx("slash", 0.6, x, y); ring(x, y, 0.3, 2.2, color, 0.3); break;
      case "Finisher": ring(x, y, 0.3, 3, "#fff", 0.35); sfx("heavy", 0.9, x, y); sfx("splat", 0.8, x, y); break;
      case "FinisherHit": blood(x, y, dx, dy, 14, color); sfx("slash", 0.6, x, y); break;
      case "PostureBreak": case "GuardBreak": ring(x, y, mag * 1.2 || 0.4, 3, GOLD, 0.4); sfx("heavy", 0.7, x, y); if (near(x, y, 3)) text("POSTURA QUEBRADA", x, y + 0.9, GOLD, 0.9, true); break;
      case "EnemyDeath": sfx("splat", 0.45, x, y); break;
      case "PerfectDodge": case "CounterThrust": ring(x, y, 0.2, 1.4, ACCENT, 0.3); sfx("dodge", 0.6, x, y); break;
      case "WeaponSwap": sfx("unsheathe", 0.45, x, y); ring(x, y, 0.2, 0.9, color, 0.2); break;
      case "ItemDrop": ring(x, y, 0.2, 1.4, color, 0.5); sfx("pickup", 0.5, x, y); break;
      case "FlowUp": ring(x, y, 0.4, 2.6, GOLD, 0.45); break;
      case "Burst": ring(x, y, 0.2, mag, color, 0.35); sfx("heavy", 0.5, x, y); break;
      case "Mastered": ring(x, y, 0.3, 1.6, GOLD, 0.4); break;
    }
  } else if (kind === "pop") {
    const near = (x, y, r) => !me || Math.hypot(x - me.x, y - me.y) < r;
    if (near(e[2], e[3], 9)) text(e[1], e[2], e[3] + 0.4, e[4], 1, true);
  } else if (kind === "toast") {
    toast(e[1], e[2]);
  } else if (kind === "blood") {
    blood(e[1], e[2], e[3], e[4], Math.min(40, e[5]), e[6]);
  } else if (kind === "gore") {
    const [, , x, y, dx, dy, color] = e;
    blood(x, y, dx, dy, 60, color); blood(x, y, -dy, dx, 30, color);
    S.stains.push({ x, y, r: 0.9, c: color });
    sfx("splat", 1, x, y);
  }
}

const MAX_PARTICLES = 700, MAX_TEXTS = 10, MAX_RINGS = 40;
function trim(list, max) { if (list.length > max) list.splice(0, list.length - max); }
function sparks(x, y, dx, dy, n, color) {
  for (let i = 0; i < n; i++) {
    const a = Math.atan2(dy, dx) + (Math.random() - 0.5) * 2.2, s = 3 + Math.random() * 6;
    S.particles.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, life: 0.25 + Math.random() * 0.2, age: 0, c: color, spark: true });
  }
  trim(S.particles, MAX_PARTICLES);
}
function blood(x, y, dx, dy, n, color) {
  const base = Math.atan2(dy || 0.0001, dx || 0.0001);
  for (let i = 0; i < n; i++) {
    const a = base + (Math.random() - 0.5) * 1.4, s = 2 + Math.random() * 6;
    S.particles.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, life: 0.3 + Math.random() * 0.3, age: 0, c: color || "#8e0f1e", blood: true, r: 0.05 + Math.random() * 0.07 });
  }
  trim(S.particles, MAX_PARTICLES);
}
function ring(x, y, r0, r1, color, life) { S.rings.push({ x, y, r0, r1, c: color || "#fff", life, age: 0 }); trim(S.rings, MAX_RINGS); }
function star(x, y, color, size) { S.rings.push({ x, y, star: true, size: size || 1, c: color, life: 0.22, age: 0 }); trim(S.rings, MAX_RINGS); }
function text(t, x, y, color, life, big) {
  // The same words at nearly the same place are one popup, not a pile.
  for (const o of S.texts) if (o.t === t && Math.abs(o.x - x) < 1.5 && Math.abs(o.y - y) < 1.5 && o.age < 0.4) return;
  S.texts.push({ t, x, y, c: color || "#fff", life: life || 0.9, age: 0, big });
  trim(S.texts, MAX_TEXTS);
}
function toast(t, color) {
  const box = document.getElementById("toasts");
  const d = document.createElement("div");
  d.textContent = t; d.style.color = color || "#f1e8d6";
  box.prepend(d);
  setTimeout(() => d.remove(), 2700);
  while (box.children.length > 4) box.lastChild.remove();
}

// ── Drawing helpers ───────────────────────────────────────────────────────────────
function sx(x) { return W / 2 + (x - S.cam.x) * PPU; }
function sy(y) { return H / 2 - (y - S.cam.y) * PPU; }

/** One frame of a 4-direction sheet: columns = down, up, left, right. */
function drawFighter(art, x, y, feetY, scale, dir, pose, frame, glowA, glowHex, alpha) {
  const sheet = pose === 3 ? "dead" : pose === 2 ? "attack" : pose === 1 ? "walk" : "idle";
  const e = img("art/" + art + "_" + sheet + ".png");
  const ref = img("art/" + art + "_idle.png");
  if (!e) return;
  const col = pose === 3 ? 0 : dir;
  const row = pose === 1 ? frame % 4 : 0;
  const bottom = ref ? ref.bottom : 0;
  const size = PPU * scale;
  const px = sx(x) - size / 2;
  const py = sy(y + feetY) - size * (16 - bottom) / 16;
  ctx.globalAlpha = alpha;
  ctx.drawImage(e.img, col * 16, row * 16, 16, 16, px, py, size, size);
  if (glowA > 0.02) {
    tintCtx.clearRect(0, 0, 16, 16);
    tintCtx.globalCompositeOperation = "source-over";
    tintCtx.drawImage(e.img, col * 16, row * 16, 16, 16, 0, 0, 16, 16);
    tintCtx.globalCompositeOperation = "source-in";
    tintCtx.fillStyle = glowHex; tintCtx.fillRect(0, 0, 16, 16);
    ctx.globalAlpha = alpha * glowA;
    ctx.drawImage(tint, 0, 0, 16, 16, px, py, size, size);
  }
  ctx.globalAlpha = 1;
}

/** A weapon sprite (drawn blade-up) laid along a world angle, gripped at reach from the centre. */
function drawWeapon(art, x, y, angleDeg, reach, length, alpha) {
  const e = img("art/" + art + ".png");
  if (!e || length <= 0) return;
  const a = angleDeg * Math.PI / 180;
  const gx = sx(x + Math.cos(a) * reach), gy = sy(y + Math.sin(a) * reach);
  const screenAngle = -a;
  const im = e.img;
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.translate(gx, gy);
  if (art === "weapon_Bow") {
    // Bows are held by the middle, belly toward the target.
    ctx.rotate(screenAngle - Math.PI / 2);
    const w = length * PPU, h = w * im.height / im.width;
    ctx.drawImage(im, -w / 2, -h / 2, w, h);
  } else {
    ctx.rotate(screenAngle + Math.PI / 2);
    if (Math.cos(a) < 0) ctx.scale(-1, 1);
    const h = length * PPU, w = h * im.width / im.height;
    const g = GRIP[art] !== undefined ? GRIP[art] : 0.18;
    ctx.drawImage(im, -w / 2, -h * (1 - g), w, h);
  }
  ctx.restore();
}

function drawBar(x, y, w, ratio, color, back) {
  ctx.fillStyle = back || "rgba(0,0,0,.65)";
  ctx.fillRect(sx(x) - w * PPU / 2 - 1, sy(y) - 1, w * PPU + 2, PPU * 0.11 + 2);
  ctx.fillStyle = color;
  ctx.fillRect(sx(x) - w * PPU / 2, sy(y), w * PPU * Math.max(0, Math.min(1, ratio)), PPU * 0.11);
}

function circle(x, y, r, stroke, fill, width) {
  ctx.beginPath(); ctx.arc(sx(x), sy(y), Math.max(0.5, r * PPU), 0, Math.PI * 2);
  if (fill) { ctx.fillStyle = fill; ctx.fill(); }
  if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = width || Math.max(2, PPU * 0.06); ctx.stroke(); }
}
function rgba(hex, a) {
  const n = parseInt(hex.slice(1), 16);
  return "rgba(" + (n >> 16 & 255) + "," + (n >> 8 & 255) + "," + (n & 255) + "," + a + ")";
}

// ── Frame ─────────────────────────────────────────────────────────────────────────
let lastFrame = performance.now();
function frame(now, fromTimer) {
  // Effects age in real time even when the tab is throttled; motion smoothing uses a capped step.
  const real = Math.min(0.5, (now - lastFrame) / 1000);
  const dt = Math.min(0.05, real);
  lastFrame = now;
  sendInput(now);
  draw(dt, now, real);
  updateHud();
  if (!fromTimer) requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
// Keys must reach the host even if the browser stops animation frames (busy or background tab):
// input goes out on its own clock, and drawing falls back to it when frames stall.
setInterval(() => {
  const now = performance.now();
  sendInput(now);
  if (now - lastFrame > 120) frame(now, true);
}, 33);

function draw(dt, now, real) {
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  ctx.fillStyle = "#0d0b0c";
  ctx.fillRect(0, 0, W, H);
  const fs = frameState();
  if (!fs || !S.room) return;
  const { a, b, t } = fs;
  const P = interpList(a, b, t, "P", 0, [3, 4]);
  const E = interpList(a, b, t, "E", 0, [2, 3]);
  // Camera follows this player (or a living teammate while down), smoothly.
  let focus = P.find((p) => p[0] === S.myId);
  if (focus && focus[21] !== 1) focus = P.find((p) => p[21] === 1) || focus;
  if (!focus) focus = P[0];
  if (focus) {
    S.cam.x += (focus[3] - S.cam.x) * Math.min(1, dt * 10);
    S.cam.y += (focus[4] - S.cam.y) * Math.min(1, dt * 10);
  }
  if (S.shake > 0) {
    S.shake = Math.max(0, S.shake - dt * 2);
    ctx.translate((Math.random() - 0.5) * S.shake * PPU * 0.4, (Math.random() - 0.5) * S.shake * PPU * 0.4);
  }

  const room = S.room;
  if (S.arena) {
    const k = PPU / 16;
    ctx.drawImage(S.arena, sx(-room.px / 16), sy((room.ph - room.py) / 16), room.pw * k, room.ph * k);
  }
  // Floor stains.
  for (const s of S.stains) circle(s.x, s.y, s.r, null, rgba(s.c, 0.55));
  drawTraps(b, room, now);

  // Telegraphs under the fighters.
  for (const e of E) {
    if (!e[22]) continue;
    const [x, y] = [e[2], e[3]];
    const col = e[28] ? DANGER : "#ffffff";
    const ang = Math.atan2(e[26], e[25]);
    ctx.save();
    ctx.fillStyle = rgba(col, Math.min(0.6, e[27]));
    if (e[22] === 1) {
      const half = (e[24] || 110) * Math.PI / 360;
      ctx.beginPath(); ctx.moveTo(sx(x), sy(y));
      ctx.arc(sx(x), sy(y), e[23] * PPU, -ang - half, -ang + half);
      ctx.closePath(); ctx.fill();
    } else if (e[22] === 2) {
      circle(x, y, e[23], rgba(col, Math.min(1, e[27] * 2)), rgba(col, e[27] * 0.5));
    } else if (e[22] === 3) {
      ctx.translate(sx(x), sy(y)); ctx.rotate(-ang);
      ctx.fillRect(0, -e[24] * PPU / 2, e[23] * PPU, e[24] * PPU);
    }
    ctx.restore();
  }

  // Loot.
  for (const it of b.I || []) {
    const glow = ctx.createRadialGradient(sx(it[0]), sy(it[1]), 0, sx(it[0]), sy(it[1]), PPU * 0.8);
    glow.addColorStop(0, rgba(it[2], 0.9)); glow.addColorStop(1, rgba(it[2], 0));
    ctx.fillStyle = glow; ctx.fillRect(sx(it[0]) - PPU, sy(it[1]) - PPU, PPU * 2, PPU * 2);
    ctx.fillStyle = rgba(it[2], 0.35);
    const beam = 1.2 + 0.45 * it[3];
    ctx.fillRect(sx(it[0]) - PPU * 0.05, sy(it[1] + beam), PPU * 0.1, beam * PPU);
    circle(it[0], it[1] + 0.15 + 0.08 * Math.sin(now / 300 + it[0]), 0.16, "#1a1420", it[2], 2);
  }

  // Shadows, then everything that stands, back to front.
  const things = [];
  for (const p of P) things.push({ y: p[4], kind: "p", d: p });
  for (const e of E) things.push({ y: e[3], kind: "e", d: e });
  for (const l of room.lan || []) things.push({ y: l[1], kind: "lan", d: l });
  for (const s of room.sta || []) things.push({ y: s[1], kind: "sta", d: s });
  things.sort((u, v) => v.y - u.y);
  for (const th of things) if (th.kind === "p" || th.kind === "e") {
    const d = th.d; const x = th.kind === "p" ? d[3] : d[2]; const y = th.kind === "p" ? d[4] : d[3];
    ctx.save(); ctx.translate(sx(x), sy(y - 0.22)); ctx.scale(1, 0.4);
    ctx.fillStyle = "rgba(0,0,0,.4)"; ctx.beginPath(); ctx.arc(0, 0, PPU * 0.45, 0, Math.PI * 2); ctx.fill(); ctx.restore();
  }
  for (const th of things) {
    const d = th.d;
    if (th.kind === "p") drawPlayer(d);
    else if (th.kind === "e") drawEnemy(d);
    else if (th.kind === "lan") drawLantern(d, room, now);
    else drawStatue(d, room);
  }

  // Arrows.
  for (const r of b.R || []) {
    const e = img("art/weapon_Arrow.png");
    const ang = Math.atan2(r[3], r[2]);
    if (r[4]) circle(r[0], r[1], 0.3, null, rgba(ACCENT, 0.4));
    if (!e) { circle(r[0], r[1], 0.12, null, "#fff"); continue; }
    ctx.save();
    ctx.translate(sx(r[0] - r[2] * 0.75), sy(r[1] - r[3] * 0.75));
    ctx.rotate(-(ang - Math.PI / 4));
    const size = 0.75 * 16 / 20.8 * PPU;
    ctx.drawImage(e.img, -size * 0.06, -size * 0.94, size, size);
    ctx.restore();
  }

  // Timing rings, bars and names on top.
  for (const e of E) drawEnemyOverlay(e);
  for (const p of P) drawPlayerOverlay(p, b);
  drawEffects(real);

  if (S.flash > 0) {
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = rgba(S.flashColor, S.flash * 0.5);
    ctx.fillRect(0, 0, W, H);
    S.flash = Math.max(0, S.flash - dt * 3);
  }
}

function drawPlayer(p) {
  const alive = p[21] === 1;
  const isMe = p[0] === S.myId;
  drawFighter(p[2], p[3], p[4], -0.228, 1, p[7], alive ? p[8] : 3, p[9], p[15], p[16], alive ? Math.max(0.35, p[23]) : 0.9);
  if (alive && p[13]) drawWeapon(p[14], p[3], p[4], p[10], p[11], p[12], Math.max(0.35, p[23]));
  if (isMe) circle(p[3], p[4] - 0.2, 0.55, rgba(GOLD, 0.35), null, 2);
}

function drawEnemy(e) {
  drawFighter(e[1], e[2], e[3], e[8], e[7], e[4], e[5], e[6], e[14], e[15], e[16]);
  if (e[12] && !e[32]) drawWeapon(e[13], e[2], e[3], e[9], e[10], e[11], e[16]);
}

function drawLantern(l, room, now) {
  const e = img("art/tiles_element.png");
  const flicker = 0.13 + 0.07 * Math.sin(now / 170 + l[0]);
  const g = ctx.createRadialGradient(sx(l[0]), sy(l[1] + 1.1), 0, sx(l[0]), sy(l[1] + 1.1), PPU * 3.5);
  g.addColorStop(0, rgba(room.flame || "#f2a541", flicker * 2)); g.addColorStop(1, rgba(room.flame || "#f2a541", 0));
  ctx.fillStyle = g; ctx.fillRect(sx(l[0]) - PPU * 3.5, sy(l[1] + 1.1) - PPU * 3.5, PPU * 7, PPU * 7);
  if (e) ctx.drawImage(e.img, 0, 80, 16, 32, sx(l[0]) - PPU / 2, sy(l[1] + 2), PPU, PPU * 2);
}

function drawStatue(s, room) {
  const e = img("art/tiles_house.png");
  if (!e) return;
  const row = room.moss ? 19 : 15;
  if (s[3] === 1) {
    ctx.drawImage(e.img, 16, row * 16, 32, 48, sx(s[0]) - PPU, sy(s[1] + 3), PPU * 2, PPU * 3);
    ctx.drawImage(e.img, 0, row * 16, 16, 48, sx(s[4] + 0.5) - PPU / 2, sy(s[1] + 3), PPU, PPU * 3);
    ctx.drawImage(e.img, 0, row * 16, 16, 48, sx(s[4] + s[2] - 0.5) - PPU / 2, sy(s[1] + 3), PPU, PPU * 3);
  } else {
    ctx.drawImage(e.img, 64, row * 16, 32, 32, sx(s[0]) - PPU, sy(s[1] + 2), PPU * 2, PPU * 2);
  }
}

function drawTraps(snap, room, now) {
  const dungeon = img("art/tiles_dungeon.png");
  const flame = img("art/fx_flame.png");
  const traps = room.trap || [];
  for (let i = 0; i < traps.length; i++) {
    const t = traps[i];
    const state = (snap.T || [])[i];
    const [type, ox, oy, dx, dy, len, cells] = t;
    if (type === 0) {
      for (let c = 0; c < cells.length; c += 2) {
        const x = cells[c], y = cells[c + 1];
        if (dungeon) ctx.drawImage(dungeon.img, state === 2 ? 64 : 80, 16, 16, 16, sx(x - 0.5), sy(y + 0.5), PPU, PPU);
        if (state === 1 && Math.floor(now / 100) % 2 === 0) { ctx.fillStyle = "rgba(255,70,50,.35)"; ctx.fillRect(sx(x - 0.5), sy(y + 0.5), PPU, PPU); }
      }
    } else if (type === 1) {
      if (dungeon) ctx.drawImage(dungeon.img, 32, 48, 16, 16, sx(ox - 0.5), sy(oy + 0.5), PPU, PPU);
      if (state >= 1) circle(ox, oy, 0.8, null, rgba("#ff8030", state === 1 ? 0.25 + 0.2 * Math.sin(now / 50) : 0.5));
      if (state === 2 && flame) {
        const n = Math.max(2, Math.round(len / 0.75));
        for (let k = 0; k < n; k++) {
          const px = ox + dx * (0.5 + k * len / n), py = oy + dy * (0.5 + k * len / n);
          const f = (Math.floor(now / 70) + k) % 8;
          const w = PPU * 25 / 16 * 0.9, h = PPU * 30 / 16 * 0.9;
          ctx.drawImage(flame.img, f * 25, 0, 25, 30, sx(px) - w / 2, sy(py) - h * 0.85, w, h);
        }
      }
    } else if (type === 2) {
      circle(ox, oy, len * 1.05, rgba("#ff5a40", 0.15), null, 2);
      if (dungeon) ctx.drawImage(dungeon.img, 32, 32, 16, 16, sx(ox - 0.5), sy(oy + 0.5), PPU, PPU);
      const ang = typeof state === "number" ? state : 0;
      for (const off of [0, 180]) {
        const a = (ang + off) * Math.PI / 180;
        drawWeapon("weapon_BigSword", ox + Math.cos(a) * 0.2, oy + Math.sin(a) * 0.2, ang + off, 0, len, 1);
      }
    }
  }
}

function drawEnemyOverlay(e) {
  const [x, y] = [e[2], e[3]];
  if (e[29] && !e[32]) {
    const col = e[28] ? DANGER : ACCENT;
    const size = e[30];
    if (e[29] === 1) {
      const s = size * (3.4 + (1 - 3.4) * e[31]);
      circle(x, y, s / 2, rgba(col, 0.15 + 0.8 * e[31]), null, Math.max(2, PPU * 0.07));
      circle(x, y, size / 2, rgba(col, 0.35), null, 2);
    } else {
      const k = e[31];
      circle(x, y, size * 0.95, null, rgba(col, 0.45 * (1 - k * 0.5)));
      circle(x, y, size * (1.18 - 0.18 * k) / 2, "#ffffff", null, Math.max(3, PPU * 0.09));
    }
  }
  if (e[32]) return;
  const top = y + e[8] + e[7] * 1.05 + 0.25;
  drawBar(x, top, 1.3, e[17], "#c8553d");
  drawBar(x, top - 0.14, 1.3, e[18], e[20] ? GOLD : "#e9a35a");
  if (e[19]) circle(x, y, 1.4 * e[7], rgba(GOLD, 0.3), null, 2);
  if (e[21]) circle(x + 0.85, top, 0.12, GOLD, null, 2);
  if (e[20]) circle(x, y, 0.9 + 0.08 * Math.sin(performance.now() / 80), GOLD, null, 3);
}

function drawPlayerOverlay(p, snap) {
  const isMe = p[0] === S.myId;
  const alive = p[21] === 1;
  if (!isMe) {
    ctx.font = Math.round(PPU * 0.32) + "px SpectralBold, serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(0,0,0,.7)"; ctx.fillText(p[1], sx(p[3]) + 2, sy(p[4] + 1.15) + 2);
    ctx.fillStyle = alive ? "#f1e8d6" : DANGER; ctx.fillText(p[1], sx(p[3]), sy(p[4] + 1.15));
    if (alive) drawBar(p[3], p[4] + 0.95, 1.0, p[17], "#d63a2c");
  }
  if (!alive) {
    circle(p[3], p[4] + 1.3, 0.45, rgba("#f1e8d6", 0.5), null, 2);
    if (p[22] > 0) drawBar(p[3], p[4] + 0.6, 1.2, p[22], GOLD);
  }
}

function drawEffects(dt) {
  for (let i = S.particles.length - 1; i >= 0; i--) {
    const q = S.particles[i];
    q.age += dt;
    q.x += q.vx * dt; q.y += q.vy * dt; q.vx *= 0.9; q.vy *= 0.9;
    if (q.age >= q.life) {
      if (q.blood && S.stains.length < 600 && Math.random() < 0.35) S.stains.push({ x: q.x, y: q.y, r: q.r * 1.4, c: q.c });
      S.particles.splice(i, 1);
      continue;
    }
    const a = 1 - q.age / q.life;
    if (q.spark) {
      ctx.strokeStyle = rgba(q.c, a); ctx.lineWidth = Math.max(1, PPU * 0.05);
      ctx.beginPath(); ctx.moveTo(sx(q.x), sy(q.y)); ctx.lineTo(sx(q.x - q.vx * 0.03), sy(q.y - q.vy * 0.03)); ctx.stroke();
    } else {
      ctx.fillStyle = rgba(q.c, a);
      ctx.fillRect(sx(q.x) - q.r * PPU / 2, sy(q.y) - q.r * PPU / 2, q.r * PPU, q.r * PPU);
    }
  }
  for (let i = S.rings.length - 1; i >= 0; i--) {
    const r = S.rings[i];
    r.age += dt;
    if (r.age >= r.life) { S.rings.splice(i, 1); continue; }
    const t = r.age / r.life;
    if (r.star) {
      const s = r.size * (0.6 + t) * PPU;
      ctx.save(); ctx.translate(sx(r.x), sy(r.y)); ctx.rotate(t * 0.6);
      ctx.fillStyle = rgba(r.c, 1 - t);
      for (let k = 0; k < 4; k++) { ctx.rotate(Math.PI / 2); ctx.beginPath(); ctx.moveTo(0, -s); ctx.lineTo(s * 0.12, 0); ctx.lineTo(-s * 0.12, 0); ctx.fill(); }
      ctx.restore();
    } else {
      circle(r.x, r.y, r.r0 + (r.r1 - r.r0) * t, rgba(r.c, 1 - t), null, Math.max(2, PPU * 0.08 * (1 - t)));
    }
  }
  ctx.textAlign = "center";
  for (let i = S.texts.length - 1; i >= 0; i--) {
    const tx = S.texts[i];
    tx.age += dt;
    if (tx.age >= tx.life) { S.texts.splice(i, 1); continue; }
    const a = 1 - Math.max(0, (tx.age / tx.life - 0.6) / 0.4);
    ctx.font = Math.round(PPU * (tx.big ? 0.55 : 0.42)) + "px Kaushan, cursive";
    const y = sy(tx.y + tx.age * 1.1);
    ctx.fillStyle = "rgba(0,0,0," + 0.7 * a + ")"; ctx.fillText(tx.t, sx(tx.x) + 2, y + 2);
    ctx.fillStyle = rgba(tx.c.length === 7 ? tx.c : "#ffffff", a); ctx.fillText(tx.t, sx(tx.x), y);
  }
}

// ── HUD (DOM) ─────────────────────────────────────────────────────────────────────
const $ = (id) => document.getElementById(id);
let hudTimer = 0;
function updateHud() {
  const now = performance.now();
  if (now - hudTimer < 66) return;
  hudTimer = now;
  const s = S.last;
  if (!s || !S.joined) return;
  const me = (s.P || []).find((p) => p[0] === S.myId);
  if (me) {
    $("myName").textContent = me[1];
    $("ping").textContent = "~" + Math.max(5, Math.round(me[28] * 16.7 - 33)) + " ms";
    $("hpText").textContent = me[26] + " / " + me[27];
    $("hpFill").style.width = (me[17] * 100) + "%";
    $("hpGhost").style.width = (me[17] * 100) + "%";
    $("postFill").style.width = (me[18] * 100) + "%";
    let d = "";
    for (let i = 0; i < me[20]; i++) d += '<span class="' + (me[19] >= i + 1 ? "" : "off") + '"></span>';
    $("dodges").innerHTML = d;
    $("weapon").textContent = me[24];
    $("weapon2").textContent = "‹›  " + me[25] + "  (Q)";
  }
  $("room").textContent = s.room || "";
  $("wave").textContent = s.wave || "";
  let f = "";
  for (let i = 0; i < 3; i++) f += '<b class="' + ((s.fl || 0) > i ? "on" : "") + '"></b>';
  $("flow").innerHTML = f;
  $("flowFill").style.width = ((s.fp || 0) * 100) + "%";
  $("streak").textContent = (s.st || 0) >= 2 ? "SEQUÊNCIA x" + s.st : "";

  let team = "";
  for (const p of s.P || []) {
    if (p[0] === S.myId) continue;
    const down = p[21] !== 1;
    team += '<div class="m' + (down ? " down" : "") + '"><span>' + escapeHtml(p[1]) + (down ? " · caído" : "") + '</span><div class="bar thin' + (down ? " gold" : "") + '"><i style="width:' +
      ((down ? p[22] : p[17]) * 100) + '%;background:' + (down ? GOLD : "#d63a2c") + '"></i></div></div>';
  }
  $("team").innerHTML = team;
  $("team").style.display = team ? "" : "none";

  // Revive prompt: standing over a fallen teammate.
  let prompt = "";
  if (me && me[21] === 1) {
    for (const p of s.P) {
      if (p[0] !== S.myId && p[21] !== 1 && Math.hypot(p[3] - me[3], p[4] - me[4]) <= 1.4) { prompt = "Segure E para reviver " + p[1]; break; }
    }
  }
  $("prompt").textContent = prompt;
  $("prompt").style.display = prompt ? "block" : "none";

  let title = "", sub = "";
  const others = (s.P || []).length;
  if (s.m === "lobby") { title = "Na sala"; sub = "Aguardando o anfitrião começar a run · " + others + " duelistas"; }
  else if (s.m === "pause") { title = "Pausa"; sub = "O anfitrião pausou o duelo"; }
  else if (s.m === "reward") { title = "Recompensa"; sub = "O anfitrião escolhe para a equipe: " + (s.rw || []).join("  ·  "); }
  else if (s.m === "equip") { title = "Equipamento"; sub = "O anfitrião está ajustando os itens da equipe"; }
  else if (s.m === "over") { title = "A morte ensina"; sub = "A run acabou. Aguardando o anfitrião."; }
  else if (me && me[21] !== 1) { title = "Você caiu"; sub = "Um aliado pode te reviver ficando sobre você e segurando E"; }
  $("center").innerHTML = title ? "<h2>" + title + "</h2><p>" + escapeHtml(sub) + "</p>" : "";
  $("center").style.display = title ? "block" : "none";
}
function escapeHtml(t) { return String(t).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c])); }
