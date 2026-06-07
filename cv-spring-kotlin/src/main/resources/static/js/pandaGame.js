(function(){
    const canvas = document.getElementById('gc');
    const ctx = canvas.getContext('2d');
    const wrap = document.getElementById('canvas-wrap');
    const slider = document.getElementById('scaleSlider');
    const scaleVal = document.getElementById('scale-val');

    const W = 884, H = 300;
    const GROUND = H - 36;

    slider.addEventListener('input', function(){
        const s = parseInt(this.value) / 100;
        scaleVal.textContent = this.value + '%';
        wrap.style.width = (W * s) + 'px';
        wrap.style.height = (H * s) + 'px';
        canvas.style.transform = 'scale(' + s + ')';
        canvas.style.transformOrigin = 'top left';
    });

// states: idle | running | dying | dead
    let state = 'idle', score = 0, hiScore = 0, lives = 3, frame = 0, speed = 5;
    let invincible = false, invTimer = 0;
    const INV_DUR = 110;

// spin state / death animation
    let spinning = false, spinAngle = 0, spinFrames = 0;
    const SPIN_TOTAL = 50; // frames for 2 full rotations (hit with lives left)

// death animation: spin + fly up then fall
    let deathAngle = 0, deathFrame = 0, deathY = 0, deathVY = 0;
    const DEATH_SPIN_TOTAL = 70;

// dead screen animation counter (runs forever on dead screen)
    let deadFrame = 0;

    const panda = { x:80, y:GROUND, vy:0, onGround:true, w:44, h:50 };

    let obstacles = [], obsTimer = 0, obsInterval = 90;
    let clouds = [{x:100,y:22,r:22},{x:310,y:35,r:16},{x:520,y:18,r:20}];
    let groundX = 0;

    function jump() {
        if(state==='idle'||state==='dead'){
            startGame();
            return;
        }
        if(state==='dying')
            return;
        if(panda.onGround && !spinning){
            panda.vy=-13.5;
            panda.onGround=false;
        }
    }

    document.getElementById('jumpBtn').addEventListener('click', jump);
    document.addEventListener('keydown', e=>{
        if(e.code==='Space'){
            e.preventDefault();
            jump();
        }
    });

    function startGame(){
        state='running';
        score=0;
        lives=3;
        speed=5;
        frame=0;
        deadFrame=0;
        invincible=false;
        invTimer=0;
        spinning=false;
        spinAngle=0;
        spinFrames=0;
        deathAngle=0;
        deathFrame=0;
        deathY=GROUND;
        deathVY=0;
        obstacles=[];
        obsTimer=0;
        obsInterval=90;
        panda.y=GROUND;
        panda.vy=0;
        panda.onGround=true;
        updateUI();
        document.getElementById('jumpBtn').textContent='Skocz';
        document.getElementById('hint').textContent='Przycisk lub Spacja = skok';
    }

    function updateUI(){
        document.getElementById('sc').textContent=score;
        document.getElementById('hi').textContent=hiScore;
        for(let i=1;i<=3;i++){
            const el=document.getElementById('lv'+i);
            el.style.opacity= i<=lives?'1':'0.2';
            el.style.transform= i<=lives?'scale(1)':'scale(0.7)';
            //el.style.transition='all 0.2s';
        }
    }

    function spawnObs(){
        const h=28+Math.random()*38, w=13+Math.random()*9;
        const n=Math.random()<0.38?2:1;
        for(let i=0;i<n;i++)
            obstacles.push({x:W+i*(w+10),y:GROUND-h+8,w,h});
    }

    function checkHit(){
        if(invincible) return false;
        const px=panda.x+10, py=panda.y-panda.h+12, pw=panda.w-18, ph=panda.h-18;
        for(const o of obstacles){
            if(px<o.x+o.w-4&&px+pw>o.x+4&&py<o.y+o.h&&py+ph>o.y) return true;
        }
        return false;
    }

    function update(){
        if(state==='dying'){
            deathFrame++;
            deathAngle = (deathFrame / DEATH_SPIN_TOTAL) * Math.PI * 4;
            deathVY += 0.5;
            deathY += deathVY;
            if(deathY >= GROUND)
                deathY = GROUND;
            if(deathFrame >= DEATH_SPIN_TOTAL){
                state='dead';
                document.getElementById('jumpBtn').textContent='Start';
                document.getElementById('hint').textContent='Kliknij Start lub Spacja aby zagrać ponownie';
            }
            return;
        }

        if(state==='dead'){
            deadFrame++;
            return;
        }

        if(state!=='running') return;

        frame++;
        score++;
        speed=5+Math.floor(score/500)*0.4;

        panda.vy+=0.65;
        panda.y+=panda.vy;

        if(panda.y>=GROUND){
            panda.y=GROUND;
            panda.vy=0;
            panda.onGround=true;
        }

        obsTimer++;
        if(obsTimer>=obsInterval){
            spawnObs();
            obsTimer=0;
            obsInterval=65+Math.random()*65;
        }
        for(const o of obstacles)
            o.x-=speed;
        obstacles=obstacles.filter(o=>o.x+o.w>-10);

        for(const c of clouds){
            c.x-=speed*0.28;
            if(c.x+c.r<0){
                c.x=W+c.r;
                c.y=15+Math.random()*45;
            }
        }
        groundX=(groundX-speed*0.6+W)%W;

        // update spin
        if(spinning){
            spinFrames++;
            spinAngle = (spinFrames / SPIN_TOTAL) * Math.PI * 4; // 2 full rotations
            if(spinFrames >= SPIN_TOTAL){
                spinning=false; spinAngle=0; spinFrames=0;
            }
        }

        if(invincible){
            invTimer--;
            if(invTimer<=0)
                invincible=false;
        }

        if(checkHit()){
            lives--;
            if(score>hiScore) hiScore=score;

            updateUI();

            // trigger spin
            spinning=true;
            spinAngle=0;
            spinFrames=0;

            if(lives<=0){
                // start death animation
                state='dying';
                deathFrame=0;
                deathAngle=0;
                deathY = panda.y - panda.h/2;
                deathVY = -10; // launch upward first
            } else {
                invincible=true;
                spinAngle=0;
                spinFrames=0;
                invincible=true;
                invTimer=INV_DUR;
            }
        }
        if(frame%4===0) updateUI();
    }

// ── Drawing ──────────────────────────────────────────────────────────────────

    function drawCloud(c){
        ctx.fillStyle='rgba(255,255,255,0.72)';  //ctx.fillStyle='rgba(200,200,200,0.55)';
        ctx.beginPath();
        ctx.arc(c.x,c.y,c.r,0,Math.PI*2);
        ctx.arc(c.x+c.r*0.75,c.y+3,c.r*0.7,0,Math.PI*2);
        ctx.arc(c.x-c.r*0.65,c.y+5,c.r*0.6,0,Math.PI*2);
        ctx.fill();
    }

    function drawGround(){
        ctx.fillStyle='#c8e6c9'; //ctx.fillStyle='#d8d8d8';
        ctx.fillRect(0,GROUND+8,W,H-GROUND);
        ctx.strokeStyle='#81c784'; //ctx.strokeStyle='#aaa';
        ctx.lineWidth=1.5;
        ctx.beginPath();
        ctx.moveTo(0,GROUND+8);
        ctx.lineTo(W,GROUND+8);
        ctx.stroke();
        for(let i=0;i<8;i++){
            const gx=(groundX+i*(W/8))%W;
            ctx.fillStyle='#a5d6a7'; //ctx.fillStyle='#bbb';
            ctx.beginPath();
            ctx.arc(gx,GROUND+8,4,Math.PI,0);
            ctx.fill();
        }
    }

    function drawBamboo(o){
        ctx.fillStyle='#558b2f'; //ctx.fillStyle='#555';
        ctx.fillRect(o.x+o.w*0.3,o.y,o.w*0.4,o.h);
        ctx.fillStyle='#33691e'; //ctx.fillStyle='#333';
        const segs=Math.floor(o.h/13);
        for(let i=0;i<=segs;i++)
            ctx.fillRect(o.x+o.w*0.18,o.y+i*13-2,o.w*0.64,3);

        ctx.fillStyle='#7cb342'; //ctx.fillStyle='#888';
        ctx.beginPath();
        ctx.ellipse(o.x+o.w*0.3-9,o.y-5,13,5,-0.4,0,Math.PI*2);
        ctx.fill();
        ctx.beginPath();
        ctx.ellipse(o.x+o.w*0.7+7,o.y-3,11,4.5,0.5,0,Math.PI*2);
        ctx.fill();
        ctx.beginPath();
        ctx.ellipse(o.x+o.w*0.5,o.y-11,9,4,0,0,Math.PI*2);
        ctx.fill();
    }

// Helper: stroke an ellipse
    function se(x,y,rx,ry,rot){
        ctx.beginPath();
        ctx.ellipse(x,y,rx,ry,rot,0,Math.PI*2);
        ctx.stroke();
    }
    function fe(x,y,rx,ry,rot,fill){
        ctx.fillStyle=fill;
        ctx.beginPath();
        ctx.ellipse(x,y,rx,ry,rot,0,Math.PI*2);
        ctx.fill();
        ctx.stroke();
    }
    function fa(x,y,r,fill){
        ctx.fillStyle=fill;
        ctx.beginPath();
        ctx.arc(x,y,r,0,Math.PI*2);
        ctx.fill();
        ctx.stroke();
    }

    function setOutline(w){
        ctx.strokeStyle='#111';
        ctx.lineWidth=w;
    }

    function drawPandaRunning(px, py){
        const leg=Math.sin(frame*0.28)*6;
        setOutline(1.5);

        // legs
        fe(px+11,py+46+(panda.onGround?leg:0),8,7,0.2,'#111');
        fe(px+31,py+46+(panda.onGround?-leg:0),8,7,-0.2,'#111');

        // body
        fe(px+22,py+32,18,21,0,'#f0f0f0');
        fe(px+22,py+35,10,13,0,'#ddd');

        // arms
        fe(px+5,py+28+(panda.onGround?leg*0.3:-4),6,10,0.5,'#111');
        fe(px+39,py+28+(panda.onGround?-leg*0.3:-4),6,10,-0.5,'#111');

        // head
        fa(px+22,py+15,16,'#f0f0f0');

        // ears outer
        fa(px+9,py+4,6,'#111');
        fa(px+35,py+4,6,'#111');
        // ears inner
        setOutline(1);
        fa(px+9,py+4,3.5,'#555');
        fa(px+35,py+4,3.5,'#555');

        setOutline(1.5);
        // eye patches
        fe(px+14,py+14,5.5,4.5,-0.3,'#222');
        fe(px+30,py+14,5.5,4.5,0.3,'#222');

        // eye whites
        setOutline(1);
        fa(px+14,py+14,2.5,'#fff');
        fa(px+30,py+14,2.5,'#fff');

        // pupils
        ctx.strokeStyle='transparent';
        fa(px+14.5,py+14,1.2,'#111');
        fa(px+30.5,py+14,1.2,'#111');

        setOutline(1.5);
        // nose
        fe(px+22,py+20,3,2,0,'#222');

        // smile
        ctx.strokeStyle='#222'; ctx.lineWidth=1.5;
        ctx.beginPath(); ctx.arc(px+22,py+21,3.5,0.1,Math.PI-0.1); ctx.stroke();
    }

// animFrame drives the eating animation independently
    function drawPandaEating(cx, cy, animFrame){
        const chew = Math.sin(animFrame * 0.18) * 1.5;
        // bob body slightly while chewing
        const bob = Math.sin(animFrame * 0.09) * 1.2;

        setOutline(1.5);

        // legs
        fe(cx+10,cy+50+bob,11,7,0.6,'#111');
        fe(cx+36,cy+50+bob,11,7,-0.6,'#111');

        // body
        fe(cx+22,cy+36+bob,18,20,0,'#f0f0f0');
        fe(cx+22,cy+39+bob,10,12,0,'#ddd');

        // arms bob with body
        fe(cx+7,cy+28+bob,6,11,0.9,'#111');
        fe(cx+37,cy+26+bob,6,11,-0.9,'#111');

        // head
        fa(cx+22,cy+15+bob,16,'#f0f0f0');

        // ears
        fa(cx+9,cy+3+bob,6,'#111');
        fa(cx+35,cy+3+bob,6,'#111');
        setOutline(1);
        fa(cx+9,cy+3+bob,3.5,'#555');
        fa(cx+35,cy+3+bob,3.5,'#555');

        setOutline(1.5);
        fe(cx+14,cy+13+bob,5.5,4.5,-0.3,'#222');
        fe(cx+30,cy+13+bob,5.5,4.5,0.3,'#222');

        // eyes close and open with chew rhythm
        const eyeOpen = Math.sin(animFrame * 0.18) > 0;
        setOutline(1);
        if(eyeOpen){
            fa(cx+14,cy+13+bob,2.5,'#fff');
            fa(cx+30,cy+13+bob,2.5,'#fff');
            ctx.strokeStyle='transparent';
            fa(cx+14.5,cy+13+bob,1.2,'#111');
            fa(cx+30.5,cy+13+bob,1.2,'#111');
        } else {
            // closed ^^ eyes
            ctx.strokeStyle='#fff';
            ctx.lineWidth=2;
            ctx.beginPath();
            ctx.arc(cx+14,cy+13+bob,2.5,Math.PI,0);
            ctx.stroke();
            ctx.beginPath();
            ctx.arc(cx+30,cy+13+bob,2.5,Math.PI,0);
            ctx.stroke();
        }
        setOutline(1.5);
        fe(cx+22,cy+19+bob,3,2,0,'#222');

        // animated chewing mouth
        ctx.strokeStyle='#222'; ctx.lineWidth=1.8;
        ctx.beginPath();
        ctx.arc(cx+22,cy+21+bob+chew,4,0,Math.PI);
        ctx.stroke();

        // blush pulses with chew
        const blushA = 0.18 + Math.abs(Math.sin(animFrame*0.18))*0.18;
        ctx.strokeStyle='transparent';
        ctx.fillStyle='rgba(150,150,150,0.28)';
        ctx.beginPath();
        ctx.ellipse(cx+10,cy+18+bob,5,3,0,0,Math.PI*2);
        ctx.fill();
        ctx.beginPath();
        ctx.ellipse(cx+34,cy+18+bob,5,3,0,0,Math.PI*2);
        ctx.fill();

        // bamboo shifts slightly with chew
        ctx.save();
        ctx.translate(cx + 22, cy + 27);
        ctx.rotate(Math.PI / 2);
        const bshift = Math.sin(animFrame * 0.18) * 0.8;
        ctx.fillStyle='#558b2f';
        ctx.fillRect(-3 + bshift, -21, 6, 42);
        ctx.strokeStyle='#222';
        ctx.lineWidth=1.5;
        ctx.strokeRect(-3 + bshift, -21, 6, 42);
        ctx.fillStyle='#33691e';
        for(let i=0;i<3;i++)
            ctx.fillRect(-5 + bshift, -19 + i * 13, 10, 3);
        ctx.restore();
        setOutline(1.5);

        // little bite particles
        const p = animFrame % 40;
        if(p < 20){
            ctx.fillStyle=`rgba(100,100,100,${(20-p)/20*0.5})`;
            ctx.beginPath();
            ctx.arc(cx+26+p*0.4, cy+22+bob-p*0.3, 2, 0, Math.PI*2);
            ctx.fill();
            ctx.beginPath();
            ctx.arc(cx+24+p*0.3, cy+20+bob-p*0.5, 1.5, 0, Math.PI*2);
            ctx.fill();
        }
    }

    function draw(){
        ctx.clearRect(0,0,W,H);
        ctx.fillStyle='#f0f7ee'; //ctx.fillStyle='#f8f8f8';
        ctx.fillRect(0,0,W,H);
        for(const c of clouds) drawCloud(c);
        drawGround();
        for(const o of obstacles) drawBamboo(o);

        if(state==='running'){
            const cx = panda.x + panda.w/2;
            const cy = panda.y - panda.h/2;
            if(spinning){
                ctx.save();
                ctx.translate(cx, cy);
                ctx.rotate(spinAngle);
                ctx.translate(-cx, -cy);
                drawPandaRunning(panda.x, panda.y - panda.h);
                ctx.restore();
            } else if(!(invincible && Math.floor(invTimer/5)%2===0)){
                drawPandaRunning(panda.x, panda.y - panda.h);
            }
        } else if(state==='dying'){
            // panda spins up then falls
            const cx = panda.x + panda.w/2;
            const cy = deathY;
            ctx.save();
            ctx.translate(cx, cy);
            ctx.rotate(deathAngle);
            ctx.translate(-cx, -cy);
            drawPandaRunning(panda.x, deathY - panda.h/2);
            ctx.restore();
        } else if(state==='idle'){
            drawPandaRunning(panda.x, panda.y-panda.h);
            ctx.fillStyle='rgba(240,247,238,0.82)'; //ctx.fillStyle='rgba(248,248,248,0.82)';
            ctx.fillRect(0,0,W,H);
            ctx.fillStyle='#2e7d32'; //ctx.fillStyle='#222';
            ctx.font='500 18px sans-serif';
            ctx.textAlign='center';
            ctx.fillText('Kliknij przycisk poniżej aby zacząć', W/2, H/2-8);
            ctx.fillStyle='#66bb6a'; //ctx.fillStyle='#777';
            ctx.font='400 13px sans-serif';
            ctx.fillText('Spacja lub przycisk = skok', W/2, H/2+16);
        } else if(state==='dead'){
            // draw animated eating panda, then overlay text on top
            drawPandaEating(W/2-22, H/2-58, deadFrame);
            ctx.fillStyle='rgba(240,247,238,0.82)'; //ctx.fillStyle='rgba(248,248,248,0.82)';
            ctx.fillRect(0,0,W,H);
            // redraw panda on top of overlay so it's visible
            drawPandaEating(W/2-22, H/2-58, deadFrame);
            ctx.fillStyle='#2e7d32'; //ctx.fillStyle='#222';
            ctx.font='500 17px sans-serif';
            ctx.textAlign='center';
            ctx.fillText('Koniec gry!  Wynik: '+score, W/2, H/2+36);
            ctx.fillStyle='#81c784'; //ctx.fillStyle='#777';
            ctx.font='400 12px sans-serif';
            ctx.fillText('Kliknij Start aby zagrać ponownie', W/2, H/2+56);
        }
    }

    function loop(){
        update();
        draw();
        requestAnimationFrame(loop);
    }
    loop();
})();
