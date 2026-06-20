/**
 * pokerGame.js — Poker front-end
 *
 * Protocol summary:
 *   POST /tables                     → { tableId }
 *   POST /tables/{id}/join           → { sessionToken, playerId }
 *   POST /tables/{id}/start?token=…  → 204
 *   WS   /tables/{id}/ws?token=…     ← TableSnapshotDto push
 *                                    → { type: "Fold"|"Check"|"Call"|"Raise", amount? }
 */

'use strict';

// ── State ───────────────────────────────────────────────────────────────────

const state = {
  tableId:  null,
  playerId: null,
  token:    null,
  ws:       null,
};

// ── Card rendering ───────────────────────────────────────────────────────────

const RANK_LABEL = {
  TWO: '2', THREE: '3', FOUR: '4', FIVE: '5', SIX: '6',
  SEVEN: '7', EIGHT: '8', NINE: '9', TEN: '10',
  JACK: 'J', QUEEN: 'Q', KING: 'K', ACE: 'A',
};

// Suit → { symbol, cssClass }
const SUIT_META = {
  RED_HEART:    { symbol: '♥', css: 'red-heart'    },
  BLUE_DIAMOND: { symbol: '♦', css: 'blue-diamond' },
  GREEN_CLUB:   { symbol: '♣', css: 'green-club'   },
  BLACK_SPADE:  { symbol: '♠', css: 'black-spade'  },
};

function cardHTML(card) {
  const rank = RANK_LABEL[card.rank] ?? card.rank;
  const meta = SUIT_META[card.suit]  ?? { symbol: card.suit, css: 'black-spade' };
  return `<div class="card ${meta.css}">
    <span class="card-rank">${rank}</span>
    <span class="card-suit">${meta.symbol}</span>
  </div>`;
}

function cardBackHTML() {
  return `<div class="card back">🂠</div>`;
}

function cardsHTML(cards, faceDown = false, count = 2) {
  if (faceDown) return Array.from({ length: count }, cardBackHTML).join('');
  if (!cards || cards.length === 0) return '';
  return cards.map(cardHTML).join('');
}

// ── API helpers ──────────────────────────────────────────────────────────────

