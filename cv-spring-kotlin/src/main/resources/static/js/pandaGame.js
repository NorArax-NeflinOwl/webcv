(function () {
  // ── Canvas i DOM ─────────────────────────────────────────────────────────────
  const canvas   = document.getElementById('gc');
  const ctx      = canvas.getContext('2d');
  const wrap     = document.getElementById('canvas-wrap');
  const slider   = document.getElementById('scaleSlider');
  const scaleVal = document.getElementById('scale-val');
  const btnLabel    = document.getElementById('jumpBtnLabel');
  const hintEl      = document.getElementById('hint');
  const saveWrap    = document.getElementById('save-score-wrap');
  const nickInput   = document.getElementById('nickInput');
  const saveBtn     = document.getElementById('saveScoreBtn');
  const saveConfirm = document.getElementById('save-confirm');

  const W      = 884;
  const H      = 300;
  const GROUND = H - 36;
  // The witch flies at this height (body centre); panda on the ground is safe,
  // panda at jump peak collides — you must NOT jump when the witch is coming.
  const FLY_Y  = 120;

  // ── Canvas colour palette (defined in pandaGameStyle.css) ────────────────────
  const css = getComputedStyle(document.documentElement);
  // Scene
  const C_SKY          = css.getPropertyValue('--canvas-sky').trim();
  const C_CLOUD        = css.getPropertyValue('--canvas-cloud').trim();
  const C_GROUND       = css.getPropertyValue('--canvas-ground').trim();
  const C_GROUND_LINE  = css.getPropertyValue('--canvas-ground-line').trim();
  // Bamboo
  const C_BAMBOO_STEM  = css.getPropertyValue('--canvas-bamboo-stem').trim();
  const C_BAMBOO_JOINT = css.getPropertyValue('--canvas-bamboo-joint').trim();
  const C_BAMBOO_LEAF  = css.getPropertyValue('--canvas-bamboo-leaf').trim();
  // Panda
  const C_P_WHITE = css.getPropertyValue('--canvas-panda-white').trim();
  const C_P_BLACK = css.getPropertyValue('--canvas-panda-black').trim();
  const C_P_DARK  = css.getPropertyValue('--canvas-panda-dark').trim();
  const C_P_EAR   = css.getPropertyValue('--canvas-panda-ear').trim();
  const C_P_BELLY = css.getPropertyValue('--canvas-panda-belly').trim();
  const C_P_EYE   = css.getPropertyValue('--canvas-panda-eye').trim();
  const C_P_BLUSH = css.getPropertyValue('--canvas-panda-blush').trim();
  // Overlay and dead screen
  const C_OV_BG    = css.getPropertyValue('--canvas-overlay-bg').trim();
  const C_OV_TITLE = css.getPropertyValue('--canvas-overlay-title').trim();
  const C_OV_SUB   = css.getPropertyValue('--canvas-overlay-sub').trim();
  const C_OV_DEAD  = css.getPropertyValue('--canvas-overlay-dead').trim();
  // Common
  const C_NONE = 'transparent';
  // Witch
  const C_W_BROOM        = css.getPropertyValue('--canvas-witch-broom').trim();
  const C_W_BRISTLE_A    = css.getPropertyValue('--canvas-witch-bristle-a').trim();
  const C_W_BRISTLE_B    = css.getPropertyValue('--canvas-witch-bristle-b').trim();
  const C_W_BRISTLE_BAND = css.getPropertyValue('--canvas-witch-bristle-band').trim();
  const C_W_ROBE         = css.getPropertyValue('--canvas-witch-robe').trim();
  const C_W_CAPE         = css.getPropertyValue('--canvas-witch-cape').trim();
  const C_W_SKIN         = css.getPropertyValue('--canvas-witch-skin').trim();
  const C_W_SKIN_LINE    = css.getPropertyValue('--canvas-witch-skin-line').trim();
  const C_W_NOSE         = css.getPropertyValue('--canvas-witch-nose').trim();
  const C_W_EYE          = css.getPropertyValue('--canvas-witch-eye').trim();
  const C_W_PUPIL        = css.getPropertyValue('--canvas-witch-pupil').trim();
  const C_W_HAT          = css.getPropertyValue('--canvas-witch-hat').trim();
  const C_W_GOLD         = css.getPropertyValue('--canvas-witch-gold').trim();
  // Witch magic sparks (alpha applied via canvas globalAlpha)
  const C_W_SPARKS = [
    css.getPropertyValue('--canvas-witch-spark-0').trim(),
    css.getPropertyValue('--canvas-witch-spark-1').trim(),
    css.getPropertyValue('--canvas-witch-spark-2').trim(),
    css.getPropertyValue('--canvas-witch-spark-3').trim(),
  ];

  // ── Canvas scaling ───────────────────────────────────────────────────────────
  slider.addEventListener('input', function () {
    const s = parseInt(this.value) / 100;
    scaleVal.textContent = this.value + '%';
    wrap.style.width  = (W * s) + 'px';
    wrap.style.height = (H * s) + 'px';
    canvas.style.transform = `scale(${s})`;
  });

  // ── Game state ───────────────────────────────────────────────────────────────
  // states: idle | running | dying | dead
  let state   = 'idle';
  let score   = 0;
  let hiScore = 0;
  let lives   = 3;
  let frame   = 0;
  let speed   = 5;

  // Invincibility after a hit
  let invincible = false;
  let invTimer   = 0;
  const INV_DUR  = 110;

  // Spin animation after a hit (when lives remain)
  let spinning    = false;
  let spinAngle   = 0;
  let spinFrames  = 0;
  const SPIN_TOTAL = 50;

  // Death animation: spin + fly up and fall
  let deathAngle = 0;
  let deathFrame = 0;
  let deathY     = 0;
  let deathVY    = 0;
  const DEATH_SPIN_TOTAL = 70;

  // Eating animation frame counter on the "dead" screen (infinite)
  let deadFrame = 0;

  const panda = { x: 80, y: GROUND, vy: 0, onGround: true, width: 44, height: 50 };

  let obstacles    = [];
  let obsTimer     = 0;
  let obsInterval  = 90;

  let witchTimer    = 0;
  let witchInterval = 300; // first witch appearance

  let clouds  = [{ x: 100, y: 22, r: 22 }, { x: 310, y: 35, r: 16 }, { x: 520, y: 18, r: 20 }];
  let groundX = 0;

  // ── Game logic ───────────────────────────────────────────────────────────────
  function jump() {
    if (state === 'idle' || state === 'dead') { startGame(); return; }
    if (state === 'dying') return;
    if (panda.onGround && !spinning) {
      panda.vy = -13.5;
      panda.onGround = false;
    }
  }

  document.getElementById('jumpBtn').addEventListener('click', jump);
  document.addEventListener('keydown', e => {
    if (e.code === 'Space') { e.preventDefault(); jump(); }
  });

  saveBtn.addEventListener('click', function () {
    const nick = nickInput.value.trim() || 'Anonymous';

    saveBtn.disabled = true;
    saveBtn.classList.add('is-saving');
    saveConfirm.textContent = 'Saving...';

    fetch('/api/pandagame/scores', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nick, score })
    })
        .then(res =>
            res.json()
                .catch(() => ({}))
                .then(data => {
                  if (!res.ok) throw new Error(data.error || ('HTTP ' + res.status));
                  return data;
                })
        )
        .then(data => {
          saveConfirm.textContent = `Saved: ${data.nick} — ${data.score} pts`;
        })
        .catch(err => {
          console.error('Score save error:', err);
          saveConfirm.textContent = 'Failed to save score: ' + err.message;
          saveBtn.disabled = false;
          saveBtn.classList.remove('is-saving');
        });
  });

  function startGame() {
    // game state
    state     = 'running';
    score     = 0;
    lives     = 3;
    speed     = 5;
    frame     = 0;
    deadFrame = 0;

    // invincibility
    invincible = false;
    invTimer   = 0;

    // hit spin
    spinning   = false;
    spinAngle  = 0;
    spinFrames = 0;

    // death animation
    deathAngle = 0;
    deathFrame = 0;
    deathY     = GROUND;
    deathVY    = 0;

    // save panel — hide on new game
    saveWrap.classList.remove('is-visible');
    saveConfirm.textContent = '';
    saveBtn.disabled        = false;
    saveBtn.classList.remove('is-saving');

    // world
    obstacles    = [];
    obsTimer     = 0;
    obsInterval  = 90;
    witchTimer   = 0;
    witchInterval = 300 + Math.random() * 200;

    // panda
    panda.y        = GROUND;
    panda.vy       = 0;
    panda.onGround = true;

    updateUI();
    btnLabel.textContent = 'Jump';
    hintEl.textContent   = 'Button or Space = jump';
  }

  function updateUI() {
    document.getElementById('sc').textContent = score;
    document.getElementById('hi').textContent = hiScore;
    for (let i = 1; i <= 3; i++) {
      document.getElementById('lv' + i).classList.toggle('is-lost', i > lives);
    }
  }

  function spawnBamboo() {
    const h = 28 + Math.random() * 38;
    const w = 13 + Math.random() * 9;
    // 38% chance of double bamboo
    const n = Math.random() < 0.38 ? 2 : 1;
    for (let i = 0; i < n; i++)
      obstacles.push({ type: 'bamboo', x: W + i * (w + 10), y: GROUND - h + 8, w, h });
  }

  function spawnWitch() {
    // o.x = witch horizontal centre; o.y = FLY_Y = body vertical centre
    obstacles.push({ type: 'witch', x: W + 55, y: FLY_Y, anim: 0 });
  }

  // Returns false when an obstacle of a different type is too close to the right edge —
  // the player wouldn't have time to react to both at once.
  function canSpawn(type) {
    const SAFE = 260; // minimum gap (px) between different obstacle types
    for (const o of obstacles) {
      if (o.type !== type && o.x > W - SAFE) return false;
    }
    return true;
  }

  function checkHit() {
    if (invincible) return false;
    // panda hitbox shrunk for more "forgiveness"
    const px = panda.x + 10, py = panda.y - panda.height + 12;
    const pw = panda.width - 18, ph = panda.height - 18;
    for (const o of obstacles) {
      if (o.type === 'witch') {
        // hitbox covers body + lower part of hat
        if (px < o.x + 18 && px + pw > o.x - 18 && py < o.y + 10 && py + ph > o.y - 22)
          return true;
      } else {
        if (px < o.x + o.w - 4 && px + pw > o.x + 4 && py < o.y + o.h && py + ph > o.y)
          return true;
      }
    }
    return false;
  }

  function update() {
    if (state === 'dying') {
      deathFrame++;
      deathAngle = (deathFrame / DEATH_SPIN_TOTAL) * Math.PI * 4;
      deathVY += 0.5;
      deathY  += deathVY;
      if (deathY >= GROUND) deathY = GROUND;
      if (deathFrame >= DEATH_SPIN_TOTAL) {
        state = 'dead';
        btnLabel.textContent = 'Start';
        hintEl.textContent   = 'Click Start or Space to play again';
        saveWrap.classList.add('is-visible');
        nickInput.focus();
      }
      return;
    }

    if (state === 'dead')    { deadFrame++; return; }
    if (state !== 'running') return;

    // physics
    frame++;
    score++;
    speed = 5 + Math.floor(score / 100) * 0.4;

    panda.vy += 0.65;
    panda.y  += panda.vy;
    if (panda.y >= GROUND) {
      panda.y        = GROUND;
      panda.vy       = 0;
      panda.onGround = true;
    }

    // obstacles
    obsTimer++;
    if (obsTimer >= obsInterval && canSpawn('bamboo')) {
      spawnBamboo();
      obsTimer    = 0;
      obsInterval = 50 + Math.random() * 50;
    }
    for (const o of obstacles) {
      o.x -= speed;
      if (o.type === 'witch') o.anim++;
    }
    obstacles = obstacles.filter(o => o.type === 'witch' ? o.x > -65 : o.x + o.w > -10);

    // witch — appears after score > 150
    if (score > 150) {
      witchTimer++;
      if (witchTimer >= witchInterval && canSpawn('witch')) {
        spawnWitch();
        witchTimer    = 0;
        witchInterval = 280 + Math.random() * 220;
      }
    }

    // clouds and ground
    for (const c of clouds) {
      c.x -= speed * 0.28;
      if (c.x + c.r < 0) { c.x = W + c.r; c.y = 15 + Math.random() * 45; }
    }
    groundX = (groundX - speed * 0.6 + W) % W;

    // hit spin animation
    if (spinning) {
      spinFrames++;
      spinAngle = (spinFrames / SPIN_TOTAL) * Math.PI * 2;
      if (spinFrames >= SPIN_TOTAL) { spinning = false; spinAngle = 0; spinFrames = 0; }
    }

    // invincibility
    if (invincible) { invTimer--; if (invTimer <= 0) invincible = false; }

    // collision
    if (checkHit()) {
      lives--;
      if (score > hiScore) hiScore = score;
      updateUI();

      spinning   = true;
      spinAngle  = 0;
      spinFrames = 0;

      if (lives <= 0) {
        state      = 'dying';
        deathFrame = 0;
        deathAngle = 0;
        deathY     = panda.y - panda.height / 2;
        deathVY    = -10;
      } else {
        invincible = true;
        invTimer   = INV_DUR;
      }
    }

    if (frame % 4 === 0) updateUI();
  }

  // ── Drawing helpers ──────────────────────────────────────────────────────────

  // Filled ellipse with outline
  function fillEllipse(x, y, rx, ry, rot, fill) {
    ctx.fillStyle = fill;
    ctx.beginPath();
    ctx.ellipse(x, y, rx, ry, rot, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }

  // Filled circle with outline
  function fillArc(x, y, r, fill) {
    ctx.fillStyle = fill;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }

  // Sets stroke colour and width to black
  function setOutline(w) {
    ctx.strokeStyle = C_P_BLACK;
    ctx.lineWidth   = w;
  }

  // Rotates context around point (cx, cy), draws fn(), restores transform
  function drawRotated(cx, cy, angle, fn) {
    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate(angle);
    ctx.translate(-cx, -cy);
    fn();
    ctx.restore();
  }

  // Semi-transparent overlay + two-line text centred on canvas
  function drawOverlay(line1, line2) {
    ctx.fillStyle = C_OV_BG;
    ctx.fillRect(0, 0, W, H);
    ctx.textAlign = 'center';
    ctx.fillStyle = C_OV_TITLE;
    ctx.font      = '500 18px sans-serif';
    ctx.fillText(line1, W / 2, H / 2 - 8);
    ctx.fillStyle = C_OV_SUB;
    ctx.font      = '400 13px sans-serif';
    ctx.fillText(line2, W / 2, H / 2 + 16);
  }

  // ── Scene drawing ────────────────────────────────────────────────────────────

  function drawCloud(c) {
    ctx.fillStyle = C_CLOUD;
    ctx.beginPath();
    ctx.arc(c.x,              c.y,     c.r,        0, Math.PI * 2);
    ctx.arc(c.x + c.r * 0.75, c.y + 3, c.r * 0.7, 0, Math.PI * 2);
    ctx.arc(c.x - c.r * 0.65, c.y + 5, c.r * 0.6, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawGround() {
    const lineY = GROUND + 8; // y of ground line (used 4×)
    ctx.fillStyle = C_GROUND;
    ctx.fillRect(0, lineY, W, H - GROUND);

    ctx.strokeStyle = C_GROUND_LINE;
    ctx.lineWidth   = 1.5;
    ctx.beginPath();
    ctx.moveTo(0, lineY);
    ctx.lineTo(W, lineY);
    ctx.stroke();

    // animated "grass tufts" scrolling at terrain speed
    ctx.fillStyle = C_GROUND;
    for (let i = 0; i < 8; i++) {
      const gx = (groundX + i * (W / 8)) % W;
      ctx.beginPath();
      ctx.arc(gx, lineY, 4, Math.PI, 0);
      ctx.fill();
    }
  }

  function drawBamboo(o) {
    // trzon
    ctx.fillStyle = C_BAMBOO_STEM;
    ctx.fillRect(o.x + o.w * 0.3, o.y, o.w * 0.4, o.h);

    // nodes
    ctx.fillStyle = C_BAMBOO_JOINT;
    const segs = Math.floor(o.h / 13);
    for (let i = 0; i <= segs; i++)
      ctx.fillRect(o.x + o.w * 0.18, o.y + i * 13 - 2, o.w * 0.64, 3);

    // listki na czubku
    ctx.fillStyle = C_BAMBOO_LEAF;
    ctx.beginPath(); ctx.ellipse(o.x + o.w * 0.3 - 9, o.y - 5,  13, 5, -0.4, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(o.x + o.w * 0.7 + 7, o.y - 3,  11, 4.5, 0.5, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(o.x + o.w * 0.5,     o.y - 11,  9, 4,   0,   0, Math.PI * 2); ctx.fill();
  }

  // ── Witch drawing ────────────────────────────────────────────────────────────
  // wx, wy = horizontal and vertical body centre; anim = animation frame counter
  function drawWitch(wx, wy, anim) {
    const bob = Math.sin(anim * 0.12) * 2.5; // gentle floating
    const y   = wy + bob;

    // Positions reused throughout the function
    const headCX   = wx - 1;  // head x centre = hat brim x centre
    const hatBaseY = y - 20;  // base of cone and hat brim (3×)
    const broomY   = y + 7;   // y of bristle base and sparks (3×)
    const bindX    = wx + 32; // x of broom binding (2×)

    ctx.save();

    // --- Broom ---
    ctx.lineCap     = 'round';
    ctx.strokeStyle = C_W_BROOM;
    ctx.lineWidth   = 3.5;
    ctx.beginPath();
    ctx.moveTo(wx - 32, y + 10);
    ctx.lineTo(wx + 36, y + 5);
    ctx.stroke();

    // Broom bristles — fan on the right side (exit side)
    for (let i = -5; i <= 5; i++) {
      ctx.strokeStyle = i % 2 === 0 ? C_W_BRISTLE_A : C_W_BRISTLE_B;
      ctx.lineWidth   = 1.5;
      ctx.beginPath();
      ctx.moveTo(wx + 34, broomY);
      ctx.lineTo(wx + 50, broomY + i * 3);
      ctx.stroke();
    }
    // Binding holding the bristles
    ctx.strokeStyle = C_W_BRISTLE_BAND;
    ctx.lineWidth   = 3;
    ctx.beginPath();
    ctx.moveTo(bindX, y + 3);
    ctx.lineTo(bindX, y + 12);
    ctx.stroke();

    // --- Robe / body ---
    setOutline(1.2);
    ctx.fillStyle = C_W_ROBE;
    ctx.beginPath();
    ctx.ellipse(wx, y + 12, 12, 17, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    // Cape (fluttering in flight — on the left side, behind the back)
    const flap = Math.sin(anim * 0.18) * 7;
    ctx.fillStyle = C_W_CAPE;
    ctx.beginPath();
    ctx.moveTo(wx - 6, y);
    ctx.quadraticCurveTo(wx - 28, y + 4 - flap, wx - 24, y + 24);
    ctx.lineTo(wx - 3, y + 22);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    // --- Head (facing left) ---
    ctx.fillStyle   = C_W_SKIN;
    ctx.strokeStyle = C_W_SKIN_LINE;
    ctx.lineWidth   = 1.2;
    ctx.beginPath();
    ctx.arc(headCX, y - 8, 13, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    // Nose (pointed, typical witch nose, facing left)
    ctx.fillStyle   = C_W_NOSE;
    ctx.strokeStyle = C_W_SKIN_LINE;
    ctx.lineWidth   = 1;
    ctx.beginPath();
    ctx.moveTo(wx - 13, y - 5);
    ctx.lineTo(wx - 23, y - 2);
    ctx.lineTo(wx - 12, y - 2);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    // Eye (green glow)
    ctx.strokeStyle = C_NONE;
    ctx.fillStyle   = C_W_EYE;
    ctx.beginPath(); ctx.arc(wx - 9, y - 9, 3.5, 0, Math.PI * 2); ctx.fill();
    // Pupil
    ctx.fillStyle = C_W_PUPIL;
    ctx.beginPath(); ctx.arc(wx - 10, y - 9, 1.8, 0, Math.PI * 2); ctx.fill();
    // Occasional blink
    if (Math.sin(anim * 0.06) > 0.92) {
      ctx.fillStyle = C_W_SKIN;
      ctx.fillRect(wx - 14, y - 11, 9, 5);
    }

    // --- Hat ---
    setOutline(1.5);
    ctx.fillStyle = C_W_HAT;
    // Hat cone (slightly tilted left)
    ctx.beginPath();
    ctx.moveTo(wx - 15, hatBaseY);
    ctx.lineTo(wx - 2,  y - 50);
    ctx.lineTo(wx + 13, hatBaseY);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    // Brim
    ctx.beginPath();
    ctx.ellipse(headCX, hatBaseY, 17, 5, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    // Gold buckle
    ctx.strokeStyle = C_NONE;
    ctx.fillStyle   = C_W_GOLD;
    ctx.fillRect(wx - 6, y - 29, 9, 6);
    ctx.fillStyle = C_W_HAT;
    ctx.fillRect(wx - 4, y - 28, 5, 4);

    // --- Magic sparks behind the broom ---
    for (let i = 0; i < 4; i++) {
      const sp = (anim + i * 11) % 44;
      if (sp < 30) {
        const alpha = (30 - sp) / 30;
        const sx    = wx - 20 - sp * 0.9;
        const sy    = broomY + Math.sin(sp * 0.32 + i * 1.8) * 8;
        const sr    = Math.max(0, 3 - sp * 0.07);
        ctx.globalAlpha = alpha;
        ctx.fillStyle   = C_W_SPARKS[i];
        ctx.beginPath(); ctx.arc(sx, sy, sr, 0, Math.PI * 2); ctx.fill();
      }
    }

    ctx.restore();
  }

  // ── Panda drawing ────────────────────────────────────────────────────────────

  // Running / jumping panda. px, py = top-left corner (py = panda.y - panda.h)
  function drawPandaRunning(px, py) {
    const leg = Math.sin(frame * 0.28) * 6;
    setOutline(1.5);

    // legs (swinging in sync with frame)
    fillEllipse(px + 11, py + 46 + (panda.onGround ?  leg : 0), 8, 7,  0.2, C_P_BLACK);
    fillEllipse(px + 31, py + 46 + (panda.onGround ? -leg : 0), 8, 7, -0.2, C_P_BLACK);

    // torso
    fillEllipse(px + 22, py + 32, 18, 21, 0, C_P_WHITE);

    // arms
    fillEllipse(px + 5,  py + 28 + (panda.onGround ?  leg * 0.3 : -4), 6, 10,  0.5, C_P_BLACK);
    fillEllipse(px + 39, py + 28 + (panda.onGround ? -leg * 0.3 : -4), 6, 10, -0.5, C_P_BLACK);

    // head
    fillArc(px + 22, py + 15, 16, C_P_WHITE);

    // ears
    fillArc(px + 9,  py + 4, 6,   C_P_BLACK);
    fillArc(px + 35, py + 4, 6,   C_P_BLACK);
    setOutline(1);
    fillArc(px + 9,  py + 4, 3.5, C_P_EAR);
    fillArc(px + 35, py + 4, 3.5, C_P_EAR);

    // eye patches
    setOutline(1.5);
    fillEllipse(px + 14, py + 14, 5.5, 4.5, -0.3, C_P_DARK);
    fillEllipse(px + 30, py + 14, 5.5, 4.5,  0.3, C_P_DARK);

    // eye whites
    setOutline(1);
    fillArc(px + 14, py + 14, 2.5, C_P_EYE);
    fillArc(px + 30, py + 14, 2.5, C_P_EYE);

    // pupils (no outline)
    ctx.strokeStyle = C_NONE;
    fillArc(px + 14.5, py + 14, 1.2, C_P_BLACK);
    fillArc(px + 30.5, py + 14, 1.2, C_P_BLACK);

    // nose and smile
    setOutline(1.5);
    fillEllipse(px + 22, py + 20, 3, 2, 0, C_P_DARK);
    ctx.strokeStyle = C_P_DARK; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.arc(px + 22, py + 21, 3.5, 0.1, Math.PI - 0.1); ctx.stroke();
  }

  // Panda eating bamboo (game-over screen). cx, cy = top-left corner
  function drawPandaEating(cx, cy, animFrame) {
    const sSin   = Math.sin(animFrame * 0.18); // shared sine for chew, bshift and eyeOpen (3×)
    const chew   = sSin * 1.5;
    const bob    = Math.sin(animFrame * 0.09) * 1.2;
    const bshift = sSin * 0.8; // bamboo shift in paws

    setOutline(1.5);

    // legs
    fillEllipse(cx + 10, cy + 50 + bob, 11, 7,  0.6, C_P_BLACK);
    fillEllipse(cx + 36, cy + 50 + bob, 11, 7, -0.6, C_P_BLACK);

    // torso with belly
    fillEllipse(cx + 22, cy + 36 + bob, 18, 20, 0, C_P_WHITE);
    fillEllipse(cx + 22, cy + 39 + bob, 10, 12, 0, C_P_BELLY);

    // arms (swaying with body)
    fillEllipse(cx + 7,  cy + 28 + bob, 6, 11,  0.9, C_P_BLACK);
    fillEllipse(cx + 37, cy + 26 + bob, 6, 11, -0.9, C_P_BLACK);

    // head
    fillArc(cx + 22, cy + 15 + bob, 16, C_P_WHITE);

    // ears
    fillArc(cx + 9,  cy + 3 + bob, 6,   C_P_BLACK);
    fillArc(cx + 35, cy + 3 + bob, 6,   C_P_BLACK);
    setOutline(1);
    fillArc(cx + 9,  cy + 3 + bob, 3.5, C_P_EAR);
    fillArc(cx + 35, cy + 3 + bob, 3.5, C_P_EAR);

    // eye patches
    setOutline(1.5);
    fillEllipse(cx + 14, cy + 13 + bob, 5.5, 4.5, -0.3, C_P_DARK);
    fillEllipse(cx + 30, cy + 13 + bob, 5.5, 4.5,  0.3, C_P_DARK);

    // eyes open and close in rhythm with chewing
    const eyeOpen = sSin > 0;
    setOutline(1);
    if (eyeOpen) {
      fillArc(cx + 14, cy + 13 + bob, 2.5, C_P_EYE);
      fillArc(cx + 30, cy + 13 + bob, 2.5, C_P_EYE);
      ctx.strokeStyle = C_NONE;
      fillArc(cx + 14.5, cy + 13 + bob, 1.2, C_P_BLACK);
      fillArc(cx + 30.5, cy + 13 + bob, 1.2, C_P_BLACK);
    } else {
      // squinted eyes ^^
      ctx.strokeStyle = C_P_EYE; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(cx + 14, cy + 13 + bob, 2.5, Math.PI, 0); ctx.stroke();
      ctx.beginPath(); ctx.arc(cx + 30, cy + 13 + bob, 2.5, Math.PI, 0); ctx.stroke();
    }

    // nose
    setOutline(1.5);
    fillEllipse(cx + 22, cy + 19 + bob, 3, 2, 0, C_P_DARK);

    // animated muzzle (chewing motion)
    ctx.strokeStyle = C_P_DARK; ctx.lineWidth = 1.8;
    ctx.beginPath(); ctx.arc(cx + 22, cy + 21 + bob + chew, 4, 0, Math.PI); ctx.stroke();

    // blush
    ctx.strokeStyle = C_NONE;
    ctx.fillStyle   = C_P_BLUSH;
    ctx.beginPath(); ctx.ellipse(cx + 10, cy + 18 + bob, 5, 3, 0, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(cx + 34, cy + 18 + bob, 5, 3, 0, 0, Math.PI * 2); ctx.fill();

    // bamboo in paws (gently swaying)
    ctx.save();
    ctx.translate(cx + 22, cy + 27);
    ctx.rotate(Math.PI / 2);
    ctx.fillStyle   = C_BAMBOO_STEM; ctx.fillRect(-3 + bshift, -21, 6, 42);
    ctx.strokeStyle = C_P_DARK;      ctx.lineWidth = 1.5;
    ctx.strokeRect(-3 + bshift, -21, 6, 42);
    ctx.fillStyle = C_BAMBOO_JOINT;
    for (let i = 0; i < 3; i++) ctx.fillRect(-5 + bshift, -19 + i * 13, 10, 3);
    ctx.restore();

    // bamboo crumbs
    setOutline(1.5);
    const p = animFrame % 40;
    if (p < 20) {
      ctx.fillStyle = `rgba(100,100,100,${(20 - p) / 20 * 0.5})`;
      ctx.beginPath(); ctx.arc(cx + 26 + p * 0.4, cy + 22 + bob - p * 0.3, 2,   0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(cx + 24 + p * 0.3, cy + 20 + bob - p * 0.5, 1.5, 0, Math.PI * 2); ctx.fill();
    }
  }

  // ── Main render loop ──────────────────────────────────────────────────────────
  function draw() {
    // sky
    ctx.clearRect(0, 0, W, H);
    ctx.fillStyle = C_SKY;
    ctx.fillRect(0, 0, W, H);

    for (const c of clouds) drawCloud(c);

    // witches — sky layer (in front of ground, behind clouds)
    for (const o of obstacles) {
      if (o.type === 'witch') drawWitch(o.x, o.y, o.anim);
    }

    drawGround();

    // bamboos — ground layer
    for (const o of obstacles) {
      if (o.type === 'bamboo') drawBamboo(o);
    }

    const pandaTop = panda.y - panda.height;
    const pandaCX  = panda.x + panda.width / 2;
    const pandaCY  = panda.y - panda.height / 2;

    if (state === 'running') {
      if (spinning) {
        drawRotated(pandaCX, pandaCY, spinAngle, () => drawPandaRunning(panda.x, pandaTop));
      } else if (!(invincible && Math.floor(invTimer / 5) % 2 === 0)) {
        // flash when invincible
        drawPandaRunning(panda.x, pandaTop);
      }

    } else if (state === 'dying') {
      drawRotated(pandaCX, deathY, deathAngle, () => drawPandaRunning(panda.x, deathY - panda.height / 2));

    } else if (state === 'idle') {
      drawPandaRunning(panda.x, pandaTop);
      drawOverlay(
        'Click button below to start',
        'Jump = Space / button   |   Witch flies high — DON\'T jump!'
      );

    } else if (state === 'dead') {
      // panda drawn twice: once under the overlay, once above it
      const EX = W / 2 - 22, EY = H / 2 - 58;
      drawPandaEating(EX, EY, deadFrame);
      ctx.fillStyle = C_OV_BG;
      ctx.fillRect(0, 0, W, H);
      drawPandaEating(EX, EY, deadFrame);

      ctx.textAlign = 'center';
      ctx.fillStyle = C_OV_TITLE; ctx.font = '500 17px sans-serif';
      ctx.fillText('Game over!  Score: ' + score, W / 2, H / 2 + 36);
      ctx.fillStyle = C_OV_DEAD;  ctx.font = '400 12px sans-serif';
      ctx.fillText('Click Start to play again', W / 2, H / 2 + 56);
    }
  }

  function loop() {
    update();
    draw();
    requestAnimationFrame(loop);
  }
  loop();
})();
