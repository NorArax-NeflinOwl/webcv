//  Actuator health check 
async function checkHealth() {
    const link = document.getElementById('health-link');
    if (!link) return;
    try {
        const res  = await fetch('/actuator/health');
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
        link.title = 'No connection to application';
    }
}

//  Footer status dot 
/**
 * Sets the footer dot colour and class based on the footerStatus
 * value from the API: "GREEN" | "YELLOW" | "RED"
 */
function applyFooterStatus(status) {
    const dot = document.querySelector('.footer .dot');
    if (!dot) return;

    // Remove previous status classes
    dot.classList.remove('dot--green', 'dot--yellow', 'dot--red');

    switch (status) {
        case 'GREEN':
            dot.classList.add('dot--green');
            dot.title = 'Available';
            break;
        case 'RED':
            dot.classList.add('dot--red');
            dot.title = 'Unavailable';
            break;
        case 'YELLOW':
        default:
            dot.classList.add('dot--yellow');
            dot.title = 'Partially available';
            break;
    }
}

//  Staggered skill card animation 
function animateSkills() {
    const items = document.querySelectorAll('.skill-item');
    items.forEach((el, i) => {
        el.style.opacity    = '0';
        el.style.transform  = 'translateY(12px)';
        el.style.transition = `opacity .4s ${i * 0.06}s ease, transform .4s ${i * 0.06}s ease, border-color .2s, background .2s`;
        setTimeout(() => {
            el.style.opacity   = '1';
            el.style.transform = 'translateY(0)';
        }, 100 + i * 60);
    });
}

//  Link icons 
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
    'E-mail':   '',  // shows email address as label
};

//  Render links from API 
function renderLinks(links) {
    const container = document.getElementById('links');
    if (!container) return;

    // Keep static buttons  remove only dynamic ones
    const dynamic = container.querySelectorAll('.link-btn--dynamic');
    dynamic.forEach(el => el.remove());

    const staticBtns  = container.querySelectorAll('.link-btn:not(.link-btn--dynamic)');
    const firstStatic = staticBtns[0] || null;

    Object.entries(links).forEach(([key, url]) => {
        if (!url) return;

        const isEmail = key === 'E-mail';
        const href    = isEmail ? `mailto:${url}` : url;
        const label   = LINK_LABELS[key] !== undefined
            ? (LINK_LABELS[key] || url)
            : key;
        const icon    = LINK_ICONS[key] ?? `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>`;

        const a       = document.createElement('a');
        a.className   = 'link-btn link-btn--dynamic';
        a.href        = href;
        a.innerHTML   = `${icon} ${label}`;
        if (!isEmail) {
            a.target = '_blank';
            a.rel    = 'noopener';
        }

        container.insertBefore(a, firstStatic);
    });
}

//  Render technologies from API 
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

//  Experience timeline — month-level granularity helpers

const MONTHS_ABBR = {
    pl: ['sty','lut','mar','kwi','maj','cze','lip','sie','wrz','paź','lis','gru'],
    en: ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec']
};

function toMonthIndex(dateStr) {
    const d = new Date(dateStr);
    return d.getFullYear() * 12 + d.getMonth();
}

function formatMonthLabel(monthIndex, language) {
    const year  = Math.floor(monthIndex / 12);
    const month = monthIndex % 12;
    const names = MONTHS_ABBR[language] ?? MONTHS_ABBR.en;
    return `${names[month]} ${year}`;
}

/**
 * Splits experiences into merged timeline segments where each segment holds
 * the set of entries active during that period.  Consecutive segments with
 * the same active set are merged into one row.
 */
/**
 * Within the same (company, contractType), a newer isCurrent entry
 * supersedes an older one: the older is closed at newer.startMonth - 1.
 * Mutates the entries array in place.
 */
function resolveSupersededEntries(entries) {
    const groups = {};
    entries.forEach((e, idx) => {
        if (!e.isCurrent) return;
        const key = e.exp.company + '||' + (e.exp.contractType ?? '');
        if (!groups[key]) groups[key] = [];
        groups[key].push(idx);
    });

    Object.values(groups).forEach(indices => {
        if (indices.length <= 1) return;
        indices.sort((a, b) => entries[a].startMonth - entries[b].startMonth);
        for (let i = 0; i < indices.length - 1; i++) {
            const older = entries[indices[i]];
            older.endMonth  = entries[indices[i + 1]].startMonth - 1;
            older.isCurrent = false;
        }
    });
}

/**
 * Splits experiences into merged timeline segments.  Each entry (including
 * isCurrent ones) contributes both startMonth and endMonth+1 as breakpoints,
 * so no separate trailing-segment handling is needed.
 * Consecutive segments with the same active set are merged.
 * Result is reversed so the newest period appears first.
 */
