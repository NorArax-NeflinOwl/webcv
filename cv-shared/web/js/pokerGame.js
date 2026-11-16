/**
 * pokerGame.js  Poker front-end
 *
 * Protocol summary:
 *   POST /api/pokertable                     → { tableId }
 *   POST /api/pokertable/{id}/join           → { sessionToken, playerId }
 *   POST /api/pokertable/{id}/start          → 204   (Authorization: Bearer <sessionToken>)
 *   WS   /api/pokertable/{id}/ws             → { type: "Auth", token: sessionToken }  (first message)
 *                                    ← TableSnapshotDto push
 *                                    → { type: "Fold"|"Check"|"Call"|"Raise", amount? }
 *
 * The session token never goes into a URL: URLs end up in the gateway's access log.
 */

'use strict';

//  State 

const state = {
  tableId:  null,
  playerId: null,
  token:    null,
  ws:       null,
};

//  Card rendering 

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

// Only the fixed labels above reach the markup: an unknown rank or suit from the server shows as '?'.
function cardHTML(card) {
  const rank = RANK_LABEL[card.rank] ?? '?';
  const meta = SUIT_META[card.suit]  ?? { symbol: '?', css: 'black-spade' };
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

//  API helpers 

// The public server limits tables and requests; these answers mean "later", not "broken".
const BUSY_MESSAGES = {
  429: 'Too many requests, please try again in a minute.',
  503: 'All poker tables are busy, please try again in a few minutes.',
};

async function apiPost(path, body = null, token = null) {
  const opts = { method: 'POST', headers: {} };
  if (token !== null) opts.headers['Authorization'] = `Bearer ${token}`;
  if (body !== null) {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(body);
  }
  const res = await fetch(path, opts);
  if (!res.ok) {
    const err = new Error(BUSY_MESSAGES[res.status] ?? `${path} → ${res.status} ${res.statusText}`);
    err.status = res.status;
    throw err;
  }
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

const createTable = ()             => apiPost('/api/pokertable', { blindAmount: 10 });
const joinTable   = (id, name)     => apiPost(`/api/pokertable/${id}/join`, { playerName: name, coins: 1000 });
const startHand   = (id, token)    => apiPost(`/api/pokertable/${id}/start`, null, token);

//  WebSocket 

function connectWS(tableId, token) {
  const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
  const ws = new WebSocket(`${proto}//${location.host}/api/pokertable/${tableId}/ws`);

  ws.onopen = async () => {
    ws.send(JSON.stringify({ type: 'Auth', token }));
    setStatus('Connected');
    window.addEventListener('beforeunload', onBeforeUnload);
    await startHand(tableId, token);
  };

  ws.onmessage = (e) => {
    let snap;
    try { snap = JSON.parse(e.data); } catch { return; }
    if (snap.error) { console.warn('[WS] error:', snap.error); return; }
    render(snap);
  };

  ws.onclose = (e) => {
    // 4004: the server removed the table (idle too long, or the player was gone).
    setStatus(e.code === 4004 ? 'The table was closed after inactivity. Reload to play again.' : `Disconnected (${e.code})`);
    window.removeEventListener('beforeunload', onBeforeUnload);
  };
  ws.onerror = () => setStatus('WebSocket error');

  return ws;
}

function onBeforeUnload(e) {
  e.preventDefault();
}

function sendAction(type, amount = undefined) {
  if (!state.ws || state.ws.readyState !== WebSocket.OPEN) return;
  const payload = amount !== undefined ? { type, amount } : { type };
  state.ws.send(JSON.stringify(payload));
}

//  Render 

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
    : '<span class="placeholder"></span>';

  // Opponents (up to 2 slots)
  for (let i = 0; i < 2; i++) {
    const opp  = opponents[i];
    const seat = el(`opponent-${i}`);
    if (!seat) continue;

    if (!opp) {
      seat.querySelector('.player-name').textContent  = '';
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
  el('actions').style.display        = canAct ? 'flex' : 'none';
  el('raise-controls').style.display = canAct ? 'flex' : 'none';

  // Cap raise input to the player's available chips so the browser validates before sending
  if (canAct && me) {
    const raiseInput = el('raise-amount');
    raiseInput.max = me.coins;
    if (parseInt(raiseInput.value, 10) > me.coins) raiseInput.value = me.coins;
  }

  const callBtn  = el('btn-call');
  const checkBtn = el('btn-check');
  if (snap.toCall > 0) {
    callBtn.textContent    = `Call ${snap.toCall}`;
    callBtn.style.display  = 'block';
    checkBtn.style.display = 'none';
  } else {
    callBtn.style.display  = 'none';
    checkBtn.style.display = '';
  }

  // "Next hand" button  only after a completed hand, never on the initial waiting state
  // (the first hand is started automatically via ws.onopen)
  el('btn-start').style.display = snap.phase === 'Showdown' ? 'block' : 'none';

  // Game over detection: after showdown, if only 1 player has chips left the session is over
  if (snap.phase === 'Showdown') {
    const alive = snap.players.filter(p => p.coins > 0);
    if (alive.length <= 1) {
      const humanWon = alive.length === 1 && alive[0].id === myId;
      showGameOver(humanWon, alive[0]?.coins ?? 0);
    }
  }

  // Ranking (showdown)
  // Built as text nodes: the player names come from the clients and must never be parsed as HTML.
  const rankingEl = el('ranking');
  if (snap.ranking && snap.ranking.length > 0) {
    const title = document.createElement('h3');
    title.textContent = 'Results';
    const entries = snap.ranking.map((r, i) => {
      const entry = document.createElement('div');
      entry.className   = 'rank-entry';
      entry.textContent = `${i + 1}. ${r.playerName}  ${r.handCategory}`;
      return entry;
    });
    rankingEl.replaceChildren(title, ...entries);
    rankingEl.style.display = 'block';
  } else {
    rankingEl.style.display = 'none';
  }
}

//  Event handlers 

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

function showGameOver(humanWon, finalCoins) {
  el('game-over-icon').textContent  = humanWon ? '🏆' : '💀';
  el('game-over-title').textContent = humanWon ? 'You Win!' : 'Game Over';
  el('game-over-msg').textContent   = humanWon
    ? `You wiped out all opponents and finished with ${finalCoins} coins.`
    : 'You ran out of coins. Better luck next time!';
  window.removeEventListener('beforeunload', onBeforeUnload);
  el('game-over').style.display = 'flex';
  el('btn-new-game').focus();
}

//  Util 

const el = (id) => document.getElementById(id);

// The stylesheet hides some elements at first, so the inline style alone does not tell.
const isShown = (id) => getComputedStyle(el(id)).display !== 'none';

function setStatus(msg) {
  const s = el('status');
  if (s) s.textContent = msg;
}

//  Init 

document.addEventListener('DOMContentLoaded', () => {
  el('join-form').addEventListener('submit',  onJoin);
  el('btn-fold').addEventListener('click',   () => sendAction('Fold'));
  el('btn-check').addEventListener('click',  () => sendAction('Check'));
  el('btn-call').addEventListener('click',   () => sendAction('Call'));
  el('btn-raise').addEventListener('click',  onRaise);
  el('btn-start').addEventListener('click',  onStartHand);
  el('btn-new-game').addEventListener('click', () => location.reload());

  // Keyboard shortcuts (active only when it's the player's turn)
  document.addEventListener('keydown', (e) => {
    // Ignore when typing in an input field
    if (e.target.tagName === 'INPUT') return;

    const actionsVisible = isShown('actions');

    switch (e.key) {
      case ' ':
        e.preventDefault();
        if (actionsVisible) {
          isShown('btn-call')
            ? sendAction('Call')
            : sendAction('Check');
        } else if (isShown('btn-start')) {
          onStartHand();
        }
        break;
      case 'f':
      case 'F':
        if (!actionsVisible) return;
        sendAction('Fold');
        break;
      case 'r':
      case 'R':
      case 'Enter':
        if (actionsVisible) onRaise();
        else if (isShown('btn-start')) {
          onStartHand();
        }
        break;
      case 'e':
      case 'E':
        if (!actionsVisible) return;
        e.preventDefault();
        el('raise-amount').focus();
        el('raise-amount').select();
        break;
    }
  });
});
