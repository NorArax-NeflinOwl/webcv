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
  // Czarownica leci na tej wysokości (środek ciała); panda na ziemi jest bezpieczna,
  // panda przy szczycie skoku koliduje — trzeba NIE skakać gdy jedzie czarownica.
  const FLY_Y  = 120;

  // ── Paleta kolorów canvasu ────────────────────────────────────────────────────
  // Scena
  const C_SKY          = '#94c1e8';
  const C_CLOUD        = 'rgba(255,255,255,0.72)';
  const C_GROUND       = '#c8e6c9';
  const C_GROUND_LINE  = '#81c784';
  // Bambus
  const C_BAMBOO_STEM  = '#558b2f';
  const C_BAMBOO_JOINT = '#33691e';
  const C_BAMBOO_LEAF  = '#7cb342';
  // Panda
  const C_P_WHITE = '#f0f0f0';
  const C_P_BLACK = '#111';
  const C_P_DARK  = '#222';
  const C_P_EAR   = '#ee77d3';
  const C_P_BELLY = '#ddd';
  const C_P_EYE   = '#fff';
  const C_P_BLUSH = 'rgba(150,150,150,0.28)';
  // Nakładka (overlay) i ekran dead
  const C_OV_BG    = 'rgba(240,247,238,0.82)';
  const C_OV_TITLE = '#2e7d32';
  const C_OV_SUB   = '#66bb6a';
  const C_OV_DEAD  = '#81c784';
  // Wspólne
  const C_NONE = 'transparent';
  // Czarownica
  const C_W_BROOM        = '#7B4F2E';
  const C_W_BRISTLE_A    = '#D4A017';
  const C_W_BRISTLE_B    = '#B88C10';
  const C_W_BRISTLE_BAND = '#5C3D00';
  const C_W_ROBE         = '#3B0764';
  const C_W_CAPE         = '#2A0050';
  const C_W_SKIN         = '#F0C080';
  const C_W_SKIN_LINE    = '#A07030';
  const C_W_NOSE         = '#C89060';
  const C_W_EYE          = '#33DD33';
  const C_W_PUPIL        = '#001100';
  const C_W_HAT          = '#160030';
  const C_W_GOLD         = '#FFD700';

  // ── Skalowanie płótna ────────────────────────────────────────────────────────
  slider.addEventListener('input', function () {
    const s = parseInt(this.value) / 100;
    scaleVal.textContent = this.value + '%';
    wrap.style.width  = (W * s) + 'px';
    wrap.style.height = (H * s) + 'px';
    canvas.style.transform       = `scale(${s})`;
    canvas.style.transformOrigin = 'top left';
  });

  // ── Stan gry ─────────────────────────────────────────────────────────────────
  // states: idle | running | dying | dead
  let state   = 'idle';
  let score   = 0;
  let hiScore = 0;
  let lives   = 3;
  let frame   = 0;
  let speed   = 5;

  // Nietykalność po uderzeniu
  let invincible = false;
  let invTimer   = 0;
  const INV_DUR  = 110;

  // Animacja obrotu po uderzeniu (gdy zostały życia)
  let spinning    = false;
  let spinAngle   = 0;
  let spinFrames  = 0;
  const SPIN_TOTAL = 50;

  // Animacja śmierci: obrót + wylot w górę i upadek
  let deathAngle = 0;
  let deathFrame = 0;
  let deathY     = 0;
  let deathVY    = 0;
  const DEATH_SPIN_TOTAL = 70;

  // Licznik animacji jedzenia na ekranie "dead" (nieskończona)
  let deadFrame = 0;

  const panda = { x: 80, y: GROUND, vy: 0, onGround: true, width: 44, height: 50 };

  let obstacles    = [];
  let obsTimer     = 0;
  let obsInterval  = 90;

  let witchTimer    = 0;
  let witchInterval = 300; // pierwsze pojawienie się czarownicy

  let clouds  = [{ x: 100, y: 22, r: 22 }, { x: 310, y: 35, r: 16 }, { x: 520, y: 18, r: 20 }];
  let groundX = 0;

  // ── Logika gry ───────────────────────────────────────────────────────────────
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
    const nick = nickInput.value.trim() || 'Anonim';

    saveBtn.disabled    = true;
    saveBtn.style.opacity = '0.5';
    saveConfirm.textContent = 'Zapisywanie...';

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
          saveConfirm.textContent = `Zapisano: ${data.nick} — ${data.score} pkt`;
        })
        .catch(err => {
          console.error('Błąd zapisu wyniku:', err);
          saveConfirm.textContent = 'Nie udało się zapisać wyniku: ' + err.message;
          saveBtn.disabled      = false;
          saveBtn.style.opacity = '1';
        });
  });

  function startGame() {
    // stan gry
    state     = 'running';
    score     = 0;
    lives     = 3;
    speed     = 5;
    frame     = 0;
    deadFrame = 0;

    // nietykalność
    invincible = false;
    invTimer   = 0;

    // obrót po uderzeniu
    spinning   = false;
    spinAngle  = 0;
    spinFrames = 0;

    // animacja śmierci
    deathAngle = 0;
    deathFrame = 0;
    deathY     = GROUND;
    deathVY    = 0;

    // panel zapisu — ukryj przy nowej grze
    saveWrap.style.display  = 'none';
    saveConfirm.textContent = '';
    saveBtn.disabled        = false;
    saveBtn.style.opacity   = '1';

    // świat
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
    btnLabel.textContent = 'Skocz';
    hintEl.textContent   = 'Przycisk lub Spacja = skok';
  }

  function updateUI() {
    document.getElementById('sc').textContent = score;
    document.getElementById('hi').textContent = hiScore;
    for (let i = 1; i <= 3; i++) {
      const el = document.getElementById('lv' + i);
      el.style.opacity   = i <= lives ? '1' : '0.2';
      el.style.transform = i <= lives ? 'scale(1)' : 'scale(0.7)';
    }
  }

  function spawnBamboo() {
    const h = 28 + Math.random() * 38;
    const w = 13 + Math.random() * 9;
    // 38% szansa na podwójny bambus
    const n = Math.random() < 0.38 ? 2 : 1;
    for (let i = 0; i < n; i++)
      obstacles.push({ type: 'bamboo', x: W + i * (w + 10), y: GROUND - h + 8, w, h });
  }

  function spawnWitch() {
    // o.x = poziomy środek czarownicy; o.y = FLY_Y = środek ciała w pionie
    obstacles.push({ type: 'witch', x: W + 55, y: FLY_Y, anim: 0 });
  }

  // Zwraca false gdy obstacle innego typu jest zbyt blisko prawej krawędzi —
  // gracz nie zdążyłby zareagować na oba naraz.
  function canSpawn(type) {
    const SAFE = 260; // minimalna przerwa (px) między różnymi typami przeszkód
    for (const o of obstacles) {
      if (o.type !== type && o.x > W - SAFE) return false;
    }
    return true;
  }

  function checkHit() {
    if (invincible) return false;
    // hitbox pandy zmniejszony dla większego „wybaczenia"
    const px = panda.x + 10, py = panda.y - panda.height + 12;
    const pw = panda.width - 18, ph = panda.height - 18;
    for (const o of obstacles) {
      if (o.type === 'witch') {
        // hitbox obejmuje ciało + dolną część kapelusza
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
        btnLabel.textContent   = 'Start';
        hintEl.textContent     = 'Kliknij Start lub Spacja aby zagrać ponownie';
        saveWrap.style.display = 'flex';
        nickInput.focus();
      }
      return;
    }

    if (state === 'dead')    { deadFrame++; return; }
    if (state !== 'running') return;

    // fizyka
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

    // przeszkody
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

    // czarownica — pojawia się po score > 150
    if (score > 150) {
      witchTimer++;
      if (witchTimer >= witchInterval && canSpawn('witch')) {
        spawnWitch();
        witchTimer    = 0;
        witchInterval = 280 + Math.random() * 220;
      }
    }

    // chmury i ziemia
    for (const c of clouds) {
      c.x -= speed * 0.28;
      if (c.x + c.r < 0) { c.x = W + c.r; c.y = 15 + Math.random() * 45; }
    }
    groundX = (groundX - speed * 0.6 + W) % W;

    // animacja obrotu po uderzeniu
    if (spinning) {
      spinFrames++;
      spinAngle = (spinFrames / SPIN_TOTAL) * Math.PI * 2;
      if (spinFrames >= SPIN_TOTAL) { spinning = false; spinAngle = 0; spinFrames = 0; }
    }

    // nietykalność
    if (invincible) { invTimer--; if (invTimer <= 0) invincible = false; }

    // kolizja
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

  // ── Helpery rysowania ────────────────────────────────────────────────────────

  // Wypełniona elipsa z obrysem
  function fillEllipse(x, y, rx, ry, rot, fill) {
    ctx.fillStyle = fill;
    ctx.beginPath();
    ctx.ellipse(x, y, rx, ry, rot, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }

  // Wypełnione koło z obrysem
  function fillArc(x, y, r, fill) {
    ctx.fillStyle = fill;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }

  // Ustawia kolor i grubość obrysu na czarny
  function setOutline(w) {
    ctx.strokeStyle = C_P_BLACK;
    ctx.lineWidth   = w;
  }

  // Obraca kontekst wokół punktu (cx, cy), rysuje fn(), przywraca transform
  function drawRotated(cx, cy, angle, fn) {
    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate(angle);
    ctx.translate(-cx, -cy);
    fn();
    ctx.restore();
  }

  // Półprzeźroczysta nakładka + dwulinijkowy tekst na środku płótna
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

  // ── Rysowanie sceny ──────────────────────────────────────────────────────────

  function drawCloud(c) {
    ctx.fillStyle = C_CLOUD;
    ctx.beginPath();
    ctx.arc(c.x,              c.y,     c.r,        0, Math.PI * 2);
    ctx.arc(c.x + c.r * 0.75, c.y + 3, c.r * 0.7, 0, Math.PI * 2);
    ctx.arc(c.x - c.r * 0.65, c.y + 5, c.r * 0.6, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawGround() {
    const lineY = GROUND + 8; // y linii ziemi (używane 4×)
    ctx.fillStyle = C_GROUND;
    ctx.fillRect(0, lineY, W, H - GROUND);

    ctx.strokeStyle = C_GROUND_LINE;
    ctx.lineWidth   = 1.5;
    ctx.beginPath();
    ctx.moveTo(0, lineY);
    ctx.lineTo(W, lineY);
    ctx.stroke();

    // animowane "trawki" przesuwające się z prędkością terenu
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

    // węzły
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

  // ── Rysowanie czarownicy ─────────────────────────────────────────────────────
  // wx, wy = poziomy i pionowy środek ciała; anim = licznik klatek animacji
  function drawWitch(wx, wy, anim) {
    const bob = Math.sin(anim * 0.12) * 2.5; // delikatne unoszenie się
    const y   = wy + bob;

    // Pozycje używane wielokrotnie wewnątrz funkcji
    const headCX   = wx - 1;  // środek x głowy = środek x ronda kapelusza
    const hatBaseY = y - 20;  // podstawa stożka i ronda kapelusza (3×)
    const broomY   = y + 7;   // y podstawy włosia i y iskier (3×)
    const bindX    = wx + 32; // x opaski miotły (2×)

    const SPARK_COLS = [
      'rgba(180,0,255,', 'rgba(255,140,0,',
      'rgba(0,200,255,', 'rgba(255,255,50,'
    ];

    ctx.save();

    // --- Miotła ---
    ctx.lineCap     = 'round';
    ctx.strokeStyle = C_W_BROOM;
    ctx.lineWidth   = 3.5;
    ctx.beginPath();
    ctx.moveTo(wx - 32, y + 10);
    ctx.lineTo(wx + 36, y + 5);
    ctx.stroke();

    // Włosie miotły — wachlarz po prawej stronie (strona wyjazdu)
    for (let i = -5; i <= 5; i++) {
      ctx.strokeStyle = i % 2 === 0 ? C_W_BRISTLE_A : C_W_BRISTLE_B;
      ctx.lineWidth   = 1.5;
      ctx.beginPath();
      ctx.moveTo(wx + 34, broomY);
      ctx.lineTo(wx + 50, broomY + i * 3);
      ctx.stroke();
    }
    // Opaska trzymająca włosie
    ctx.strokeStyle = C_W_BRISTLE_BAND;
    ctx.lineWidth   = 3;
    ctx.beginPath();
    ctx.moveTo(bindX, y + 3);
    ctx.lineTo(bindX, y + 12);
    ctx.stroke();

    // --- Szata / ciało ---
    setOutline(1.2);
    ctx.fillStyle = C_W_ROBE;
    ctx.beginPath();
    ctx.ellipse(wx, y + 12, 12, 17, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    // Peleryna (łopocze w locie — z lewej strony, za plecami)
    const flap = Math.sin(anim * 0.18) * 7;
    ctx.fillStyle = C_W_CAPE;
    ctx.beginPath();
    ctx.moveTo(wx - 6, y);
    ctx.quadraticCurveTo(wx - 28, y + 4 - flap, wx - 24, y + 24);
    ctx.lineTo(wx - 3, y + 22);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    // --- Głowa (skierowana w lewo) ---
    ctx.fillStyle   = C_W_SKIN;
    ctx.strokeStyle = C_W_SKIN_LINE;
    ctx.lineWidth   = 1.2;
    ctx.beginPath();
    ctx.arc(headCX, y - 8, 13, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    // Nos (szpiczasty, typowy dla wiedźmy, w lewo)
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

    // Oko (zielona poświata)
    ctx.strokeStyle = C_NONE;
    ctx.fillStyle   = C_W_EYE;
    ctx.beginPath(); ctx.arc(wx - 9, y - 9, 3.5, 0, Math.PI * 2); ctx.fill();
    // Źrenica
    ctx.fillStyle = C_W_PUPIL;
    ctx.beginPath(); ctx.arc(wx - 10, y - 9, 1.8, 0, Math.PI * 2); ctx.fill();
    // Mrugnięcie co jakiś czas
    if (Math.sin(anim * 0.06) > 0.92) {
      ctx.fillStyle = C_W_SKIN;
      ctx.fillRect(wx - 14, y - 11, 9, 5);
    }

    // --- Kapelusz ---
    setOutline(1.5);
    ctx.fillStyle = C_W_HAT;
    // Stożek kapelusza (lekko pochylony w lewo)
    ctx.beginPath();
    ctx.moveTo(wx - 15, hatBaseY);
    ctx.lineTo(wx - 2,  y - 50);
    ctx.lineTo(wx + 13, hatBaseY);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    // Rondo
    ctx.beginPath();
    ctx.ellipse(headCX, hatBaseY, 17, 5, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    // Złota sprzączka
    ctx.strokeStyle = C_NONE;
    ctx.fillStyle   = C_W_GOLD;
    ctx.fillRect(wx - 6, y - 29, 9, 6);
    ctx.fillStyle = C_W_HAT;
    ctx.fillRect(wx - 4, y - 28, 5, 4);

    // --- Magiczne iskry za miotłą ---
    for (let i = 0; i < 4; i++) {
      const sp = (anim + i * 11) % 44;
      if (sp < 30) {
        const alpha = (30 - sp) / 30;
        const sx    = wx - 20 - sp * 0.9;
        const sy    = broomY + Math.sin(sp * 0.32 + i * 1.8) * 8;
        ctx.fillStyle = SPARK_COLS[i] + alpha + ')';
        const sr = Math.max(0, 3 - sp * 0.07);
        ctx.beginPath(); ctx.arc(sx, sy, sr, 0, Math.PI * 2); ctx.fill();
      }
    }

    ctx.restore();
  }

  // ── Rysowanie pandy ──────────────────────────────────────────────────────────

  // Panda biegnąca / skacząca. px, py = lewy górny róg (py = panda.y - panda.h)
  function drawPandaRunning(px, py) {
    const leg = Math.sin(frame * 0.28) * 6;
    setOutline(1.5);

    // nogi (huśtanie synchro z frame)
    fillEllipse(px + 11, py + 46 + (panda.onGround ?  leg : 0), 8, 7,  0.2, C_P_BLACK);
    fillEllipse(px + 31, py + 46 + (panda.onGround ? -leg : 0), 8, 7, -0.2, C_P_BLACK);

    // tułów
    fillEllipse(px + 22, py + 32, 18, 21, 0, C_P_WHITE);

    // ramiona
    fillEllipse(px + 5,  py + 28 + (panda.onGround ?  leg * 0.3 : -4), 6, 10,  0.5, C_P_BLACK);
    fillEllipse(px + 39, py + 28 + (panda.onGround ? -leg * 0.3 : -4), 6, 10, -0.5, C_P_BLACK);

    // głowa
    fillArc(px + 22, py + 15, 16, C_P_WHITE);

    // uszy
    fillArc(px + 9,  py + 4, 6,   C_P_BLACK);
    fillArc(px + 35, py + 4, 6,   C_P_BLACK);
    setOutline(1);
    fillArc(px + 9,  py + 4, 3.5, C_P_EAR);
    fillArc(px + 35, py + 4, 3.5, C_P_EAR);

    // łaty oczu
    setOutline(1.5);
    fillEllipse(px + 14, py + 14, 5.5, 4.5, -0.3, C_P_DARK);
    fillEllipse(px + 30, py + 14, 5.5, 4.5,  0.3, C_P_DARK);

    // białka oczu
    setOutline(1);
    fillArc(px + 14, py + 14, 2.5, C_P_EYE);
    fillArc(px + 30, py + 14, 2.5, C_P_EYE);

    // źrenice (bez obrysu)
    ctx.strokeStyle = C_NONE;
    fillArc(px + 14.5, py + 14, 1.2, C_P_BLACK);
    fillArc(px + 30.5, py + 14, 1.2, C_P_BLACK);

    // nos i uśmiech
    setOutline(1.5);
    fillEllipse(px + 22, py + 20, 3, 2, 0, C_P_DARK);
    ctx.strokeStyle = C_P_DARK; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.arc(px + 22, py + 21, 3.5, 0.1, Math.PI - 0.1); ctx.stroke();
  }

  // Panda jedząca bambus (ekran game-over). cx, cy = lewy górny róg
  function drawPandaEating(cx, cy, animFrame) {
    const sSin   = Math.sin(animFrame * 0.18); // wspólny sinus dla chew, bshift i eyeOpen (3×)
    const chew   = sSin * 1.5;
    const bob    = Math.sin(animFrame * 0.09) * 1.2;
    const bshift = sSin * 0.8; // przesunięcie bambusa w łapach

    setOutline(1.5);

    // nogi
    fillEllipse(cx + 10, cy + 50 + bob, 11, 7,  0.6, C_P_BLACK);
    fillEllipse(cx + 36, cy + 50 + bob, 11, 7, -0.6, C_P_BLACK);

    // tułów z brzuchem
    fillEllipse(cx + 22, cy + 36 + bob, 18, 20, 0, C_P_WHITE);
    fillEllipse(cx + 22, cy + 39 + bob, 10, 12, 0, C_P_BELLY);

    // ramiona (kołyszą się z ciałem)
    fillEllipse(cx + 7,  cy + 28 + bob, 6, 11,  0.9, C_P_BLACK);
    fillEllipse(cx + 37, cy + 26 + bob, 6, 11, -0.9, C_P_BLACK);

    // głowa
    fillArc(cx + 22, cy + 15 + bob, 16, C_P_WHITE);

    // uszy
    fillArc(cx + 9,  cy + 3 + bob, 6,   C_P_BLACK);
    fillArc(cx + 35, cy + 3 + bob, 6,   C_P_BLACK);
    setOutline(1);
    fillArc(cx + 9,  cy + 3 + bob, 3.5, C_P_EAR);
    fillArc(cx + 35, cy + 3 + bob, 3.5, C_P_EAR);

    // łaty oczu
    setOutline(1.5);
    fillEllipse(cx + 14, cy + 13 + bob, 5.5, 4.5, -0.3, C_P_DARK);
    fillEllipse(cx + 30, cy + 13 + bob, 5.5, 4.5,  0.3, C_P_DARK);

    // oczy otwierają się i zamykają w rytm żucia
    const eyeOpen = sSin > 0;
    setOutline(1);
    if (eyeOpen) {
      fillArc(cx + 14, cy + 13 + bob, 2.5, C_P_EYE);
      fillArc(cx + 30, cy + 13 + bob, 2.5, C_P_EYE);
      ctx.strokeStyle = C_NONE;
      fillArc(cx + 14.5, cy + 13 + bob, 1.2, C_P_BLACK);
      fillArc(cx + 30.5, cy + 13 + bob, 1.2, C_P_BLACK);
    } else {
      // oczy przymrużone ^^
      ctx.strokeStyle = C_P_EYE; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(cx + 14, cy + 13 + bob, 2.5, Math.PI, 0); ctx.stroke();
      ctx.beginPath(); ctx.arc(cx + 30, cy + 13 + bob, 2.5, Math.PI, 0); ctx.stroke();
    }

    // nos
    setOutline(1.5);
    fillEllipse(cx + 22, cy + 19 + bob, 3, 2, 0, C_P_DARK);

    // animowany pysk (ruch żucia)
    ctx.strokeStyle = C_P_DARK; ctx.lineWidth = 1.8;
    ctx.beginPath(); ctx.arc(cx + 22, cy + 21 + bob + chew, 4, 0, Math.PI); ctx.stroke();

    // rumieniec
    ctx.strokeStyle = C_NONE;
    ctx.fillStyle   = C_P_BLUSH;
    ctx.beginPath(); ctx.ellipse(cx + 10, cy + 18 + bob, 5, 3, 0, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(cx + 34, cy + 18 + bob, 5, 3, 0, 0, Math.PI * 2); ctx.fill();

    // bambus w łapach (lekko się kołysze)
    ctx.save();
    ctx.translate(cx + 22, cy + 27);
    ctx.rotate(Math.PI / 2);
    ctx.fillStyle   = C_BAMBOO_STEM; ctx.fillRect(-3 + bshift, -21, 6, 42);
    ctx.strokeStyle = C_P_DARK;      ctx.lineWidth = 1.5;
    ctx.strokeRect(-3 + bshift, -21, 6, 42);
    ctx.fillStyle = C_BAMBOO_JOINT;
    for (let i = 0; i < 3; i++) ctx.fillRect(-5 + bshift, -19 + i * 13, 10, 3);
    ctx.restore();

    // okruszki bambusa
    setOutline(1.5);
    const p = animFrame % 40;
    if (p < 20) {
      ctx.fillStyle = `rgba(100,100,100,${(20 - p) / 20 * 0.5})`;
      ctx.beginPath(); ctx.arc(cx + 26 + p * 0.4, cy + 22 + bob - p * 0.3, 2,   0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(cx + 24 + p * 0.3, cy + 20 + bob - p * 0.5, 1.5, 0, Math.PI * 2); ctx.fill();
    }
  }

  // ── Główna pętla renderowania ─────────────────────────────────────────────────
  function draw() {
    // niebo
    ctx.clearRect(0, 0, W, H);
    ctx.fillStyle = C_SKY;
    ctx.fillRect(0, 0, W, H);

    for (const c of clouds) drawCloud(c);

    // czarownice — warstwa nieba (przed ziemią, za chmurami)
    for (const o of obstacles) {
      if (o.type === 'witch') drawWitch(o.x, o.y, o.anim);
    }

    drawGround();

    // bambusy — warstwa ziemi
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
        // miganie gdy nietykalny
        drawPandaRunning(panda.x, pandaTop);
      }

    } else if (state === 'dying') {
      drawRotated(pandaCX, deathY, deathAngle, () => drawPandaRunning(panda.x, deathY - panda.height / 2));

    } else if (state === 'idle') {
      drawPandaRunning(panda.x, pandaTop);
      drawOverlay(
        'Kliknij przycisk poniżej aby zacząć',
        'Skok = Spacja / przycisk   |   Czarownica leci wysoko — NIE skacz!'
      );

    } else if (state === 'dead') {
      // panda jest rysowana dwa razy: raz pod nakładką, raz nad nią
      const EX = W / 2 - 22, EY = H / 2 - 58;
      drawPandaEating(EX, EY, deadFrame);
      ctx.fillStyle = C_OV_BG;
      ctx.fillRect(0, 0, W, H);
      drawPandaEating(EX, EY, deadFrame);

      ctx.textAlign = 'center';
      ctx.fillStyle = C_OV_TITLE; ctx.font = '500 17px sans-serif';
      ctx.fillText('Koniec gry!  Wynik: ' + score, W / 2, H / 2 + 36);
      ctx.fillStyle = C_OV_DEAD;  ctx.font = '400 12px sans-serif';
      ctx.fillText('Kliknij Start aby zagrać ponownie', W / 2, H / 2 + 56);
    }
  }

  function loop() {
    update();
    draw();
    requestAnimationFrame(loop);
  }
  loop();
})();
