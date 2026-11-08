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
// The samurai a player can be (same order as the host's PlayerController.PartyArt).
const SKINS = [
  { art: "Samurai", name: "Samurai de Palha" }, { art: "SamuraiRed", name: "Samurai Escarlate" },
  { art: "NinjaBlue", name: "Ninja Azul" }, { art: "Monk", name: "Monge" }, { art: "Master", name: "Mestre" },
  { art: "NinjaGreen", name: "Ninja Verde" }, { art: "Hunter", name: "Caçador" }, { art: "FighterWhite", name: "Lutador Branco" },
];
const GRIP = { weapon_Lance: 0.3, weapon_Lance2: 0.3, weapon_Stick: 0.3, weapon_Club: 0.25, weapon_Hammer: 0.25, weapon_AxeTool: 0.25 };
const DANGER = "#ff5a3c", ACCENT = "#6fc3df", GOLD = "#f2c14e";
const VIEW_UNITS = 14;          // world units visible vertically
const INTERP_DELAY = 45;        // ms behind the newest snapshot (smooth motion)

// ── State ─────────────────────────────────────────────────────────────────────────
const S = {
  ws: null, myId: null, joined: false, room: null, arena: null, arenaVersion: -1,
  snaps: [], last: null, picks: ["katana", "naginata"],
  particles: [], texts: [], stains: [], rings: [], anims: [], shake: 0, flash: 0, flashColor: "#fff",
  cam: { x: 0, y: 0 }, input: { atk: 0, par: 0, dod: 0, swp: 0, det: 0, kia: 0, tl: 0, tc: 0, gd: 0, bd: 0 }, kamae: -1, lastSent: 0, lastInput: "",
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

// ── Volume (each player sets their own; remembered in this browser) ──────────────
const VOL = { music: 0.35, sfx: 0.8 };
try {
  const v = JSON.parse(localStorage.getItem("shura.vol") || "null");
  if (v && typeof v.music === "number") VOL.music = v.music;
  if (v && typeof v.sfx === "number") VOL.sfx = v.sfx;
} catch (e) { /* private mode */ }
function bindVolume() {
  for (const el of document.querySelectorAll(".volMusic")) {
    el.value = Math.round(VOL.music * 100);
    el.oninput = () => { VOL.music = el.value / 100; saveVolume(); };
  }
  for (const el of document.querySelectorAll(".volSfx")) {
    el.value = Math.round(VOL.sfx * 100);
    el.oninput = () => { VOL.sfx = el.value / 100; saveVolume(); };
  }
}
function saveVolume() {
  for (const el of document.querySelectorAll(".volMusic")) el.value = Math.round(VOL.music * 100);
  for (const el of document.querySelectorAll(".volSfx")) el.value = Math.round(VOL.sfx * 100);
  if (musicGain) musicGain.gain.value = VOL.music;
  try { localStorage.setItem("shura.vol", JSON.stringify(VOL)); } catch (e) { /* ignore */ }
}

// ── Sound ─────────────────────────────────────────────────────────────────────────
const AudioCtx = window.AudioContext || window.webkitAudioContext;
let audio = null, manifest = {}, buffers = {}, musicGain = null;
// Two moods, each a shuffled playlist crossfading track to track (same as the host's game).
const moods = { calm: { files: [], gain: null, level: 0, target: 0, src: null, last: -1, endsAt: 0 },
                fight: { files: [], gain: null, level: 0, target: 0, src: null, last: -1, endsAt: 0 } };
async function startAudio() {
  if (audio) return;
  try { audio = new AudioCtx(); } catch (e) { return; }
  try { manifest = await (await fetch("sfx/manifest.json")).json(); } catch (e) { manifest = {}; }
  musicGain = audio.createGain(); musicGain.gain.value = VOL.music; musicGain.connect(audio.destination);
  let lists = {};
  try { lists = await (await fetch("music/playlists.json")).json(); } catch (e) { lists = {}; }
  for (const k of ["calm", "fight"]) {
    const m = moods[k];
    m.files = lists[k] || [];
    m.gain = audio.createGain(); m.gain.gain.value = 0; m.gain.connect(musicGain);
    nextTrack(k);
  }
}
function nextTrack(k) {
  const m = moods[k];
  if (!m.files.length) return;
  let pick = Math.floor(Math.random() * m.files.length);
  if (m.files.length > 1 && pick === m.last) pick = (pick + 1) % m.files.length;
  m.last = pick;
  // Streamed, not decoded whole: minutes of music would cost hundreds of MB as PCM.
  const el = new Audio("music/" + k + "/" + m.files[pick]);
  el.preload = "auto";
  const node = audio.createMediaElementSource(el);
  const fade = audio.createGain(); node.connect(fade); fade.connect(m.gain);
  const now = audio.currentTime, X = 4;
  fade.gain.setValueAtTime(0, now); fade.gain.linearRampToValueAtTime(1, now + X);
  if (m.src) {
    const old = m.src;
    old.fade.gain.setValueAtTime(old.fade.gain.value, now); old.fade.gain.linearRampToValueAtTime(0, now + X);
    setTimeout(() => { old.el.pause(); old.el.src = ""; }, (X + 0.2) * 1000);
  }
  el.play().catch(() => { /* resumes on the next user gesture */ });
  m.src = { el, fade };
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
function sfx(cat, volume, x, y, pitch) {
  if (!audio || !manifest[cat]) return;
  const files = manifest[cat];
  const url = "sfx/" + cat + "/" + files[(Math.random() * files.length) | 0];
  const me = myPlayer();
  let v = volume * VOL.sfx;
  if (v <= 0.001) return;
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
  const mine = (snap.P || []).find((p) => p[0] === S.myId);
  const intense = combat && (mine ? mine[29] || 0 : 0) >= 2;
  const target = { calm: combat ? 0 : 0.6, fight: combat ? (intense ? 0.8 : 0.7) : 0 };
  for (const k in moods) {
    const m = moods[k];
    if (!m.gain) continue;
    m.level += (target[k] - m.level) * 0.04;
    m.gain.gain.value = m.level;
    const el = m.src && m.src.el;
    if (el && el.duration > 8 && !m.switching && (el.ended || el.duration - el.currentTime < 4)) {
      m.switching = true; nextTrack(k); setTimeout(() => { m.switching = false; }, 6000);
    } else if (el && el.paused && !el.ended && audio.state === "running") el.play().catch(() => {});
  }
}

// ── Lobby ─────────────────────────────────────────────────────────────────────────
const nameInput = document.getElementById("name");
try { nameInput.value = localStorage.getItem("shura.name") || ""; } catch (e) { /* private mode */ }
try { S.art = Math.max(0, Math.min(SKINS.length - 1, parseInt(localStorage.getItem("shura.art") || "-1", 10))); } catch (e) { S.art = -1; }
if (isNaN(S.art)) S.art = -1;
try { const w = JSON.parse(localStorage.getItem("shura.weapons") || "null"); if (w && w.length === 2) S.picks = w; } catch (e) { /* ignore */ }
const skinsBox = document.getElementById("skins");
function renderSkins() {
  skinsBox.innerHTML = "";
  SKINS.forEach((sk, i) => {
    const d = document.createElement("div");
    d.className = "skin" + (S.art === i ? " on" : "");
    d.title = sk.name;
    d.style.backgroundImage = "url(art/" + sk.art + "_idle.png)";
    d.onclick = () => { S.art = i; renderSkins(); loadoutChanged(); };
    skinsBox.appendChild(d);
  });
}
renderSkins();
/** Once in the room, every change of name, look or weapons goes to the host at once. */
let loadoutTimer = 0;
function loadoutChanged() {
  try {
    localStorage.setItem("shura.art", String(S.art));
    localStorage.setItem("shura.weapons", JSON.stringify(S.picks));
  } catch (e) { /* ignore */ }
  if (!S.joined || S.picks.length !== 2) return;
  clearTimeout(loadoutTimer);
  loadoutTimer = setTimeout(() => {
    send({ t: "loadout", name: nameInput.value.trim(), a: S.picks[0], b: S.picks[1], art: S.art });
  }, 150);
}
nameInput.addEventListener("input", () => {
  try { localStorage.setItem("shura.name", nameInput.value.trim()); } catch (e) { /* ignore */ }
  loadoutChanged();
});
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
      loadoutChanged();
    };
    weaponsBox.appendChild(d);
  }
  updateJoinButton();
}
function updateJoinButton() {
  const b = document.getElementById("join");
  if (S.joined) {
    b.disabled = S.picks.length !== 2;
    b.textContent = S.ready ? "Pronto! (clique para cancelar)" : "Estou pronto";
  } else {
    b.disabled = !(S.ws && S.ws.readyState === 1 && S.picks.length === 2);
    b.textContent = "Entrar no duelo";
  }
}
renderWeapons();
document.getElementById("join").onclick = () => {
  startAudio();
  if (S.joined) {
    S.ready = !S.ready;
    send({ t: "ready", rd: S.ready ? 1 : 0 });
    updateJoinButton();
    return;
  }
  const name = nameInput.value.trim() || "Ronin";
  try { localStorage.setItem("shura.name", name); } catch (e) { /* ignore */ }
  send({ t: "join", name, a: S.picks[0], b: S.picks[1], art: S.art });
  setStatus("Entrando…");
};
bindVolume();