async function apiPost(path, body = null) {
  const opts = { method: 'POST', headers: {} };
  if (body !== null) {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(body);
  }
  const res = await fetch(path, opts);
  if (!res.ok) {
    const err = new Error(`${path} → ${res.status} ${res.statusText}`);
    err.status = res.status;
    throw err;
  }
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

const createTable = ()             => apiPost('/tables', { blindAmount: 10 });
const joinTable   = (id, name)     => apiPost(`/tables/${id}/join`, { playerName: name, coins: 1000 });
const startHand   = (id, token)    => apiPost(`/tables/${id}/start?token=${token}`);

// ── WebSocket ────────────────────────────────────────────────────────────────

function connectWS(tableId, token) {
  const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
  const ws = new WebSocket(`${proto}//${location.host}/tables/${tableId}/ws?token=${token}`);

  ws.onopen = async () => {
    setStatus('Connected');
    await startHand(tableId, token);
  };

  ws.onmessage = (e) => {
    let snap;
    try { snap = JSON.parse(e.data); } catch { return; }
    if (snap.error) { console.warn('[WS] error:', snap.error); return; }
    render(snap);
  };

  ws.onclose = (e) => setStatus(`Disconnected (${e.code})`);
  ws.onerror = ()  => setStatus('WebSocket error');

  return ws;
}

function sendAction(type, amount = undefined) {
  if (!state.ws || state.ws.readyState !== WebSocket.OPEN) return;
  const payload = amount !== undefined ? { type, amount } : { type };
  state.ws.send(JSON.stringify(payload));
}

// ── Render ───────────────────────────────────────────────────────────────────

function render(snap) {
  const myId      = state.playerId;
  const me        = snap.players.find(p => p.id === myId);
  const opponents = snap.players.filter(p => p.id !== myId);
  const isMyTurn  = snap.currentTurnId === myId;
  const canAct    = isMyTurn && me && !me.folded;

  // Phase / pot
  el('phase').textContent  = snap.phase;
  el('pot').textContent    = `Pot: ${snap.pot}`;
  el('toCall').textContent = snap.toCall > 0 ? `To call: ${snap.toCall}` : '';

  // Board
  const boardEl = el('board-cards');
  boardEl.innerHTML = snap.board.length > 0
    ? snap.board.map(cardHTML).join('')
    : '<span class="placeholder">—</span>';

  // Opponents (up to 2 slots)
  for (let i = 0; i < 2; i++) {
    const opp  = opponents[i];
    const seat = el(`opponent-${i}`);
    if (!seat) continue;

    if (!opp) {
      seat.querySelector('.player-name').textContent  = '—';
      seat.querySelector('.player-cards').innerHTML   = '';
      seat.querySelector('.player-coins').textContent = '';
      seat.querySelector('.player-bet').textContent   = '';
      seat.classList.remove('active-turn', 'folded');
      continue;
    }

    const isShowdown = snap.phase === 'Showdown';
    const hasCards   = opp.holeCards && opp.holeCards.length > 0;
    const turnMark   = snap.currentTurnId === opp.id ? ' ◀' : '';
    const foldMark   = opp.folded ? ' [F]' : '';

    seat.querySelector('.player-name').textContent  = opp.name + foldMark + turnMark;
    seat.querySelector('.player-coins').textContent = `${opp.coins} 🪙`;
    seat.querySelector('.player-bet').textContent   = opp.betThisRound > 0 ? `Bet: ${opp.betThisRound}` : '';
    seat.querySelector('.player-cards').innerHTML   = (isShowdown && hasCards)
      ? cardsHTML(opp.holeCards, false)
      : cardsHTML(null, true, 2);

    seat.classList.toggle('active-turn', snap.currentTurnId === opp.id);
    seat.classList.toggle('folded', opp.folded);
  }

  // My seat
  if (me) {
    el('my-name').textContent  = me.name + (me.folded ? ' [F]' : '');
    el('my-coins').textContent = `${me.coins} 🪙`;
    el('my-bet').textContent   = me.betThisRound > 0 ? `Bet: ${me.betThisRound}` : '';
    el('my-cards').innerHTML   = me.holeCards && me.holeCards.length > 0
      ? cardsHTML(me.holeCards, false)
      : '';
    el('my-seat').classList.toggle('active-turn', isMyTurn);
    el('my-seat').classList.toggle('folded', me.folded);
  }

  // Action buttons
  el('actions').style.display       = canAct ? 'flex' : 'none';
  el('raise-controls').style.display = canAct ? 'flex' : 'none';

  const callBtn  = el('btn-call');
  const checkBtn = el('btn-check');
  if (snap.toCall > 0) {
    callBtn.textContent    = `Call ${snap.toCall}`;
    callBtn.style.display  = '';
    checkBtn.style.display = 'none';
  } else {
    callBtn.style.display  = 'none';
    checkBtn.style.display = '';
  }

  // Start / next hand button
  const showStart = snap.phase === 'Showdown' || snap.phase === 'WaitingForPlayers';
  el('btn-start').style.display = showStart ? '' : 'none';

  // Ranking (showdown)
  const rankingEl = el('ranking');
  if (snap.ranking && snap.ranking.length > 0) {
    rankingEl.innerHTML = '<h3>Results</h3>' +
      snap.ranking.map((r, i) =>
        `<div class="rank-entry">${i + 1}. ${r.playerName} — ${r.handCategory}</div>`
      ).join('');
    rankingEl.style.display = 'block';
  } else {
    rankingEl.style.display = 'none';
  }
}

// ── Event handlers ───────────────────────────────────────────────────────────

async function onJoin(e) {
  e.preventDefault();
  const name = (el('player-name').value.trim() || 'Player').slice(0, 20);

  el('join-screen').style.display = 'none';
  el('game-screen').style.display = 'grid';
  setStatus('Creating table…');

  try {
    const { tableId } = await createTable();
    state.tableId = tableId;

    setStatus('Joining…');
    const { sessionToken, playerId } = await joinTable(tableId, name);
    state.token    = sessionToken;
    state.playerId = playerId;

    setStatus('Connecting…');
    state.ws = connectWS(tableId, sessionToken);
  } catch (err) {
    setStatus('Error: ' + err.message);
    console.error(err);
  }
}

function onRaise() {
  const amount = parseInt(el('raise-amount').value, 10);
  if (!isNaN(amount) && amount > 0) sendAction('Raise', amount);
}

async function onStartHand() {
  if (!state.tableId || !state.token) return;
  const btn = el('btn-start');
  btn.disabled = true;
  try {
    await startHand(state.tableId, state.token);
  } catch (err) {
    // 409 = hand already in progress (e.g. double-click or race); not an error worth surfacing
    if (err.status !== 409) setStatus('Failed to start hand: ' + err.message);
  } finally {
    btn.disabled = false;
  }
}

// ── Util ─────────────────────────────────────────────────────────────────────

const el = (id) => document.getElementById(id);

function setStatus(msg) {
  const s = el('status');
  if (s) s.textContent = msg;
}

// ── Init ─────────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
  el('join-form').addEventListener('submit',  onJoin);
  el('btn-fold').addEventListener('click',   () => sendAction('Fold'));
  el('btn-check').addEventListener('click',  () => sendAction('Check'));
  el('btn-call').addEventListener('click',   () => sendAction('Call'));
  el('btn-raise').addEventListener('click',  onRaise);
  el('btn-start').addEventListener('click',  onStartHand);
});
