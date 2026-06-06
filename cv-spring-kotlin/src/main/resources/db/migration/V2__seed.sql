-- ── Icons ────────────────────────────────────────────────
INSERT INTO icon (key, svg_path, label) VALUES
                                            ('github',    'M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22', 'GitHub'),
                                            ('linkedin',  'M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-2-2 2 2 0 0 0-2 2v7h-4v-7a6 6 0 0 1 6-6z', 'LinkedIn'),
                                            ('email',     'M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z', 'E-mail'),
                                            ('kotlin',    'M2 2h20L12 12 2 22V2z', 'Kotlin'),
                                            ('docker',    'M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z', 'Docker'),
                                            ('database',  'M12 2C6.48 2 2 4.24 2 7v10c0 2.76 4.48 5 10 5s10-2.24 10-5V7c0-2.76-4.48-5-10-5z', 'Database'),
                                            ('tool',      'M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z', 'Tool'),
                                            ('git',       'M9 19V6l12-3v13M9 19c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zm12-3c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2z', 'Git'),
                                            ('pulse',     'M22 12h-4l-3 9L9 3l-3 9H2', 'Actuator');

-- ── Skills ───────────────────────────────────────────────
INSERT INTO skill (name, category, icon_id) VALUES
    ('Kotlin',      'primary language', (SELECT id FROM icon WHERE key = 'kotlin')),
('Spring Boot', 'web / rest api',   NULL),
('Docker',      'containerization', (SELECT id FROM icon WHERE key = 'docker')),
('PostgreSQL',  'database',         (SELECT id FROM icon WHERE key = 'database')),
('Gradle',      'build tool',       (SELECT id FROM icon WHERE key = 'tool')),
('Git',         'version control',  (SELECT id FROM icon WHERE key = 'git'));

-- ── Stack items ──────────────────────────────────────────
INSERT INTO stack_item (label, sort_order) VALUES
                                               ('JVM 17',          1),
                                               ('Spring MVC',      2),
                                               ('Spring Actuator', 3),
                                               ('Jackson',         4),
                                               ('JUnit 5',         5),
                                               ('MockK',           6),
                                               ('Docker Compose',  7),
                                               ('IntelliJ IDEA',   8),
                                               ('GitHub Actions',  9),
                                               ('REST API',        10);

-- ── Profile ───────────────────────────────────────────────
INSERT INTO profile (name, eyebrow, role, bio, footer, email, github_url, linkedin_url) VALUES
    (
        'Patryk Norbert Pudwel',
        'backend developer',
        '<span>Kotlin</span> / Spring Boot / Docker / PostgreSQL',
        'Backend developer z pasją do czystego kodu i skalowalnych systemów. Buduję RESTowe API i mikroserwisy w ekosystemie JVM — od projektu do produkcji w kontenerze.',
        'dostępny do współpracy w ciągu 3 miesięcy',
        'pudwel.n.patryk@gmail.com',
        'https://github.com/NorArax-NeflinOwl',
        'https://www.linkedin.com/in/ppudwel199527/'
    );

-- ── Profile skills ────────────────────────────────────────
INSERT INTO profile_skill (profile_id, skill_id, tag, sort_order)
SELECT
    (SELECT id FROM profile LIMIT 1),
    s.id,
    s.category,
    ROW_NUMBER() OVER (ORDER BY s.name)
FROM skill s;

-- ── Profile stack ─────────────────────────────────────────
INSERT INTO profile_stack (profile_id, stack_item_id, sort_order)
SELECT
    (SELECT id FROM profile LIMIT 1),
    si.id,
    si.sort_order
FROM stack_item si;

-- ── Experience ────────────────────────────────────────────
INSERT INTO experience (profile_id, company, position, contract_type, date_from, is_current, description) VALUES
                                                                                                              (
                                                                                                                  (SELECT id FROM profile LIMIT 1),
    'Przykładowa Firma Sp. z o.o.',
    'Junior Backend Developer',
    'B2B',
    '2023-03-01',
    TRUE,
    'Rozwój mikroserwisów w Kotlin + Spring Boot. Utrzymanie infrastruktury Docker.'
    );