/** Shows the room (lobby) while the host is in it; the duel otherwise. */
function showRoom(inRoom) {
  const lobby = document.getElementById("lobby");
  if (lobby.hidden === !inRoom && S.roomShown === inRoom) return;
  S.roomShown = inRoom;
  lobby.hidden = !inRoom;
  document.getElementById("hud").hidden = inRoom;
  document.getElementById("party").hidden = !S.joined;
  document.getElementById("lobbyTitle").textContent = S.joined ? "Na sala" : "Duelo online";
  document.getElementById("lobbyTag").textContent = S.joined
    ? "Escolha seu nome, aparência e armas. O anfitrião começa a run quando todos estiverem prontos."
    : "Entre na sala do anfitrião e lute ao lado dele, até 8 duelistas.";
  if (inRoom) { closeMenu(); hideRewards(); }
  updateJoinButton();
}

let partyKey = "";
function renderParty(snap) {
  const P = snap.P || [];
  const key = JSON.stringify(P.map((p) => [p[0], p[1], p[33], p[24], p[25], p[32], Math.round(p[28] / 3)]));
  if (key === partyKey) return;
  partyKey = key;
  let html = "";
  for (const p of P) {
    const skin = SKINS[p[33]] || SKINS[0];
    const me = p[0] === S.myId;
    const ready = p[32] === 1;
    const host = p[0] === 0;
    html += '<div class="pm"><div class="face" style="background-image:url(art/' + skin.art + '_idle.png)"></div>' +
      '<div><div class="nm">' + escapeHtml(p[1]) + (me ? " (você)" : host ? " · anfitrião" : "") + '</div><div class="wp">' +
      escapeHtml(p[24]) + "  ·  " + escapeHtml(p[25]) + "</div></div>" +
      '<span class="rd' + (ready ? " yes" : "") + '">' + (host ? "" : ready ? "pronto" : "escolhendo…") + "</span>" +
      '<span class="wp">' + (host ? "" : "~" + Math.max(5, Math.round(p[28] * 16.7 - 33)) + " ms") + "</span></div>";
  }
  document.getElementById("partyList").innerHTML = html;
}

// ── Personal rewards ──────────────────────────────────────────────────────────────
const KINDS = ["Técnica", "Relíquia", "Bênção", "Item"];
const KIND_COLORS = ["#8c5e0c", "#b3261e", "#2d4f73", "#5e5146"];
function showRewards(list) {
  S.rewards = list;
  const box = document.getElementById("cards");
  box.innerHTML = "";
  list.forEach((r, i) => {
    const d = document.createElement("div");
    d.className = "card";
    d.innerHTML = '<span class="key">' + (i + 1) + '</span><div class="kind" style="color:' + KIND_COLORS[r.k] + '">' + KINDS[r.k] + "</div>" +
      '<div class="nm">' + escapeHtml(r.n) + '</div><div class="g">' + escapeHtml(r.g) + "</div>" +
      (r.c ? '<div class="c">Custo: ' + escapeHtml(r.c) + "</div>" : "") +
      (r.src ? '<div class="src">Absorvida de ' + escapeHtml(r.src) + "</div>" : "");
    d.onclick = () => pickReward(i);
    box.appendChild(d);
  });
  document.getElementById("rewards").hidden = list.length === 0;
  closeMenu();
}
function pickReward(i) {
  if (!S.rewards || i < 0 || i >= S.rewards.length) return;
  send({ t: "pick", i });
  hideRewards();
}
function hideRewards() { S.rewards = null; document.getElementById("rewards").hidden = true; }

// ── Equipment & sound menu (the duel keeps going) ────────────────────────────────
function openMenu() {
  if (!S.joined || S.roomShown) return;
  S.menuOpen = true;
  document.getElementById("menu").hidden = false;
  send({ t: "inv" });
  keys.clear(); mouse.left = mouse.right = false;
}
function closeMenu() { S.menuOpen = false; document.getElementById("menu").hidden = true; }
document.getElementById("closeMenu").onclick = closeMenu;
function renderInventory(m) {
  const itemHtml = (it, slotName, i, equipped) => {
    if (!it) return '<div class="item empty"><div class="sl">' + escapeHtml(slotName) + "</div>vazio</div>";
    return '<div class="item" data-i="' + i + '" data-eq="' + (equipped ? 1 : 0) + '"><div class="sl">' + escapeHtml(slotName) + "</div>" +
      '<div class="nm" style="color:' + it.rc + '">' + escapeHtml(it.n) + ' <span class="sl">' + escapeHtml(it.r) + "</span></div>" +
      '<div class="g">' + escapeHtml(it.g) + "</div></div>";
  };
  let eq = "";
  m.eq.forEach((it, i) => { eq += itemHtml(it, m.slots[i], i, true); });
  let bag = "";
  m.bag.forEach((it, i) => { bag += itemHtml(it, m.slots[it.s], i, false); });
  document.getElementById("equipped").innerHTML = eq;
  document.getElementById("bag").innerHTML = bag || '<div class="item empty">A mochila está vazia. Itens caem dos inimigos.</div>';
  document.getElementById("owned").innerHTML = (m.own || []).map((o) => '<span class="k' + o[1] + '">' + escapeHtml(o[0]) + "</span>").join("") ||
    '<span>Nada ainda: escolha recompensas ao vencer salas.</span>';
  const st = m.st || [];
  document.getElementById("stats").textContent = "Vida " + st[0] + "  ·  Postura " + st[1] + "  ·  Dano " + st[2] + "%  ·  Esquivas " + st[3] + "  ·  Parry " + st[4] + " frames";
  for (const el of document.querySelectorAll("#menu .item[data-i]")) {
    el.onclick = () => send({ t: el.dataset.eq === "1" ? "unequip" : "equip", i: parseInt(el.dataset.i, 10) });
  }
}
function setStatus(t) { document.getElementById("status").textContent = t; }

