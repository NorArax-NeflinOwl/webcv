// ── Actuator health check ────────────────────────────────
async function checkHealth() {
    const link = document.getElementById('health-link');
    if (!link) return;
    try {
        const res = await fetch('/actuator/health');
        const data = await res.json();
        if (data.status === 'UP') {
            link.classList.add('health-ok');
            link.title = 'Status: UP';
        } else {
            link.classList.add('health-error');
            link.title = `Status: ${data.status}`;
        }
    } catch {
        link.classList.add('health-error');
        link.title = 'Brak połączenia z aplikacją';
    }
}

// ── Staggered skill card animation ──────────────────────
function animateSkills() {
    const items = document.querySelectorAll('.skill-item');
    items.forEach((el, i) => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(12px)';
        el.style.transition = `opacity .4s ${i * 0.06}s ease, transform .4s ${i * 0.06}s ease, border-color .2s, background .2s`;
        setTimeout(() => {
            el.style.opacity = '1';
            el.style.transform = 'translateY(0)';
        }, 100 + i * 60);
    });
}

// ── Link icons ───────────────────────────────────────────
const LINK_ICONS = {
    GithubLink: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22"/></svg>`,
    LinkedIn:   `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-2-2 2 2 0 0 0-2 2v7h-4v-7a6 6 0 0 1 6-6z"/><rect x="2" y="9" width="4" height="12"/><circle cx="4" cy="4" r="2"/></svg>`,
    RocketJob:  `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z"/><path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z"/><path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0"/><path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5"/></svg>`,
    'E-mail':   `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>`,
};

const LINK_LABELS = {
    GithubLink: 'GitHub',
    LinkedIn:   'LinkedIn',
    RocketJob:  'RocketJobs',
    'E-mail':   '',  // pokazuje adres email jako label
};

// ── Render links from API ────────────────────────────────
function renderLinks(links) {
    const container = document.getElementById('links');
    if (!container) return;

    // Zachowaj stałe przyciski (health, hello, profile) — usuń tylko dynamiczne
    const dynamic = container.querySelectorAll('.link-btn--dynamic');
    dynamic.forEach(el => el.remove());

    // Wstaw dynamiczne linki przed stałymi
    const staticBtns = container.querySelectorAll('.link-btn:not(.link-btn--dynamic)');
    const firstStatic = staticBtns[0] || null;

    Object.entries(links).forEach(([key, url]) => {
        if (!url) return; // pomiń puste (np. RocketJob bez URL)

        const isEmail = key === 'E-mail';
        const href    = isEmail ? `mailto:${url}` : url;
        const label   = LINK_LABELS[key] !== undefined
            ? (LINK_LABELS[key] || url)   // pusty label → pokaż URL/adres
            : key;
        const icon    = LINK_ICONS[key] ?? `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>`;

        const a = document.createElement('a');
        a.className = 'link-btn link-btn--dynamic';
        a.href      = href;
        a.innerHTML = `${icon} ${label}`;
        if (!isEmail) {
            a.target = '_blank';
            a.rel    = 'noopener';
        }

        container.insertBefore(a, firstStatic);
    });
}

// ── Render technologies from API ─────────────────────────
function renderTechnologies(technologies) {
    const grid = document.getElementById('skills-grid');
    if (!grid) return;

    grid.innerHTML = Object.entries(technologies)
        .map(([name, tag]) => `
            <div class="skill-item">
                <div class="skill-item__name">${name}</div>
                <div class="skill-item__tag">${tag}</div>
            </div>`)
        .join('');
}

// ── Render stack chips from API ──────────────────────────
function renderChips(stacks) {
    const container = document.getElementById('stack-chips');
    if (!container) return;

    container.innerHTML = stacks
        .map(label => `<span class="chip">${label}</span>`)
        .join('');
}

// ── Fetch & populate profile ─────────────────────────────
async function loadProfile() {
    try {
        const res  = await fetch('/api/profileinfo');
        const data = await res.json();

        // <title>
        document.title = data.title;

        // Header
        setText('eyebrow',   data.eyebrow);
        setText('name',      data.name);
        setHTML('role',      data.role);      // role zawiera <span>
        setText('bio',       data.bio);

        // Footer
        setText('footer-status', data.footer);

        // Dynamiczne sekcje
        renderLinks(data.links);
        renderTechnologies(data.technologies);
        renderChips(data.stacks);

        // Animacja kart po wyrenderowaniu
        animateSkills();

    } catch (err) {
        console.error('Błąd ładowania profilu:', err);
    }
}

// ── Helpers ──────────────────────────────────────────────
function setText(id, value) {
    const el = document.getElementById(id);
    if (el && value !== undefined) el.textContent = value;
}

function setHTML(id, value) {
    const el = document.getElementById(id);
    if (el && value !== undefined) el.innerHTML = value;
}

// ── Init ─────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadProfile();
    checkHealth();
});