function buildTimelineSegments(experiences) {
    const now = new Date();
    const currentMonth = now.getFullYear() * 12 + now.getMonth();

    const entries = experiences.map(exp => ({
        exp,
        startMonth: toMonthIndex(exp.dateFrom),
        endMonth:   exp.isCurrent ? currentMonth : toMonthIndex(exp.dateTo),
        isCurrent:  exp.isCurrent
    }));

    resolveSupersededEntries(entries);

    // Every entry contributes its start and (end+1) as breakpoints
    const pointSet = new Set();
    entries.forEach(({ startMonth, endMonth }) => {
        pointSet.add(startMonth);
        pointSet.add(endMonth + 1);
    });
    const points = [...pointSet].sort((a, b) => a - b);

    // For each interval between breakpoints, collect active entries
    const rawSegments = [];
    for (let i = 0; i < points.length - 1; i++) {
        const segStart   = points[i];
        const segEnd     = points[i + 1] - 1;
        const activeEntries = entries.filter(e => e.startMonth <= segStart && e.endMonth >= segEnd);
        if (activeEntries.length === 0) continue;

        rawSegments.push({
            startMonth: segStart,
            endMonth:   segEnd,
            isCurrent:  activeEntries.some(e => e.isCurrent),
            active:     activeEntries.map(e => e.exp)
        });
    }

    if (rawSegments.length === 0) return [];

    // Merge consecutive segments with the same active-experience set
    const merged = [{ ...rawSegments[0], active: [...rawSegments[0].active] }];
    for (let i = 1; i < rawSegments.length; i++) {
        const cur  = rawSegments[i];
        const last = merged[merged.length - 1];
        if (sameExperienceSet(last.active, cur.active)) {
            last.endMonth  = cur.endMonth;
            last.isCurrent = cur.isCurrent;
        } else {
            merged.push({ ...cur, active: [...cur.active] });
        }
    }

    // Newest period first
    return merged.reverse();
}

function sameExperienceSet(a, b) {
    if (a.length !== b.length) return false;
    const key = exps => exps
        .map(e => `${e.company}|${e.position}|${e.contractType}`)
        .sort()
        .join('\n');
    return key(a) === key(b);
}

/** Renders one card per experience entry. Description is hidden;
 * hovering the card reveals it as a tooltip. */
function buildTimelineCellHTML(exp) {
    const contractBadge = exp.contractType
        ? `<span class="timeline-cell__contract">${exp.contractType}</span>`
        : '';
    const tooltip = exp.description
        ? `<div class="timeline-cell__tooltip">${exp.description}</div>`
        : '';

    return `
        <div class="timeline-cell">
            <div class="timeline-cell__header">
                <span class="timeline-cell__company">${exp.company}</span>
                ${contractBadge}
            </div>
            <span class="timeline-cell__position">${exp.position}</span>
            ${tooltip}
        </div>`;
}

//  Render experience timeline from API
function renderExperiences(experiences, language = 'pl') {
    const container = document.getElementById('experience-list');
    if (!container) return;

    if (!experiences || experiences.length === 0) {
        container.innerHTML = '';
        return;
    }

    const nowLabel = language === 'pl' ? 'teraz' : 'now';
    const segments = buildTimelineSegments(experiences);

    container.innerHTML = segments.map(seg => {
        const from  = formatMonthLabel(seg.startMonth, language);
        const to    = seg.isCurrent ? nowLabel : formatMonthLabel(seg.endMonth, language);
        const cells = seg.active.map(buildTimelineCellHTML).join('');

        return `
            <div class="timeline-row">
                <span class="timeline-row__period">${from} – ${to}</span>
                <div class="timeline-row__cells">${cells}</div>
            </div>`;
    }).join('');
}

//  Render stack chips from API
function renderChips(stacks) {
    const container = document.getElementById('stack-chips');
    if (!container) return;

    container.innerHTML = stacks
        .map(label => `<span class="chip">${label}</span>`)
        .join('');
}

//  Fetch & populate profile
async function loadProfile(language) {
    try {
        const res  = await fetch(`/api/profileinfo?lang=${language}`);
        const data = await res.json();

        document.title = data.title ?? document.title;

        setText('eyebrow',       data.eyebrow);
        setText('name',          data.name);
        setHTML('role',          data.role);
        setText('bio',           data.bio);
        setText('footer-status', data.footer);

        if (data.footerStatus)  applyFooterStatus(data.footerStatus);
        if (data.links)         renderLinks(data.links);
        if (data.experiences)   renderExperiences(data.experiences, language);
        if (data.technologies)  renderTechnologies(data.technologies);
        if (data.stacks)        renderChips(data.stacks);

        animateSkills();

    } catch (err) {
        console.error('Profile loading error:', err);
    }
}

//  Language switcher
function initLanguageSwitcher() {
    const savedLanguage = localStorage.getItem('lang') ?? 'pl';
    applyLanguage(savedLanguage);

    document.querySelectorAll('.lang-btn').forEach(btn => {
        btn.addEventListener('click', () => applyLanguage(btn.dataset.lang));
    });
}

function applyLanguage(language) {
    localStorage.setItem('lang', language);

    document.querySelectorAll('.lang-btn').forEach(btn => {
        btn.classList.toggle('lang-btn--active', btn.dataset.lang === language);
    });

    loadProfile(language);
}

//  Helpers
function setText(id, value) {
    const el = document.getElementById(id);
    if (el && value !== undefined) el.textContent = value;
}

function setHTML(id, value) {
    const el = document.getElementById(id);
    if (el && value !== undefined) el.innerHTML = value;
}

//  Backend label
/**
 * The page is shared by every backend (cv-shared/web), so the footer asks
 * the server which one is serving it instead of hard-coding the stack.
 */
async function loadBackendLabel() {
    try {
        const res  = await fetch('/api/backend');
        const data = await res.json();
        setText('backend-label', data.label);
    } catch (err) {
        console.error('Backend label loading error:', err);
    }
}

//  Init
document.addEventListener('DOMContentLoaded', () => {
    initLanguageSwitcher();
    checkHealth();
    loadBackendLabel();
});
