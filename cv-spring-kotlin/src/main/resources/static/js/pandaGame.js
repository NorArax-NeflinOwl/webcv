(function () {

  const WIDTH  = 884;
  const HEIGTH = 300;
  const GROUND = HEIGTH - 36;
  // The witch flies at this height (body centre); panda on the ground is safe,
  // panda at jump peak collides — you must NOT jump when the witch is coming.
  const WITCH_HEIGTH_SPAWN  = 120;

  // ── Invincibility / spin constants ───────────────────────────────────────────
  const INV_DUR          = 110; // frames of invincibility after a hit
  const SPIN_TOTAL       = 50;  // frames of hit-spin animation
  const DEATH_SPIN_TOTAL = 70;  // frames of death-spin animation

  // ── DOM elements ─────────────────────────────────────────────────────────────
  // All DOM references centralised here; ctx is a rendering context, not an element.
  const elements = {
    canvas:           document.getElementById('gc'),
    wrap:             document.getElementById('canvas-wrap'),
    slider:           document.getElementById('scaleSlider'),
    scaleValue:       document.getElementById('scale-val'),
    jumpButton:       document.getElementById('jumpBtn'),
    jumpLabel:        document.getElementById('jumpBtnLabel'),
    hint:             document.getElementById('hint'),
    savePanel:        document.getElementById('save-score-wrap'),
    nickInput:        document.getElementById('nickInput'),
    saveButton:       document.getElementById('saveScoreBtn'),
    saveConfirm:      document.getElementById('save-confirm'),
    scoreDisplay:     document.getElementById('sc'),
    highScoreDisplay: document.getElementById('hi'),
    lifeIcons:        [1, 2, 3].map(i => document.getElementById('lv' + i)),
  };

  const ctx = elements.canvas.getContext('2d');

  // ── Canvas colour palette (values defined in pandaGameStyle.css) ────────────
  const css  = getComputedStyle(document.documentElement);
  const prop = name => css.getPropertyValue(name).trim();
  const colors = {
    // Scene
    sky:        prop('--canvas-sky'),
    cloud:      prop('--canvas-cloud'),
    ground:     prop('--canvas-ground'),
    groundLine: prop('--canvas-ground-line'),
    // Bamboo
    bambooStem:  prop('--canvas-bamboo-stem'),
    bambooJoint: prop('--canvas-bamboo-joint'),
    bambooLeaf:  prop('--canvas-bamboo-leaf'),
    // Panda
    pandaWhite: prop('--canvas-panda-white'),
    pandaBlack: prop('--canvas-panda-black'),
    pandaDark:  prop('--canvas-panda-dark'),
    pandaEar:   prop('--canvas-panda-ear'),
    pandaEye:   prop('--canvas-panda-eye'),
    pandaBlush: prop('--canvas-panda-blush'),
    // Overlay
    overlayBackground: prop('--canvas-overlay-bg'),
    overlayTitle:      prop('--canvas-overlay-title'),
    overlaySubtitle:   prop('--canvas-overlay-sub'),
    overlayDead:       prop('--canvas-overlay-dead'),
    // Common
    none: 'transparent',
    // Witch
    witchBroom:        prop('--canvas-witch-broom'),
    witchBristleLight: prop('--canvas-witch-bristle-a'),
    witchBristleDark:  prop('--canvas-witch-bristle-b'),
    witchBristleBand:  prop('--canvas-witch-bristle-band'),
    witchRobe:         prop('--canvas-witch-robe'),
    witchCape:         prop('--canvas-witch-cape'),
    witchSkin:         prop('--canvas-witch-skin'),
    witchSkinLine:     prop('--canvas-witch-skin-line'),
    witchNose:         prop('--canvas-witch-nose'),
    witchEye:          prop('--canvas-witch-eye'),
    witchPupil:        prop('--canvas-witch-pupil'),
    witchHat:          prop('--canvas-witch-hat'),
    witchGold:         prop('--canvas-witch-gold'),
    // Magic sparks (alpha applied via canvas globalAlpha)
    witchSparks: [
      prop('--canvas-witch-spark-0'),
      prop('--canvas-witch-spark-1'),
      prop('--canvas-witch-spark-2'),
      prop('--canvas-witch-spark-3'),
    ],
  };


  // ── Game game.state ───────────────────────────────────────────────────────────────
  const states = {
    IDLE:     'idle',
    RUNNING:  'running',
    DYING:    'dying',
    DEAD:     'dead'
  }

  const game = {
    state:    states.IDLE,
    score:    0,
    hiScore:  0,
    lives:    3,
    frame:    0,
    speed:    5,
    deadFrame: 0, // eating animation counter on the dead screen
  };

  // Panda entity: position, physics, and transient combat game.state.
  // All fields that change together on hit/death/reset are kept here.
  const panda = {
    x:                  80, 
    y:                  GROUND, 
    velocityY:          0, 
    onGround:           true, 
    width:              44, 
    height:             50,
    // invincibility window after a hit
    invincible:         false,
    invincibilityTimer: 0,
    // hit-spin animation
    spinning:           false,
    spinAngle:          0,
    spinFrames:         0,
  };

  // Death animation: panda spins and arcs off screen when all game.lives are lost.
  const death = {
    angle:     0,
    frame:     0,
    y:         0,
    velocityY: 0,
  };

  // Obstacle spawning timers and the active obstacle list.
  const spawner = {
    obstacles:      [],
    bambooTimer:    0,
    bambooInterval: 90,
    witchTimer:     0,
    witchInterval:  300, // first witch appearance
  };

  // Scrolling background elements.
  const world = {
    clouds:  [{ x: 100, y: 22, r: 22 }, { x: 310, y: 35, r: 16 }, { x: 520, y: 18, r: 20 }],
    groundX: 0,
  };

  elements.slider.addEventListener('input', slide);
  elements.jumpButton.addEventListener('click', jump);
  elements.saveButton.addEventListener('click', saveHandle);
  document.addEventListener('keydown', spaceHandle);

  // ── Game logic ───────────────────────────────────────────────────────────────
  function slide(){
    const s = parseInt(this.value) / 100;
    elements.scaleValue.textContent = this.value + '%';
    elements.wrap.style.width       = (WIDTH * s) + 'px';
    elements.wrap.style.height      = (HEIGTH * s) + 'px';
    elements.canvas.style.transform = `scale(${s})`;
  }

  function jump() {
    if (game.state === states.IDLE || game.state === states.DEAD) { 
      startGame(); 
      return; 
    }
    if (game.state === states.DYING) return;

    if (panda.onGround && !panda.spinning) {
      panda.velocityY = -13.5;
      panda.onGround = false;
    }
  }

  function saveHandle() {
    const nick = elements.elements.nickInput.value.trim() || 'Anonymous';

    elements.saveButton.disabled = true;
    elements.saveButton.classList.add('is-saving');
    elements.saveConfirm.textContent = 'Saving...';

    fetch('/api/pandagame/scores', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nick, score: game.score })
    })
    .then(res =>
      res.json()
        .catch(() => ({}))
        .then(data => {
          if (!res.ok) 
            throw new Error(data.error || ('HTTP ' + res.status));
          return data;
        })
    )
    .then(data => {
      elements.saveConfirm.textContent = `Saved: ${data.nick} — ${data.score} pts`;
    })
    .catch(err => {
      console.error('Score save error:', err);
      elements.saveConfirm.textContent = 'Failed to save score: ' + err.message;
      elements.saveButton.disabled = false;
      elements.saveButton.classList.remove('is-saving');
    });
  }

  function spaceHandle(e) {
    if (e.code === 'Space') { 
      e.preventDefault(); 
      jump(); }
  }

  function startGame() {
    Object.assign(game, {
      state: states.RUNNING, score: 0, lives: 3, speed: 5, frame: 0, deadFrame: 0,
    });

    Object.assign(panda, {
      y: GROUND, velocityY: 0, onGround: true,
      invincible: false, invincibilityTimer: 0,
      spinning: false, spinAngle: 0, spinFrames: 0,
    });

    Object.assign(death, { angle: 0, frame: 0, y: GROUND, velocityY: 0 });

    Object.assign(spawner, {
      obstacles:      [],
      bambooTimer:    0,
      bambooInterval: 90,
      witchTimer:     0,
      witchInterval:  300 + Math.random() * 200,
    });

    // save panel — hide on new game
    elements.savePanel.classList.remove('is-visible');
    elements.saveConfirm.textContent = '';
    elements.saveButton.disabled     = false;
    elements.saveButton.classList.remove('is-saving');

    updateUI();
    elements.jumpLabel.textContent = 'Jump';
    elements.hint.textContent      = 'Button or Space = jump';
  }

  function updateUI() {
    elements.scoreDisplay.textContent     = game.score;
    elements.highScoreDisplay.textContent = game.hiScore;
    elements.lifeIcons.forEach((icon, i) => icon.classList.toggle('is-lost', i + 1 > game.lives));
  }

  function spawnBamboo() {
    const h = 28 + Math.random() * 38;
    const w = 13 + Math.random() * 9;
    // 38% chance of double bamboo
    const n = Math.random() < 0.38 ? 2 : 1;
    for (let i = 0; i < n; i++)
      spawner.obstacles.push({ type: 'bamboo', x: WIDTH + i * (w + 10), y: GROUND - h + 8, w, h });
  }

  function spawnWitch() {
    // o.x = witch horizontal centre; o.y = FLY_Y = body vertical centre
    spawner.obstacles.push({ type: 'witch', x: WIDTH + 55, y: WITCH_HEIGTH_SPAWN, anim: 0 });
  }

  // Returns false when an obstacle of a different type is too close to the right edge —
  // the player wouldn't have time to react to both at once.
  function canSpawn(type) {
    const SAFE = 260; // minimum gap (px) between different obstacle types
    for (const o of spawner.obstacles) {
      if (o.type !== type && o.x > WIDTH - SAFE) return false;
    }
    return true;
  }

  function checkHit() {
    if (panda.invincible) return false;
    // panda hitbox shrunk for more "forgiveness"
    const px = panda.x + 10, py = panda.y - panda.height + 12;
    const pw = panda.width - 18, ph = panda.height - 18;
    for (const o of spawner.obstacles) {
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
    if (game.state === states.DYING) {
      death.frame++;
      death.angle = (death.frame / DEATH_SPIN_TOTAL) * Math.PI * 4;
      death.velocityY += 0.5;
      death.y  += death.velocityY;
      if (death.y >= GROUND) death.y = GROUND;
      if (death.frame >= DEATH_SPIN_TOTAL) {
        game.state = states.DEAD;
        elements.jumpLabel.textContent = 'Start';
        elements.hint.textContent   = 'Click Start or Space to play again';
        elements.savePanel.classList.add('is-visible');
        elements.nickInput.focus();
      }
      return;
    }

    if (game.state === states.DEAD)    { game.deadFrame++; return; }
    if (game.state !== states.RUNNING) return;

    // physics
    game.frame++;
    game.score++;
    game.speed = 5 + Math.floor(game.score / 100) * 0.4;

    panda.velocityY += 0.65;
    panda.y  += panda.velocityY;
    if (panda.y >= GROUND) {
      panda.y        = GROUND;
      panda.velocityY       = 0;
      panda.onGround = true;
    }

    // spawner.obstacles
    spawner.bambooTimer++;
    if (spawner.bambooTimer >= spawner.bambooInterval && canSpawn('bamboo')) {
      spawnBamboo();
      spawner.bambooTimer    = 0;
      spawner.bambooInterval = 50 + Math.random() * 50;
    }
    for (const o of spawner.obstacles) {
      o.x -= game.speed;
      if (o.type === 'witch') o.anim++;
    }
    spawner.obstacles = spawner.obstacles.filter(o => o.type === 'witch' ? o.x > -65 : o.x + o.w > -10);

    // witch — appears after game.score > 150
    if (game.score > 150) {
      spawner.witchTimer++;
      if (spawner.witchTimer >= spawner.witchInterval && canSpawn('witch')) {
        spawnWitch();
        spawner.witchTimer    = 0;
        spawner.witchInterval = 280 + Math.random() * 220;
      }
    }

    // world.clouds and ground
    for (const c of world.clouds) {
      c.x -= game.speed * 0.28;
      if (c.x + c.r < 0) { c.x = WIDTH + c.r; c.y = 15 + Math.random() * 45; }
    }
    world.groundX = (world.groundX - game.speed * 0.6 + WIDTH) % WIDTH;

    // hit spin animation
    if (panda.spinning) {
      panda.spinFrames++;
      panda.spinAngle = (panda.spinFrames / SPIN_TOTAL) * Math.PI * 2;
      if (panda.spinFrames >= SPIN_TOTAL) { panda.spinning = false; panda.spinAngle = 0; panda.spinFrames = 0; }
    }

    // invincibility
    if (panda.invincible) { panda.invincibilityTimer--; if (panda.invincibilityTimer <= 0) panda.invincible = false; }

    // collision
    if (checkHit()) {
      game.lives--;
      if (game.score > game.hiScore) game.hiScore = game.score;
      updateUI();

      panda.spinning   = true;
      panda.spinAngle  = 0;
      panda.spinFrames = 0;

      if (game.lives <= 0) {
        game.state      = states.DYING;
        death.frame = 0;
        death.angle = 0;
        death.y     = panda.y - panda.height / 2;
        death.velocityY    = -10;
      } else {
        panda.invincible = true;
        panda.invincibilityTimer   = INV_DUR;
      }
    }

    if (game.frame % 4 === 0) updateUI();
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
    ctx.strokeStyle = colors.pandaBlack;
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
    ctx.fillStyle = colors.overlayBackground;
    ctx.fillRect(0, 0, WIDTH, HEIGTH);
    ctx.textAlign = 'center';
    ctx.fillStyle = colors.overlayTitle;
    ctx.font      = '500 18px sans-serif';
    ctx.fillText(line1, WIDTH / 2, HEIGTH / 2 - 8);
    ctx.fillStyle = colors.overlaySubtitle;
    ctx.font      = '400 13px sans-serif';
    ctx.fillText(line2, WIDTH / 2, HEIGTH / 2 + 16);
  }

  // ── Scene drawing ────────────────────────────────────────────────────────────
  function drawCloud(c) {
    ctx.fillStyle = colors.cloud;
    ctx.beginPath();
    ctx.arc(c.x,              c.y,     c.r,        0, Math.PI * 2);
    ctx.arc(c.x + c.r * 0.75, c.y + 3, c.r * 0.7, 0, Math.PI * 2);
    ctx.arc(c.x - c.r * 0.65, c.y + 5, c.r * 0.6, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawGround() {
    const lineY = GROUND + 8; // y of ground line (used 4×)
    ctx.fillStyle = colors.ground;
    ctx.fillRect(0, lineY, WIDTH, HEIGTH - GROUND);

    ctx.strokeStyle = colors.groundLine;
    ctx.lineWidth   = 1.5;
    ctx.beginPath();
    ctx.moveTo(0, lineY);
    ctx.lineTo(WIDTH, lineY);
    ctx.stroke();

    // animated "grass tufts" scrolling at terrain game.speed
    ctx.fillStyle = colors.ground;
    for (let i = 0; i < 8; i++) {
      const gx = (world.groundX + i * (WIDTH / 8)) % WIDTH;
      ctx.beginPath();
      ctx.arc(gx, lineY, 4, Math.PI, 0);
      ctx.fill();
    }
  }

  function drawBamboo(o) {
    // trzon
    ctx.fillStyle = colors.bambooStem;
    ctx.fillRect(o.x + o.w * 0.3, o.y, o.w * 0.4, o.h);

    // nodes
    ctx.fillStyle = colors.bambooJoint;
    const segs = Math.floor(o.h / 13);
    for (let i = 0; i <= segs; i++)
      ctx.fillRect(o.x + o.w * 0.18, o.y + i * 13 - 2, o.w * 0.64, 3);

    // listki na czubku
    ctx.fillStyle = colors.bambooLeaf;
    ctx.beginPath(); ctx.ellipse(o.x + o.w * 0.3 - 9, o.y - 5,  13, 5, -0.4, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(o.x + o.w * 0.7 + 7, o.y - 3,  11, 4.5, 0.5, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(o.x + o.w * 0.5,     o.y - 11,  9, 4,   0,   0, Math.PI * 2); ctx.fill();
  }

  // ── Witch drawing ────────────────────────────────────────────────────────────
  // wx, wy = horizontal and vertical body centre; anim = animation game.frame counter
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
    ctx.strokeStyle = colors.witchBroom;
    ctx.lineWidth   = 3.5;
    ctx.beginPath();
    ctx.moveTo(wx - 32, y + 10);
    ctx.lineTo(wx + 36, y + 5);
    ctx.stroke();

    // Broom bristles — fan on the right side (exit side)
    for (let i = -5; i <= 5; i++) {
      ctx.strokeStyle = i % 2 === 0 ? colors.witchBristleLight : colors.witchBristleDark;
      ctx.lineWidth   = 1.5;
      ctx.beginPath();
      ctx.moveTo(wx + 34, broomY);
      ctx.lineTo(wx + 50, broomY + i * 3);
      ctx.stroke();
    }
    // Binding holding the bristles
    ctx.strokeStyle = colors.witchBristleBand;
    ctx.lineWidth   = 3;
    ctx.beginPath();
    ctx.moveTo(bindX, y + 3);
    ctx.lineTo(bindX, y + 12);
    ctx.stroke();

    // --- Robe / body ---
    setOutline(1.2);
    ctx.fillStyle = colors.witchRobe;
    ctx.beginPath();
    ctx.ellipse(wx, y + 12, 12, 17, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    // Cape (fluttering in flight — on the left side, behind the back)
    const flap = Math.sin(anim * 0.18) * 7;
    ctx.fillStyle = colors.witchCape;
    ctx.beginPath();
    ctx.moveTo(wx - 6, y);
    ctx.quadraticCurveTo(wx - 28, y + 4 - flap, wx - 24, y + 24);
    ctx.lineTo(wx - 3, y + 22);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    // --- Head (facing left) ---
    ctx.fillStyle   = colors.witchSkin;
    ctx.strokeStyle = colors.witchSkinLine;
    ctx.lineWidth   = 1.2;
    ctx.beginPath();
    ctx.arc(headCX, y - 8, 13, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    // Nose (pointed, typical witch nose, facing left)
    ctx.fillStyle   = colors.witchNose;
    ctx.strokeStyle = colors.witchSkinLine;
    ctx.lineWidth   = 1;
    ctx.beginPath();
    ctx.moveTo(wx - 13, y - 5);
    ctx.lineTo(wx - 23, y - 2);
    ctx.lineTo(wx - 12, y - 2);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    // Eye (green glow)
    ctx.strokeStyle = colors.none;
    ctx.fillStyle   = colors.witchEye;
    ctx.beginPath(); ctx.arc(wx - 9, y - 9, 3.5, 0, Math.PI * 2); ctx.fill();
    // Pupil
    ctx.fillStyle = colors.witchPupil;
    ctx.beginPath(); ctx.arc(wx - 10, y - 9, 1.8, 0, Math.PI * 2); ctx.fill();
    // Occasional blink
    if (Math.sin(anim * 0.06) > 0.92) {
      ctx.fillStyle = colors.witchSkin;
      ctx.fillRect(wx - 14, y - 11, 9, 5);
    }

    // --- Hat ---
    setOutline(1.5);
    ctx.fillStyle = colors.witchHat;
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
    ctx.strokeStyle = colors.none;
    ctx.fillStyle   = colors.witchGold;
    ctx.fillRect(wx - 6, y - 29, 9, 6);
    ctx.fillStyle = colors.witchHat;
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
        ctx.fillStyle   = colors.witchSparks[i];
        ctx.beginPath(); ctx.arc(sx, sy, sr, 0, Math.PI * 2); ctx.fill();
      }
    }

    ctx.restore();
  }

  // ── Panda drawing ────────────────────────────────────────────────────────────

  // Running / jumping panda. px, py = top-left corner (py = panda.y - panda.h)
  function drawPandaRunning(px, py) {
    const leg = Math.sin(game.frame * 0.28) * 6;
    setOutline(1.5);

    // legs (swinging in sync with game.frame)
    fillEllipse(px + 11, py + 46 + (panda.onGround ?  leg : 0), 8, 7,  0.2, colors.pandaBlack);
    fillEllipse(px + 31, py + 46 + (panda.onGround ? -leg : 0), 8, 7, -0.2, colors.pandaBlack);

    // torso
    fillEllipse(px + 22, py + 32, 18, 21, 0, colors.pandaWhite);

    // arms
    fillEllipse(px + 5,  py + 28 + (panda.onGround ?  leg * 0.3 : -4), 6, 10,  0.5, colors.pandaBlack);
    fillEllipse(px + 39, py + 28 + (panda.onGround ? -leg * 0.3 : -4), 6, 10, -0.5, colors.pandaBlack);

    // head
    fillArc(px + 22, py + 15, 16, colors.pandaWhite);

    // ears
    fillArc(px + 9,  py + 4, 6,   colors.pandaBlack);
    fillArc(px + 35, py + 4, 6,   colors.pandaBlack);
    setOutline(1);
    fillArc(px + 9,  py + 4, 3.5, colors.pandaEar);
    fillArc(px + 35, py + 4, 3.5, colors.pandaEar);

    // eye patches
    setOutline(1.5);
    fillEllipse(px + 14, py + 14, 5.5, 4.5, -0.3, colors.pandaDark);
    fillEllipse(px + 30, py + 14, 5.5, 4.5,  0.3, colors.pandaDark);

    // eye whites
    setOutline(1);
    fillArc(px + 14, py + 14, 2.5, colors.pandaEye);
    fillArc(px + 30, py + 14, 2.5, colors.pandaEye);

    // pupils (no outline)
    ctx.strokeStyle = colors.none;
    fillArc(px + 14.5, py + 14, 1.2, colors.pandaBlack);
    fillArc(px + 30.5, py + 14, 1.2, colors.pandaBlack);

    // nose and smile
    setOutline(1.5);
    fillEllipse(px + 22, py + 20, 3, 2, 0, colors.pandaDark);
    ctx.strokeStyle = colors.pandaDark; ctx.lineWidth = 1.5;
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
    fillEllipse(cx + 10, cy + 50 + bob, 11, 7,  0.6, colors.pandaBlack);
    fillEllipse(cx + 36, cy + 50 + bob, 11, 7, -0.6, colors.pandaBlack);

    // torso
    fillEllipse(cx + 22, cy + 36 + bob, 18, 20, 0, colors.pandaWhite);

    // arms (swaying with body)
    fillEllipse(cx + 7,  cy + 28 + bob, 6, 11,  0.9, colors.pandaBlack);
    fillEllipse(cx + 37, cy + 26 + bob, 6, 11, -0.9, colors.pandaBlack);

    // head
    fillArc(cx + 22, cy + 15 + bob, 16, colors.pandaWhite);

    // ears
    fillArc(cx + 9,  cy + 3 + bob, 6,   colors.pandaBlack);
    fillArc(cx + 35, cy + 3 + bob, 6,   colors.pandaBlack);
    setOutline(1);
    fillArc(cx + 9,  cy + 3 + bob, 3.5, colors.pandaEar);
    fillArc(cx + 35, cy + 3 + bob, 3.5, colors.pandaEar);

    // eye patches
    setOutline(1.5);
    fillEllipse(cx + 14, cy + 13 + bob, 5.5, 4.5, -0.3, colors.pandaDark);
    fillEllipse(cx + 30, cy + 13 + bob, 5.5, 4.5,  0.3, colors.pandaDark);

    // eyes open and close in rhythm with chewing
    const eyeOpen = sSin > 0;
    setOutline(1);
    if (eyeOpen) {
      fillArc(cx + 14, cy + 13 + bob, 2.5, colors.pandaEye);
      fillArc(cx + 30, cy + 13 + bob, 2.5, colors.pandaEye);
      ctx.strokeStyle = colors.none;
      fillArc(cx + 14.5, cy + 13 + bob, 1.2, colors.pandaBlack);
      fillArc(cx + 30.5, cy + 13 + bob, 1.2, colors.pandaBlack);
    } else {
      // squinted eyes ^^
      ctx.strokeStyle = colors.pandaEye; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(cx + 14, cy + 13 + bob, 2.5, Math.PI, 0); ctx.stroke();
      ctx.beginPath(); ctx.arc(cx + 30, cy + 13 + bob, 2.5, Math.PI, 0); ctx.stroke();
    }

    // nose
    setOutline(1.5);
    fillEllipse(cx + 22, cy + 19 + bob, 3, 2, 0, colors.pandaDark);

    // animated muzzle (chewing motion)
    ctx.strokeStyle = colors.pandaDark; ctx.lineWidth = 1.8;
    ctx.beginPath(); ctx.arc(cx + 22, cy + 21 + bob + chew, 4, 0, Math.PI); ctx.stroke();

    // blush
    ctx.strokeStyle = colors.none;
    ctx.fillStyle   = colors.pandaBlush;
    ctx.beginPath(); ctx.ellipse(cx + 10, cy + 18 + bob, 5, 3, 0, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(cx + 34, cy + 18 + bob, 5, 3, 0, 0, Math.PI * 2); ctx.fill();

    // bamboo in paws (gently swaying)
    ctx.save();
    ctx.translate(cx + 22, cy + 27);
    ctx.rotate(Math.PI / 2);
    ctx.fillStyle   = colors.bambooStem; ctx.fillRect(-3 + bshift, -21, 6, 42);
    ctx.strokeStyle = colors.pandaDark;      ctx.lineWidth = 1.5;
    ctx.strokeRect(-3 + bshift, -21, 6, 42);
    ctx.fillStyle = colors.bambooJoint;
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
    ctx.clearRect(0, 0, WIDTH, HEIGTH);
    ctx.fillStyle = colors.sky;
    ctx.fillRect(0, 0, WIDTH, HEIGTH);

    for (const c of world.clouds) drawCloud(c);

    // witches — sky layer (in front of ground, behind world.clouds)
    for (const o of spawner.obstacles) {
      if (o.type === 'witch') drawWitch(o.x, o.y, o.anim);
    }

    drawGround();

    // bamboos — ground layer
    for (const o of spawner.obstacles) {
      if (o.type === 'bamboo') drawBamboo(o);
    }

    const pandaTop = panda.y - panda.height;
    const pandaCX  = panda.x + panda.width / 2;
    const pandaCY  = panda.y - panda.height / 2;

    if (game.state === states.RUNNING) {
      if (panda.spinning) {
        drawRotated(pandaCX, pandaCY, panda.spinAngle, () => drawPandaRunning(panda.x, pandaTop));
      } else if (!(panda.invincible && Math.floor(panda.invincibilityTimer / 5) % 2 === 0)) {
        // flash when panda.invincible
        drawPandaRunning(panda.x, pandaTop);
      }
    } else if (game.state === states.DYING) {
      drawRotated(pandaCX, death.y, death.angle, () => drawPandaRunning(panda.x, death.y - panda.height / 2));
    } else if (game.state === states.IDLE) {
      drawPandaRunning(panda.x, pandaTop);
      drawOverlay(
        'Click button below to start',
        'Jump = Space / button   |   Witch flies high — DON\'T jump!'
      );
    } else if (game.state === states.DEAD) {
      // panda drawn twice: once under the overlay, once above it
      const EX = WIDTH / 2 - 22, EY = HEIGTH / 2 - 58;
      drawPandaEating(EX, EY, game.deadFrame);
      ctx.fillStyle = colors.overlayBackground;
      ctx.fillRect(0, 0, WIDTH, HEIGTH);
      drawPandaEating(EX, EY, game.deadFrame);

      ctx.textAlign = 'center';
      ctx.fillStyle = colors.overlayTitle; ctx.font = '500 17px sans-serif';
      ctx.fillText('Game over!  Score: ' + game.score, WIDTH / 2, HEIGTH / 2 + 36);
      ctx.fillStyle = colors.overlayDead;  ctx.font = '400 12px sans-serif';
      ctx.fillText('Click Start to play again', WIDTH / 2, HEIGTH / 2 + 56);
    }
  }

  function loop() {
    update();
    draw();
    requestAnimationFrame(loop);
  }
  loop();
})();