// ── Network ───────────────────────────────────────────────────────────────────────
function connect() {
  const ws = new WebSocket("ws://" + location.host + "/ws");
  S.ws = ws;
  ws.onopen = () => { setStatus("Conectado ao anfitrião. Escolha e entre!"); renderWeapons(); };
  ws.onclose = () => {
    setStatus("Sem conexão com o anfitrião. Tentando de novo…");
    S.joined = false; S.ready = false;
    closeMenu(); hideRewards();
    showRoom(true);
    setTimeout(connect, 2000);
  };
  ws.onmessage = (ev) => {
    let m;
    try { m = JSON.parse(ev.data); } catch (e) { return; }
    switch (m.t) {
      case "s": onSnapshot(m); break;
      case "room": onRoom(m); break;
      case "welcome":
        S.myId = m.id; S.joined = true; S.ready = false;
        if (typeof m.art === "number") { S.art = m.art; renderSkins(); }
        S.roomShown = null;
        setStatus("");
        break;
      case "rewards": showRewards(m.list || []); break;
      case "route": showRoute(m); break;
      case "routed": hideRoute(); break;
      case "picked": hideRewards(); toast("Obtido: " + m.n, GOLD); break;
      case "inv": renderInventory(m); break;
      case "toast": toast(m.text, m.c); break;
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
  if (S.joined) {
    if (S.lastMode === "lobby" && m.m !== "lobby" && S.ready) { S.ready = false; updateJoinButton(); }
    S.lastMode = m.m;
    // Between runs (the room, or after a defeat) everyone can change name, look and weapons.
    const between = m.m === "lobby" || m.m === "over";
    showRoom(between);
    if (between) renderParty(m);
    if (between) {
      document.getElementById("lobbyTitle").textContent = m.m === "over" ? "A morte ensina" : "Na sala";
    }
    if (m.m !== "reward" && S.rewards) hideRewards();
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
  if (k === "escape" || k === "tab") {
    e.preventDefault();
    if (S.menuOpen) closeMenu(); else openMenu();
    return;
  }
  if (S.rewards && ["1", "2", "3", "4"].includes(k)) { pickReward(parseInt(k, 10) - 1); return; }
  if (S.route && ["1", "2", "3"].includes(k)) { voteRoute(parseInt(k, 10) - 1); return; }
  if (S.menuOpen || S.rewards || S.roomShown || document.activeElement === nameInput) return;
  if (!keys.has(k)) {
    if (k === " " || k === "l") S.input.dod++;
    if (k === "q") S.input.swp++;
    if (k === "j") S.input.atk++;
    if (k === "k") S.input.par++;
    if (k === "g") S.input.det++;
    if (k === "v") S.input.kia++;
    if (k === "r") S.input.tl++;
    if (k === "t") S.input.tc++;
    if (k === "f") S.input.gd++;
    if (k === "h") S.input.bd++;
    if (["1", "2", "3", "4"].includes(k)) S.kamae = parseInt(k, 10) - 1;
  }
  keys.add(k);
  if ([" ", "arrowup", "arrowdown", "arrowleft", "arrowright"].includes(k)) e.preventDefault();
});
addEventListener("keyup", (e) => keys.delete(e.key.toLowerCase()));
addEventListener("blur", () => { keys.clear(); mouse.left = mouse.right = false; });
canvas.addEventListener("mousemove", (e) => { const dpr = W / innerWidth; mouse.x = e.clientX * dpr; mouse.y = e.clientY * dpr; });
canvas.addEventListener("mousedown", (e) => {
  if (S.menuOpen || S.rewards) return;
  if (e.button === 0) { mouse.left = true; S.input.atk++; }
  if (e.button === 2) { mouse.right = true; S.input.par++; }
  if (e.button === 1) { S.input.swp++; e.preventDefault(); }
  startAudio();
});
addEventListener("mouseup", (e) => { if (e.button === 0) mouse.left = false; if (e.button === 2) mouse.right = false; });
canvas.addEventListener("contextmenu", (e) => e.preventDefault());

const pad = { a: false, x: false, y: false, rb: false, lb: false };
function readInput() {
  // In the menu or choosing a reward, this duelist stands still (the fight goes on).
  if (S.menuOpen || S.rewards || S.roomShown) {
    return { t: "in", mx: 0, my: 0, ax: 0, ay: 0, p: 0, r: 0, ...counters(), ah: 0 };
  }
  let mx = 0, my = 0, ax = 0, ay = 0, parry = mouse.right || keys.has("k"), revive = keys.has("e"), held = mouse.left || keys.has("j");
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
    if (b(7) && !pad.rt) S.input.tl++;
    if (b(6) && !pad.lt) S.input.tc++;
    if (b(1) && !pad.b) S.input.gd++;
    if (b(10) && !pad.l3) S.input.bd++;
    if (b(12)) S.kamae = 0;
    if (b(15)) S.kamae = 1;
    if (b(13)) S.kamae = 2;
    if (b(14)) S.kamae = 3;
    pad.a = b(0); pad.y = b(3); pad.rb = b(5); pad.lb = b(4); pad.x = b(2);
    pad.rt = b(7); pad.lt = b(6); pad.b = b(1); pad.l3 = b(10);
    parry = parry || b(4); revive = revive || b(2); held = held || b(5);
  }
  const len = Math.hypot(mx, my);
  if (len > 1) { mx /= len; my /= len; }
  return { t: "in", mx, my, ax, ay, p: parry ? 1 : 0, r: revive ? 1 : 0, ...counters(), ah: held ? 1 : 0 };
}
function counters() {
  const i = S.input;
  return { atk: i.atk, par: i.par, dod: i.dod, swp: i.swp, det: i.det, kia: i.kia, tl: i.tl, tc: i.tc, gd: i.gd, bd: i.bd, km: S.kamae };
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
      case "Explosion":
        anim("fx_explosion", 40, 40, 9, x, y + 0.3, mag * 0.85, 0.55);
        ring(x, y, 0.2, mag * 1.1, color, 0.3); sparks(x, y, 0, 1, 14, color);
        sfx("heavy", 0.9, x, y, 0.7); sfx("crunch", 0.7, x, y, 0.8);
        if (near(x, y, mag + 1.5)) S.shake = Math.max(S.shake, 0.6);
        break;
      case "Smoke": anim("fx_smoke", 32, 32, 6, x, y + 0.2, Math.max(0.8, mag * 1.6), 0.45); sfx("dodge", 0.4, x, y, 0.8); break;
      case "Heal": anim("fx_aura", 25, 24, 5, x, y + 0.4, 1 + mag, 0.5, color); break;
      case "HealPulse": ring(x, y, 0.4, mag, color, 0.6); break;
      case "Phase": ring(x, y, 0.2, mag * 1.6, color, 0.25); sfx("dodge", 0.4, x, y, 1.5); break;
      case "Enrage": ring(x, y, 0.3, 2.4, color, 0.45); sparks(x, y, 0, 1, 16, color); sfx("heavy", 0.7, x, y, 0.6); break;
      case "Drain": ring(x, y, 1.0, 0.2, color, 0.35); break;
      case "SlamImpact": ring(x, y, 0.2, mag, "#ffffff", 0.3); sfx("heavy", 0.7, x, y, 1.2); if (near(x, y, 4)) S.shake = Math.max(S.shake, 0.5); break;
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
/** A pixel-art effect strip (explosion, smoke, aura) played once. scale multiplies the frame's pixels. */
function anim(name, fw, fh, frames, x, y, scale, life, tint) {
  S.anims.push({ name, fw, fh, frames, x, y, scale, life, age: 0, tint });
  trim(S.anims, 30);
}
function ring(x, y, r0, r1, color, life) { S.rings.push({ x, y, r0, r1, c: color || "#fff", life, age: 0 }); trim(S.rings, MAX_RINGS); }
function star(x, y, color, size) { S.rings.push({ x, y, star: true, size: size || 1, c: color, life: 0.22, age: 0 }); trim(S.rings, MAX_RINGS); }
function text(t, x, y, color, life, big) {
  // The same words at nearly the same place are one popup, not a pile.
  for (const o of S.texts) if (o.t === t && Math.abs(o.x - x) < 1.5 && Math.abs(o.y - y) < 1.5 && o.age < 0.4) return;
  S.texts.push({ t, x, y, c: color || "#fff", life: life || 0.9, age: 0, big });
  trim(S.texts, MAX_TEXTS);
}
const LOG = [];
let logStart = performance.now();
function addLog(t, color) {
  if (!t) return;
  const last = LOG[LOG.length - 1];
  if (last && last.t === t && performance.now() - last.at < 3000) { last.at = performance.now(); return; }
  const s = Math.floor((performance.now() - logStart) / 1000);
  LOG.push({ t, c: color || "#f1e8d6", at: performance.now(), stamp: String(Math.floor(s / 60)).padStart(2, "0") + ":" + String(s % 60).padStart(2, "0") });
  if (LOG.length > 40) LOG.shift();
  renderLog();
}
function renderLog() {
  const box = document.getElementById("logLines");
  if (!box) return;
  box.innerHTML = LOG.slice(-8).map((e) => '<div class="' + (performance.now() - e.at > 14000 ? "old" : "") + '"><span class="t">' + e.stamp +
    '</span><span class="d" style="background:' + e.c + '"></span><span style="color:' + e.c + '">' + escapeHtml(e.t) + "</span></div>").join("");
  document.getElementById("log").style.display = LOG.length ? "block" : "none";
}
function toast(t, color) {
  // In a run, notices are written in the log instead of crossing the screen.
  if (S.joined && S.last && ["play", "pause", "reward", "route", "equip"].includes(S.last.m)) { addLog(t, color); return; }
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
  drawZones(b, now);
  for (const pr of b.PR || []) if (pr[0] === 3 || pr[0] === 4) drawProp(pr, now);
  drawTraps(b, room, now);
  drawBlasts(b, now);

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
  const brokenL = b.LB || [], brokenS = b.SB || [];
  (room.lan || []).forEach((l, i) => { if (!brokenL.includes(i)) things.push({ y: l[1], kind: "lan", d: l }); });
  (room.sta || []).forEach((s, i) => { if (!brokenS.includes(i)) things.push({ y: s[1], kind: "sta", d: s }); });
  for (const pr of b.PR || []) if (pr[0] <= 2) things.push({ y: pr[2], kind: "prop", d: pr });
  for (const o of b.O || []) things.push({ y: o[1], kind: "off", d: o });
  things.sort((u, v) => v.y - u.y);
  for (const th of things) if (th.kind === "p" || th.kind === "e") {
    const d = th.d; const x = th.kind === "p" ? d[3] : d[2]; const y = th.kind === "p" ? d[4] : d[3];
    if (th.kind === "e" && d[16] < 0.05) continue;
    ctx.save(); ctx.translate(sx(x), sy(y - 0.22)); ctx.scale(1, 0.4);
    ctx.fillStyle = "rgba(0,0,0,.4)"; ctx.beginPath(); ctx.arc(0, 0, PPU * 0.45, 0, Math.PI * 2); ctx.fill(); ctx.restore();
  }
  for (const th of things) {
    const d = th.d;
    if (th.kind === "p") drawPlayer(d);
    else if (th.kind === "e") drawEnemy(d);
    else if (th.kind === "lan") drawLantern(d, room, now);
    else if (th.kind === "prop") drawProp(d, now);
    else if (th.kind === "off") drawOffering(d);
    else drawStatue(d, room);
  }

  // Arrows.
  for (const r of b.R || []) {
    if (r[6]) { drawThrown(r, now); continue; }
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
  drawEnvironment(b, now);
  for (const o of b.O || []) drawOfferingLabel(o);

  if (S.flash > 0) {
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = rgba(S.flashColor, S.flash * 0.5);
    ctx.fillRect(0, 0, W, H);
    S.flash = Math.max(0, S.flash - dt * 3);
  }
}

function drawThrown(r, now) {
  const e = img("art/" + r[6] + ".png");
  if (r[4]) circle(r[0], r[1], 0.3, null, rgba(ACCENT, 0.4));
  if (!e) { circle(r[0], r[1], 0.12, null, "#fff"); return; }
  const shuriken = r[6].indexOf("shuriken") >= 0;
  const fw = shuriken ? 16 : e.img.width, fh = shuriken ? 16 : e.img.height;
  const ppu = shuriken ? 22 : 18;
  const w = fw / ppu * PPU, h = fh / ppu * PPU;
  ctx.save();
  ctx.translate(sx(r[0]), sy(r[1]));
  ctx.rotate(-Math.atan2(r[3], r[2]) - (shuriken ? now / 1000 * 15.7 : 0));
  ctx.drawImage(e.img, 0, 0, fw, fh, -w / 2, -h / 2, w, h);
  ctx.restore();
}

function drawPlayer(p) {
  const alive = p[21] === 1;
  const isMe = p[0] === S.myId;
  drawFighter(p[2], p[3], p[4], -0.228, 1, p[7], alive ? p[8] : 3, p[9], p[15], p[16], alive ? Math.max(0.35, p[23]) : 0.9);
  if (alive && p[13]) drawWeapon(p[14], p[3], p[4], p[10], p[11], p[12], Math.max(0.35, p[23]));
  if (isMe) circle(p[3], p[4] - 0.2, 0.55, rgba(GOLD, 0.35), null, 2);
}

function fogAlpha(x, y) {
  const env = S.last && S.last.env, me = myPlayer();
  if (!env || !(env[5] > 0) || !me) return 1;
  return Math.max(0, Math.min(1, 1 - (Math.hypot(x - me.x, y - me.y) - env[5]) / 2));
}
function drawEnemy(e) {
  const fog = fogAlpha(e[2], e[3]);
  if (fog <= 0.01) return;
  ctx.globalAlpha = fog;
  if (e[39]) circle(e[2], e[3], 0.55, null, "rgba(160,215,255,.45)");
  ctx.globalAlpha = 1;
  drawFighter(e[1], e[2], e[3], e[8], e[7], e[4], e[5], e[6], e[40] ? 0.4 : e[14], e[40] ? DANGER : e[15], e[16] * fog);
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
    ctx.drawImage(e.img, 48, row * 16, 32, 32, sx(s[0]) - PPU, sy(s[1] + 2), PPU * 2, PPU * 2);
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
        // Retracted spikes: a dim grate sunk in the floor (bright, it looks like a block of wall).
        ctx.globalAlpha = state === 2 ? 1 : 0.45;
        const g = state === 2 ? PPU : PPU * 0.8;
        if (dungeon) ctx.drawImage(dungeon.img, state === 2 ? 64 : 80, 16, 16, 16, sx(x) - g / 2, sy(y) - g / 2, g, g);
        ctx.globalAlpha = 1;
        if (state === 1 && Math.floor(now / 100) % 2 === 0) { ctx.fillStyle = "rgba(255,70,50,.35)"; ctx.fillRect(sx(x - 0.5), sy(y + 0.5), PPU, PPU); }
      }
    } else if (type === 1) {
      // Ember plates pave the lane the flame sweeps: dim at rest, blinking red on warning, hot when firing.
      const plates = Math.max(1, Math.round(len));
      const blink = state === 1 && Math.floor(now / 50) % 2 === 0;
      for (let k = 0; k < plates; k++) {
        const px = ox + dx * (0.6 + k * (len - 0.2) / plates), py = oy + dy * (0.6 + k * (len - 0.2) / plates);
        const sz = PPU * 0.85;
        ctx.globalAlpha = state === 0 ? 0.75 : 1;
        if (dungeon) ctx.drawImage(dungeon.img, 16, 16, 16, 16, sx(px) - sz / 2, sy(py) - sz / 2, sz, sz);
        ctx.globalAlpha = 1;
        if (state === 2 || blink) { ctx.fillStyle = state === 2 ? "rgba(255,190,110,.35)" : "rgba(255,70,50,.4)"; ctx.fillRect(sx(px) - sz / 2, sy(py) - sz / 2, sz, sz); }
      }
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

/** Bombs and talismans: a circle filling as the fuse burns, flickering near the end. */
function drawBlasts(snap, now) {
  for (const bl of snap.B || []) {
    const [x, y, r, t, kind, color] = bl;
    const late = t > 0.66, blink = late && Math.floor(now / 42) % 2 === 0 ? 1 : 0.6;
    circle(x, y, r * (0.15 + 0.85 * t), null, rgba(color, (0.18 + 0.3 * t) * blink));
    circle(x, y, r, rgba(late && blink === 1 ? "#ffffff" : color, 0.5 + 0.5 * t), null, Math.max(2, PPU * 0.06));
    const icon = kind === 0 ? "item_bomb" : kind === 1 ? "item_scroll_fire" : null;
    const e = icon && img("art/" + icon + ".png");
    if (e) {
      const bob = kind === 0 ? 0.12 * Math.abs(Math.sin(now / 100)) : 0;
      const s = (kind === 0 ? 1.1 : 0.9) * (1 + (late ? 0.15 * blink : 0));
      const w = e.img.width / 16 * PPU * s, h = e.img.height / 16 * PPU * s;
      ctx.drawImage(e.img, sx(x) - w / 2, sy(y + 0.15 + bob) - h / 2, w, h);
    }
  }
}

const RING_COLORS = [ACCENT, DANGER, GOLD, ACCENT, "#b07cff", "#7bc950", "#ffffff"];
const RING_DASH = [null, null, [10, 8], null, [2, 9], [4, 4], [26, 10]];
function ringVisible(e) {
  const me = myPlayer();
  if (!me) return true;
  const raw = me.raw;
  if (raw[50] === 1 || raw[57] === 1) return false;
  const env = S.last && S.last.env;
  if (env && env[4] > 0 && Math.hypot(e[2] - me.x, e[3] - me.y) > env[4]) return false;
  if (e[35] === 1 && Math.hypot(e[2] - me.x, e[3] - me.y) > 3) return false;
  return true;
}
function drawEnemyOverlay(e) {
  const [x, y] = [e[2], e[3]];
  if (e[29] && !e[32] && ringVisible(e)) {
    const kind = e[33] || 0;
    const col = RING_COLORS[kind] || (e[28] ? DANGER : ACCENT);
    const me = myPlayer();
    const size = e[30] * (me && me.raw[51] ? me.raw[51] : 1);
    ctx.save();
    if (RING_DASH[kind]) ctx.setLineDash(RING_DASH[kind].map((d) => d * PPU / 48));
    if (e[29] === 1) {
      const s = size * (3.4 + (1 - 3.4) * e[31]);
      circle(x, y, s / 2, rgba(col, 0.15 + 0.8 * e[31]), null, Math.max(2, PPU * 0.07));
      circle(x, y, size / 2, rgba(col, 0.35), null, 2);
      if (kind === 3) circle(x, y, size * 0.3, rgba(col, 0.6), null, 2);
    } else {
      const k = e[31];
      circle(x, y, size * 0.95, null, rgba(col, 0.45 * (1 - k * 0.5)));
      circle(x, y, size * (1.18 - 0.18 * k) / 2, "#ffffff", null, Math.max(3, PPU * 0.09));
    }
    ctx.restore();
  }
  // Seals: small gold marks over the head.
  if (e[34] > 0 && !e[32]) {
    for (let i = 0; i < e[34]; i++) circle(x - 0.36 + i * 0.18, y + e[8] + e[7] * 1.05 + 0.55, 0.06, null, GOLD);
  }
  if (e[32] || e[16] < 0.05 || e[5] === 3 || fogAlpha(x, y) < 0.3) return;
  const top = y + e[8] + e[7] * 1.05 + 0.25;
  if (e[38]) {
    ctx.font = Math.round(PPU * 0.24) + "px SpectralBold, serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "#9bd39b"; ctx.fillText("RONIN ALIADO", sx(x), sy(top + 0.3));
  }
  drawBar(x, top, 1.3, e[17], "#c8553d");
  drawBar(x, top - 0.14, 1.3, e[18], e[20] ? GOLD : "#e9a35a");
  if (e[19]) circle(x, y, 1.4 * e[7], rgba(GOLD, 0.3), null, 2);
  if (e[21]) circle(x + 0.85, top, 0.12, GOLD, null, 2);
  if (e[36]) {
    ctx.font = Math.round(PPU * 0.24) + "px SpectralBold, serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "rgba(0,0,0,.7)"; ctx.fillText(e[36], sx(x) + 1, sy(top + 0.3) + 1);
    ctx.fillStyle = GOLD; ctx.fillText(e[36], sx(x), sy(top + 0.3));
  }
  if (e[37]) {
    ctx.font = Math.round(PPU * 0.22) + "px SpectralBold, serif";
    ctx.textAlign = "center";
    ctx.fillStyle = "#a8d8c8"; ctx.fillText("sem fôlego", sx(x), sy(y - 0.5));
  }
  if (e[20]) circle(x, y, 0.9 + 0.08 * Math.sin(performance.now() / 80), GOLD, null, 3);
}

function dotted(x0, y0, x1, y1, color, size) {
  const n = Math.max(2, Math.round(Math.hypot(x1 - x0, y1 - y0) / 0.32));
  ctx.fillStyle = color;
  for (let i = 0; i <= n; i++) {
    const t = i / n;
    ctx.fillRect(sx(x0 + (x1 - x0) * t) - size / 2, sy(y0 + (y1 - y0) * t) - size / 2, size, size);
  }
}
function drawPlayerOverlay(p, snap) {
  const isMe = p[0] === S.myId;
  const alive = p[21] === 1;
  if (isMe && p[46] === 1) {
    const a = p[47] * Math.PI / 180, lx = Math.cos(a) * 2.6, ly = Math.sin(a) * 2.6;
    dotted(p[48] - lx, p[49] - ly, p[48] + lx, p[49] + ly, GOLD, Math.max(4, PPU * 0.12));
    dotted(p[3], p[4], p[3] + p[5] * 3.2, p[4] + p[6] * 3.2, "rgba(241,232,214,.8)", Math.max(3, PPU * 0.08));
  }
  if (p[45] === 1) circle(p[3], p[4], 0.9 + 0.08 * Math.sin(performance.now() / 70), null, "rgba(215,38,61,.25)");
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
  for (let i = S.anims.length - 1; i >= 0; i--) {
    const an = S.anims[i];
    an.age += dt;
    if (an.age >= an.life) { S.anims.splice(i, 1); continue; }
    const e = img("art/" + an.name + ".png");
    if (!e) continue;
    const f = Math.min(an.frames - 1, Math.floor(an.age / an.life * an.frames));
    const w = an.fw / 16 * PPU * an.scale, h = an.fh / 16 * PPU * an.scale;
    if (an.tint) {
      // Tinted strips (healing aura) are drawn through the tint canvas.
      tintCtx.clearRect(0, 0, tint.width, tint.height);
      tintCtx.globalCompositeOperation = "source-over";
      tintCtx.drawImage(e.img, f * an.fw, 0, an.fw, an.fh, 0, 0, an.fw, an.fh);
      tintCtx.globalCompositeOperation = "multiply";
      tintCtx.fillStyle = an.tint; tintCtx.fillRect(0, 0, an.fw, an.fh);
      tintCtx.globalCompositeOperation = "destination-in";
      tintCtx.drawImage(e.img, f * an.fw, 0, an.fw, an.fh, 0, 0, an.fw, an.fh);
      ctx.drawImage(tint, 0, 0, an.fw, an.fh, sx(an.x) - w / 2, sy(an.y) - h / 2, w, h);
    } else {
      ctx.drawImage(e.img, f * an.fw, 0, an.fw, an.fh, sx(an.x) - w / 2, sy(an.y) - h / 2, w, h);
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
    $("breathFill").style.width = ((me[34] || 0) * 100) + "%";
    $("breathFill").classList.toggle("exhausted", me[35] === 1);
    $("shuraFill").style.width = ((me[36] || 0) * 100) + "%";
    $("shuraName").textContent = me[37] > 0 ? "· " + SHURA_NAMES[me[37]] : "";
    $("kamae").textContent = KAMAE_KANJI[me[38] || 0] + "  " + KAMAE_NAMES[me[38] || 0];
    const wm = me[43] || 0;
    const status = [];
    if (me[52] > 0) status.push("CARGA " + me[52]);
    if (me[55]) status.push(me[55] + " (" + me[56] + ")");
    $("wounds").textContent = status.join("  ·  ");
    $("woundTag").textContent = wm ? "FERIDO" : "";
    drawPortrait(me);
    drawStances(me);
    drawDoll(wm);
    drawBelt(me);
    // Mushin: the interface fades away.
    document.getElementById("hud").classList.toggle("mushin", me[44] === 1);
  }
  // Iai standoff.
  const so = s.so;
  $("standoff").style.display = so ? "block" : "none";
  if (so) {
    $("standoffText").textContent = so[1] === 1 ? "AGORA!" : "Segure o ataque. Solte só quando ele AVANÇAR.";
    $("standoffText").classList.toggle("now", so[1] === 1);
  }
  $("room").textContent = s.room || "";
  $("wave").textContent = s.wave || "";
  const env = s.env;
  if (env) {
    const parts = [];
    if (env[12] !== 0) parts.push(env[13]);
    parts.push(env[9]);
    if (env[0] !== 0) parts.push(env[8]);
    if (env[11] > 1) parts.push("Kalpa " + ROMAN[env[11] - 1]);
    $("envLine").textContent = parts.join("  ·  ");
    $("ruleLine").textContent = env[10];
  } else { $("envLine").textContent = ""; $("ruleLine").textContent = ""; }
  const ch = s.ch;
  $("challenge").textContent = ch ? (ch[1] ? "DESAFIO PERDIDO" : "DESAFIO: " + ch[0] + (ch[2] > 0 ? "  ·  " + Math.ceil(ch[2]) + " s" : "")) : "";
  $("challenge").classList.toggle("failed", !!(ch && ch[1]));
  const enc = s.enc;
  $("encBanner").style.display = enc ? "block" : "none";
  if (enc) { $("encTitle").textContent = enc[0]; $("encText").textContent = enc[1]; }
  if (S.route && s.rt) {
    $("routeTimer").textContent = "Cada um vota (teclas 1–" + S.route.o.length + " ou clique). Empate: decide o anfitrião · " + s.rt[0] + " s";
    S.route.o.forEach((_, i) => { const v = document.getElementById("rv" + i); if (v) v.textContent = "●".repeat(s.rt[i + 1] || 0); });
  }
  // The scroll closes when the host says so ("routed"), or if play has clearly resumed.
  if (S.route && s.m === "play" && performance.now() - S.routeAt > 2000) hideRoute();
  let f = "";
  const fl = me ? me[29] || 0 : 0, fp = me ? me[30] || 0 : 0, st = me ? me[31] || 0 : 0;
  for (let i = 0; i < 3; i++) f += '<b class="' + (fl > i ? "on" : "") + '"></b>';
  $("flow").innerHTML = f;
  $("flowFill").style.width = (fp * 100) + "%";
  $("streak").textContent = st >= 2 ? "SEQUÊNCIA x" + st : "";

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
  if (s.m === "lobby" || s.m === "over") { title = ""; }
  else if (s.m === "pause") { title = "Pausa"; sub = "O anfitrião pausou o duelo"; }
  else if (s.m === "reward") { title = S.rewards ? "" : "Sala vencida"; sub = "Aguardando o grupo escolher as recompensas · " + others + " duelistas"; }
  else if (s.m === "equip") { title = "Equipamento"; sub = "O anfitrião está ajustando os itens dele"; }
  else if (s.m === "over") { title = "A morte ensina"; sub = "A run acabou. Aguardando o anfitrião."; }
  else if (me && me[21] !== 1) { title = "Você caiu"; sub = "Um aliado pode te reviver ficando sobre você e segurando E"; }
  $("center").innerHTML = title ? "<h2>" + title + "</h2><p>" + escapeHtml(sub) + "</p>" : "";
  $("center").style.display = title ? "block" : "none";
}
const KAMAE_NAMES = ["JŌDAN", "CHŪDAN", "GEDAN", "WAKI"];
const KAMAE_KANJI = ["上段", "中段", "下段", "脇構"];
// Blade angle of each guard (degrees clockwise from facing right) and where the hands hold it.
const KAMAE_ANGLE = [-112, -18, 38, 158];
const KAMAE_HANDS = [[0.48, 0.2], [0.66, 0.58], [0.62, 0.66], [0.34, 0.66]];

// ── The HUD's pixel art (the same grids the game paints) ──────────────────────────
const HUD = {
  gourd: [".....ccc.....", "....cCCCc....", ".....ooo.....", "....obbbo....", "...obhbbbo...", "...obhbbbo...", "...obbbbso...", "....obbso....",
    ".....rrr.....", "....rRrRr....", "....obbbo....", "...obhbbbo...", "..obhbbbbbo..", ".obhhbbbbbso.", ".obhbbbbbbso.", ".obbbbbbbsso.",
    "..obbbbbsso..", "...osssssoo..", "....ooooo...."],
  gourdFull: { o: "#3a1f0e", b: "#d9963a", h: "#f6d38a", s: "#a8601f", r: "#b3261e", R: "#e24b3b", c: "#6b4a2b", C: "#8a6a44" },
  gourdEmpty: { o: "#211b17", b: "#5e5146", h: "#7a6c5e", s: "#3d342c", r: "#5a2a26", R: "#6e3530", c: "#6b4a2b", C: "#8a6a44" },
  bandage: ["....oooooo....", "..oowwWWwwoo..", ".owWWwwwwWWwo.", "owWwwooooowWwo", "owWwo....owWwo", "owWwo....owWwo", "owWwwooooowWwo",
    ".owWWwwwwWWwo.", "..oowwwwwwoorr", "....oooooo.rRr", "...........rr."],
  bandagePal: { o: "#5e5146", w: "#e8dfcc", W: "#ffffff", r: "#b3261e", R: "#e24b3b" },
  ofuda: ["oooooooo", "oppppppo", "oprrrrpo", "opppppPo", "oprprrpo", "oppprppo", "oprrrppo", "opppppPo", "opprrppo", "oprpprpo", "opprrppo",
    "oppppPpo", "opppppPo", "oooooooo"],
  ofudaPal: { o: "#8c7a5c", p: "#f1e8d6", P: "#d9ccb0", r: "#b3261e" },
  coin: ["...oooooo...", "..oggGGggo..", ".ogGGggggso.", "ogGgoooogsso", "ogGgo..ogsso", "oggo....oggo", "oggo....oggo", "osgo....osgo",
    "osggo..ogsgo", ".osgooooggo.", "..ossgggso..", "...oooooo..."],
  coinPal: { o: "#5c3a0a", g: "#d9a13a", G: "#fbe29a", s: "#a8701f" },
  bell: [".....oo.....", "....oyyo....", "....oyyo....", "...oyYyyo...", "..oyYyyyyo..", "..oyYyyyyo..", "..oyYyyyso..", ".oyYyyyysso.",
    "oyYyyyyyysso", "oooooooooooo", ".....oo....."],
  bellPal: { o: "#3d2a0e", y: "#c9902e", Y: "#f2c14e", s: "#8a5a1a" },
  hook: ["..ll..ll....", ".l..ll..l...", "..ll..ll.l..", ".........l..", "..........l.", "...oo.....l.", "..oSSo....l.", ".oSo.oo..l..",
    ".oSo..oSSo..", "..oSo.oSo...", "...oSSSo....", "....ooo....."],
  hookPal: { o: "#2a2a30", S: "#c8c2b8", l: "#8c8597" },
  oil: ["....cc....", "....oo....", "...oggo...", "...oggo...", "..oggGgo..", ".oyyyyyyo.", "oyYyyyyyso", "oyYyyyyyso", "oyyyyyysso",
    ".osssssso.", "..oooooo.."],
  oilPal: { o: "#2a1a10", c: "#8a6a44", g: "#b8c4c8", G: "#ffffff", y: "#e08a2b", Y: "#ffd27a", s: "#a8501a" },
  doll: ["....HHHH....", "...HHHHHH...", "...HEHHEH...", "...HHHHHH...", "....HHHH....", ".....HH.....", "..AATTTTAA..", ".AAATTTTAAA.",
    ".AA.TTTT.AA.", ".AA.TTTT.AA.", ".AA.TTTT.AA.", ".AA.TTTT.AA.", "AA..TTTT..AA", "AA..TTTT..AA", "....LLLL....", "...LL..LL...",
    "...LL..LL...", "...LL..LL...", "...LL..LL...", "...LL..LL...", "..LLL..LLL.."],
};
const gridCache = {};
function gridCanvas(key, rows, pal) {
  if (gridCache[key]) return gridCache[key];
  const w = Math.max(...rows.map((r) => r.length)), h = rows.length;
  const c = document.createElement("canvas"); c.width = w; c.height = h;
  const g = c.getContext("2d");
  rows.forEach((row, y) => { for (let x = 0; x < row.length; x++) { const col = pal[row[x]]; if (col) { g.fillStyle = col; g.fillRect(x, y, 1, 1); } } });
  return (gridCache[key] = c);
}
function blit(canvasId, src, alpha) {
  const cv = document.getElementById(canvasId);
  if (!cv || !src) return;
  const g = cv.getContext("2d");
  g.imageSmoothingEnabled = false;
  g.clearRect(0, 0, cv.width, cv.height);
  const k = Math.min(cv.width / src.width, cv.height / src.height);
  g.globalAlpha = alpha == null ? 1 : alpha;
  g.drawImage(src, (cv.width - src.width * k) / 2, (cv.height - src.height * k) / 2, src.width * k, src.height * k);
  g.globalAlpha = 1;
}
/** A blade-up weapon image held at a grip, pointing at a GUI angle (clockwise from right). */
function drawHeld(g, weaponArt, gx, gy, angle, px) {
  const e = weaponArt && img("art/" + weaponArt + ".png");
  if (!e) return;
  const w = e.img.width * px, h = e.img.height * px;
  g.save(); g.translate(gx, gy); g.rotate((angle + 90) * Math.PI / 180);
  g.drawImage(e.img, -w / 2, -h * 0.85, w, h);
  g.restore();
}
function drawPortrait(me) {
  const cv = document.getElementById("portrait");
  if (!cv) return;
  const g = cv.getContext("2d");
  g.imageSmoothingEnabled = false;
  g.clearRect(0, 0, cv.width, cv.height);
  const art = me[2], st = me[59] || 0, moving = me[60] === 1, k = me[38] || 0;
  const attacking = st === 1 || st === 4 || st === 9, dead = st === 10 || me[21] !== 1;
  const sheet = dead ? "dead" : attacking ? "attack" : (moving || st === 5) ? "walk" : "idle";
  const e = img("art/" + art + "_" + sheet + ".png"), ref = img("art/" + art + "_idle.png");
  if (!e) return;
  const bottom = ref ? ref.bottom : 2;
  const px = 0.5 * cv.height / 13;
  const size = 16 * px;
  const x0 = (cv.width - size) / 2 + size * 0.06, y0 = cv.height - 22 - size + bottom * px + (st === 0 ? Math.sin(performance.now() / 420) * 3 : 0);
  const row = sheet === "walk" ? Math.floor(performance.now() / (st === 5 ? 70 : 125)) % 4 : 0;
  const col = dead ? 0 : 3;
  let angle = attacking ? -80 + 120 * Math.abs(((performance.now() / 160) % 2) - 1) : KAMAE_ANGLE[k];
  if (st === 2 || st === 3) angle = -60;
  const hands = attacking ? [0.7, 0.5] : KAMAE_HANDS[k];
  const gx = x0 + hands[0] * size, gy = y0 + hands[1] * size;
  const blade = !dead && st !== 11;
  const behind = k === 3 && !attacking;
  g.fillStyle = "rgba(0,0,0,.5)"; g.beginPath(); g.ellipse(cv.width / 2, cv.height - 14, size * 0.28, size * 0.06, 0, 0, Math.PI * 2); g.fill();
  if (blade && behind) drawHeld(g, me[14], gx, gy, angle, px);
  if (me[45] === 1 || st === 7) g.filter = "sepia(1) saturate(6) hue-rotate(-30deg)";
  g.drawImage(e.img, col * 16, row * 16, 16, 16, x0, y0, size, size);
  g.filter = "none";
  if (blade && !behind) drawHeld(g, me[14], gx, gy, angle, px);
  if (st === 11) g.drawImage(gridCanvas("gf", HUD.gourd, HUD.gourdFull), gx - 10, y0 + size * 0.2, 26, 38);
  cv.classList.toggle("flow", (me[29] || 0) > 0);
  cv.classList.toggle("asura", me[45] === 1);
}
function drawStances(me) {
  const cv = document.getElementById("stances");
  if (!cv) return;
  const g = cv.getContext("2d");
  g.imageSmoothingEnabled = false;
  g.clearRect(0, 0, cv.width, cv.height);
  const ref = img("art/" + me[2] + "_idle.png");
  const cur = me[38] || 0, cw = cv.width / 4;
  for (let i = 0; i < 4; i++) {
    const on = i === cur, col = on ? "#f2c14e" : "rgba(185,172,151,.45)";
    const x = i * cw + (cw - 44) / 2, y = 2;
    if (on) { g.fillStyle = "rgba(242,193,78,.18)"; g.beginPath(); g.ellipse(x + 22, y + 24, 26, 24, 0, 0, Math.PI * 2); g.fill(); }
    if (ref) {
      // A silhouette of the duelist in the guard's colour.
      tintCtx.clearRect(0, 0, 16, 16);
      tintCtx.globalCompositeOperation = "source-over";
      tintCtx.drawImage(ref.img, 48, 0, 16, 16, 0, 0, 16, 16);
      tintCtx.globalCompositeOperation = "source-in";
      tintCtx.fillStyle = col; tintCtx.fillRect(0, 0, 16, 16);
      g.drawImage(tint, 0, 0, 16, 16, x, y, 44, 44);
    }
    const hx = x + KAMAE_HANDS[i][0] * 44, hy = y + KAMAE_HANDS[i][1] * 44, a = KAMAE_ANGLE[i] * Math.PI / 180;
    g.strokeStyle = col; g.lineWidth = 3;
    g.beginPath(); g.moveTo(hx - 4 * Math.cos(a), hy - 4 * Math.sin(a)); g.lineTo(hx + 24 * Math.cos(a), hy + 24 * Math.sin(a)); g.stroke();
    g.fillStyle = col; g.font = "bold 12px serif"; g.textAlign = "center"; g.fillText(String(i + 1), x + 22, y + 60);
  }
}
function drawDoll(mask) {
  const cv = document.getElementById("doll");
  if (!cv) return;
  const g = cv.getContext("2d");
  g.imageSmoothingEnabled = false;
  g.clearRect(0, 0, cv.width, cv.height);
  const pulse = 0.75 + 0.25 * Math.sin(performance.now() / 160);
  const regions = { H: null, T: 4, A: 1, L: 2, E: 8 };
  for (const [ch, bit] of Object.entries(regions)) {
    const hurt = bit != null && (mask & bit);
    const col = hurt ? "rgba(255,90,60," + pulse + ")" : ch === "E" ? "#1a1a1f" : "rgba(185,172,151,.45)";
    const rows = HUD.doll.map((r) => r.replace(new RegExp("[^" + ch + "]", "g"), "."));
    const c = gridCanvas("doll" + ch + col, rows, { [ch]: col });
    const k = Math.min(cv.width / c.width, cv.height / c.height);
    g.drawImage(c, (cv.width - c.width * k) / 2, 0, c.width * k, c.height * k);
  }
}
function weaponIcon(id, art, lit) {
  const cv = document.getElementById(id);
  if (!cv) return;
  const g = cv.getContext("2d");
  g.imageSmoothingEnabled = false;
  g.clearRect(0, 0, cv.width, cv.height);
  const e = art && img("art/" + art + ".png");
  if (!e) return;
  const k = Math.min(cv.width / e.img.height, cv.height / e.img.width) * 0.95;
  g.save(); g.globalAlpha = lit ? 1 : 0.55; g.translate(cv.width / 2, cv.height / 2); g.rotate(Math.PI / 2);
  g.drawImage(e.img, -e.img.width * k / 2, -e.img.height * k / 2, e.img.width * k, e.img.height * k);
  g.restore();
}
function drawBelt(me) {
  weaponIcon("w0", me[14], true);
  weaponIcon("w1", me[58], false);
  $("w0n").textContent = me[24];
  $("w1n").textContent = me[25];
  const ch = me[52] || 0;
  $("charge").innerHTML = ch > 0 ? "●".repeat(ch) : "";
  // One hyōtan per sip.
  const gv = document.getElementById("gourds");
  if (gv) {
    const g = gv.getContext("2d"); g.imageSmoothingEnabled = false; g.clearRect(0, 0, gv.width, gv.height);
    const max = me[40] || 0, sips = me[39] || 0;
    const full = gridCanvas("gf", HUD.gourd, HUD.gourdFull), empty = gridCanvas("ge", HUD.gourd, HUD.gourdEmpty);
    for (let i = 0; i < max; i++) g.drawImage(i < sips ? full : empty, 4 + i * 34, 2, 32, 48);
  }
  blit("bandage", gridCanvas("band", HUD.bandage, HUD.bandagePal), (me[53] || 0) > 0 ? 1 : 0.35);
  $("bandN").textContent = "× " + (me[53] || 0);
  const tool = me[41] || 0;
  let icon = null;
  if (tool === 0) { const e = img("art/fx_shuriken.png"); if (e) { const c = document.createElement("canvas"); c.width = 16; c.height = 16; c.getContext("2d").drawImage(e.img, 0, 0, 16, 16, 0, 0, 16, 16); icon = c; } }
  else if (tool === 1) { const e = img("art/fx_smoke.png"); if (e) { const c = document.createElement("canvas"); c.width = 32; c.height = 32; c.getContext("2d").drawImage(e.img, 64, 0, 32, 32, 0, 0, 32, 32); icon = c; } }
  else if (tool === 2) icon = gridCanvas("bell", HUD.bell, HUD.bellPal);
  else if (tool === 3) { const e = img("art/item_bomb.png"); icon = e ? e.img : null; }
  else if (tool === 4) icon = gridCanvas("oil", HUD.oil, HUD.oilPal);
  else icon = gridCanvas("hook", HUD.hook, HUD.hookPal);
  blit("toolIcon", icon, 1);
  blit("ofuda", gridCanvas("ofuda", HUD.ofuda, HUD.ofudaPal), (me[42] || 0) > 0 ? 1 : 0.35);
  $("tool").textContent = TOOL_NAMES[tool] + "  · × " + (me[42] || 0);
  blit("coin", gridCanvas("coin", HUD.coin, HUD.coinPal), 1);
  $("purse").textContent = String(me[54] || 0);
}
const ROMAN = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];

// ── The road: route map, zones, props, offerings, light and weather ────────────────
function showRoute(m) {
  S.route = m;
  S.routeAt = performance.now();
  const box = document.getElementById("routeCards");
  box.innerHTML = "";
  m.o.forEach((n, i) => {
    const d = document.createElement("div");
    d.className = "door";
    d.style.borderColor = n[4];
    d.innerHTML = '<span class="key">' + (i + 1) + '</span><div class="glyph" style="color:' + (n[0] === 0 ? "#1c1a20" : n[4]) + '">' + escapeHtml(n[1]) + '</div>' +
      '<div class="nm">' + escapeHtml(n[2]) + '</div><div class="g">' + escapeHtml(n[3]) + '</div><div class="votes" id="rv' + i + '"></div>';
    d.onclick = () => voteRoute(i);
    box.appendChild(d);
  });
  const ahead = document.getElementById("routeAhead");
  ahead.innerHTML = (m.ahead || []).map((layer) => '<div class="layer">' + layer.map((n) =>
    '<span style="border-color:' + n[4] + '">' + escapeHtml(n[2].split(" · ")[0]) + "</span>").join("") + "</div>").join("");
  document.getElementById("route").hidden = false;
  hideRewards();
  closeMenu();
}
function voteRoute(i) {
  if (!S.route || i < 0 || i >= S.route.o.length) return;
  send({ t: "vote", i });
  document.querySelectorAll("#routeCards .door").forEach((d, k) => d.classList.toggle("chosen", k === i));
}
function hideRoute() { S.route = null; document.getElementById("route").hidden = true; }

const ZONE_COLORS = ["#b8d46a", "#8e0f1e", "#8e44ad", "#ff8c2b", "#cfefff"];
function drawZones(snap, now) {
  for (const z of snap.Z || []) {
    const [k, x, y, r] = z;
    const flick = k === 3 ? 0.25 + 0.1 * Math.sin(now / 90 + x) : 0;
    const a = k === 1 ? 0.7 : k === 2 ? 0.3 : k === 4 ? 0.3 : 0.38;
    const g = ctx.createRadialGradient(sx(x), sy(y), 0, sx(x), sy(y), r * PPU * 1.2);
    g.addColorStop(0, rgba(ZONE_COLORS[k], a + flick)); g.addColorStop(1, rgba(ZONE_COLORS[k], 0));
    ctx.fillStyle = g;
    ctx.fillRect(sx(x) - r * PPU * 1.2, sy(y) - r * PPU * 1.2, r * PPU * 2.4, r * PPU * 2.4);
  }
}
function drawProp(pr, now) {
  const [k, x, y, ang] = pr;
  ctx.save();
  if (k === 0) {
    // Bamboo: three stalks with knots.
    for (let i = 0; i < 3; i++) {
      const ox = (i - 1) * 0.28, h = 1.3 + 0.25 * (i % 2);
      ctx.fillStyle = "#5c8a34"; ctx.fillRect(sx(x + ox) - PPU * 0.07, sy(y - 0.45 + h), PPU * 0.14, h * PPU);
      ctx.fillStyle = "#a7d46a";
      for (let j = 1; j < 4; j++) ctx.fillRect(sx(x + ox) - PPU * 0.08, sy(y - 0.45 + j * h / 4), PPU * 0.16, PPU * 0.04);
    }
  } else if (k === 1) {
    ctx.fillStyle = "rgba(241,232,214,.92)"; ctx.fillRect(sx(x - 0.5), sy(y + 0.85), PPU, PPU * 1.35);
    ctx.strokeStyle = "#6b4a2b"; ctx.lineWidth = Math.max(1, PPU * 0.05);
    ctx.strokeRect(sx(x - 0.5), sy(y + 0.85), PPU, PPU * 1.35);
    for (let j = 1; j < 4; j++) { ctx.beginPath(); ctx.moveTo(sx(x - 0.5), sy(y + 0.85 - j * 0.34)); ctx.lineTo(sx(x + 0.5), sy(y + 0.85 - j * 0.34)); ctx.stroke(); }
    ctx.beginPath(); ctx.moveTo(sx(x), sy(y + 0.85)); ctx.lineTo(sx(x), sy(y - 0.5)); ctx.stroke();
  } else if (k === 2) {
    const g = img("art/tiles_dungeon.png");
    const glow = ctx.createRadialGradient(sx(x), sy(y + 0.3), 0, sx(x), sy(y + 0.3), PPU);
    glow.addColorStop(0, "rgba(217,166,255,.6)"); glow.addColorStop(1, "rgba(217,166,255,0)");
    ctx.fillStyle = glow; ctx.fillRect(sx(x) - PPU, sy(y + 0.3) - PPU, PPU * 2, PPU * 2);
    if (g) ctx.drawImage(g.img, 32, 32, 16, 16, sx(x) - PPU / 2, sy(y + 0.6), PPU, PPU);
  } else if (k === 3) {
    ctx.fillStyle = "#050409"; ctx.fillRect(sx(x - 0.5), sy(y + 0.5), PPU, PPU);
    ctx.fillStyle = "#332c44"; ctx.fillRect(sx(x - 0.5), sy(y + 0.5), PPU, PPU * 0.12);
  } else if (k === 4) {
    ctx.translate(sx(x), sy(y)); ctx.rotate(-ang * Math.PI / 180);
    ctx.fillStyle = "#6e9c40"; ctx.fillRect(-PPU * 1.25, -PPU * 0.08, PPU * 2.5, PPU * 0.16);
  }
  ctx.restore();
}
function drawOffering(o) {
  const [x, y, sheet, tx, ty, tw, th, ch] = o;
  const exit = o[13] === 1;
  ctx.save();
  ctx.globalAlpha = o[12] === 1 && !exit ? 0.45 : 1;
  circle(x, y, exit ? 1.6 : 0.8, rgba(o[14], 0.55), null, 2);
  if (ch) drawFighter(ch, x, y, -0.228, 1, 0, 0, 0, 0, "#000000", ctx.globalAlpha);
  else if (sheet) {
    const g = img("art/" + sheet + ".png");
    if (g) {
      const w = tw * PPU, h = th * PPU;
      ctx.drawImage(g.img, tx * 16, ty * 16, tw * 16, th * 16, sx(x) - w / 2, sy(y - (exit ? 0.6 : 0.3)) - h, w, h);
    }
  }
  ctx.restore();
}
function drawOfferingLabel(o) {
  const [x, y] = o;
  const exit = o[13] === 1, me = myPlayer();
  const used = (o[15] || []).includes(S.myId) && o[16] !== 1;
  const off = o[12] === 1 || used;
  const ly = y + (exit ? 2.6 : 1.9);
  ctx.font = Math.round(PPU * 0.3) + "px SpectralBold, serif";
  ctx.textAlign = "center";
  const title = o[8] + (o[10] > 0 ? "  ·  " + o[10] + " Mon" : "");
  ctx.fillStyle = "rgba(0,0,0,.75)"; ctx.fillText(title, sx(x) + 2, sy(ly) + 2);
  ctx.fillStyle = off ? "rgba(200,190,180,.55)" : o[14]; ctx.fillText(title, sx(x), sy(ly));
  const near = me && Math.hypot(me.x - x, me.y - y) <= (exit ? 1.8 : 1.1);
  if (near && o[9]) {
    ctx.font = Math.round(PPU * 0.24) + "px Spectral, serif";
    const text = o[9] + (!off && !exit ? "  (segure E)" : "");
    const w = Math.min(PPU * 9, ctx.measureText(text).width + PPU * 0.6);
    ctx.fillStyle = "rgba(14,13,19,.85)"; ctx.fillRect(sx(x) - w / 2, sy(ly + 0.75), w, PPU * 0.5);
    ctx.fillStyle = "#ede6d6"; ctx.fillText(text, sx(x), sy(ly + 0.4), PPU * 8.8);
  }
  if (o[11] > 0) drawBar(x, ly - 0.25, 1.2, Math.min(1, o[11]), GOLD);
}
let holeCanvas = null;
function drawEnvironment(snap, now) {
  const env = snap.env;
  if (!env) return;
  ctx.save();
  ctx.setTransform(1, 0, 0, 1, 0, 0);
  const tints = [null, "rgba(255,190,100,.05)", "rgba(240,115,64,.12)", "rgba(13,20,64,.3)", "rgba(255,153,190,.08)"];
  if (tints[env[1]]) { ctx.fillStyle = tints[env[1]]; ctx.fillRect(0, 0, W, H); }
  if (env[5] > 0) {
    const g = ctx.createRadialGradient(W / 2, H / 2, Math.min(W, H) * 0.2, W / 2, H / 2, Math.max(W, H) * 0.7);
    g.addColorStop(0, "rgba(190,200,210,0)"); g.addColorStop(1, "rgba(190,200,210,.55)");
    ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
  }
  const me = myPlayer();
  if (env[3] > 0 && me) {
    const cx = sx(me.x), cy = sy(me.y), r = env[3] * PPU;
    const g = ctx.createRadialGradient(cx, cy, r * 0.55, cx, cy, r);
    g.addColorStop(0, "rgba(0,0,5,0)"); g.addColorStop(1, "rgba(0,0,5,.94)");
    ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = "rgba(0,0,5,.94)";
    ctx.fillRect(0, 0, W, Math.max(0, cy - r)); ctx.fillRect(0, cy + r, W, H);
    ctx.fillRect(0, cy - r, Math.max(0, cx - r), r * 2); ctx.fillRect(cx + r, cy - r, W, r * 2);
  }
  const t = now / 1000, w = env[0];
  const hash = (i) => { let x = Math.imul(i, 2654435761) >>> 0; x ^= x >>> 13; x = Math.imul(x, 0x5bd1e995) >>> 0; x ^= x >>> 15; return (x & 0xffffff) / 16777216; };
  if (w === 1 || w === 4) {
    ctx.fillStyle = "rgba(190,215,255,.35)";
    const n = w === 4 ? 140 : 90;
    for (let i = 0; i < n; i++) {
      const x = ((hash(i * 7 + 1) * W + t * 180) % W + W) % W, y = ((hash(i * 13 + 5) * H + t * 1500) % H + H) % H;
      ctx.fillRect(x, y, 2, 26);
    }
    if (w === 4 && (t % 7) < 0.08) { ctx.fillStyle = "rgba(255,255,255,.25)"; ctx.fillRect(0, 0, W, H); }
  } else if (w === 5) {
    ctx.fillStyle = "rgba(255,255,255,.7)";
    for (let i = 0; i < 110; i++) {
      const h1 = hash(i * 11 + 3), h2 = hash(i * 17 + 9);
      const x = ((h1 * W + Math.sin(t + i) * 30) % W + W) % W, y = ((h2 * H + t * 90 * (0.6 + h1)) % H + H) % H;
      ctx.beginPath(); ctx.arc(x, y, 1.5 + 2 * h2, 0, Math.PI * 2); ctx.fill();
    }
  } else if (w === 2) {
    ctx.fillStyle = "rgba(201,166,107,.75)";
    for (let i = 0; i < 40; i++) {
      const x = ((hash(i * 5 + 2) * W + env[6] * t * 420) % W + W) % W;
      const y = ((hash(i * 19 + 4) * H - env[7] * t * 420 + Math.sin(t * 3 + i) * 12) % H + H) % H;
      ctx.save(); ctx.translate(x, y); ctx.rotate(t * 3 + i); ctx.fillRect(-6, -3, 12, 6); ctx.restore();
    }
  } else if (w === 6) {
    ctx.fillStyle = "rgba(153,13,20,.06)"; ctx.fillRect(0, 0, W, H);
  }
  ctx.restore();
}
const TOOL_NAMES = ["Shuriken", "Bomba de fumaça", "Sino de bronze", "Petardo", "Óleo", "Corrente com gancho"];
const WOUND_NAMES = ["braço", "perna", "tronco", "olhos"];
const SHURA_NAMES = ["", "IRA", "SEDE", "FRENESI", "QUEDA NO SHURADŌ"];
function escapeHtml(t) { return String(t).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c])); }
