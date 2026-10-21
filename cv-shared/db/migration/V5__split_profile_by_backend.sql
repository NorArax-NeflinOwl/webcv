--  Each backend shows its own description (eyebrow, role, bio, footer), technologies and stack & tools:
--  backend = 'kotlin' (cv-spring-kotlin) or 'dotnet' (cv-asp.net). The rows so far describe the Kotlin backend.

ALTER TABLE profile_translation ADD COLUMN backend VARCHAR(16) NOT NULL DEFAULT 'kotlin';
ALTER TABLE profile_skill       ADD COLUMN backend VARCHAR(16) NOT NULL DEFAULT 'kotlin';
ALTER TABLE profile_stack       ADD COLUMN backend VARCHAR(16) NOT NULL DEFAULT 'kotlin';

ALTER TABLE profile_translation ALTER COLUMN backend DROP DEFAULT;
ALTER TABLE profile_skill       ALTER COLUMN backend DROP DEFAULT;
ALTER TABLE profile_stack       ALTER COLUMN backend DROP DEFAULT;

ALTER TABLE profile_translation DROP CONSTRAINT profile_translation_profile_id_language_key;
ALTER TABLE profile_skill       DROP CONSTRAINT profile_skill_profile_id_skill_id_key;
ALTER TABLE profile_stack       DROP CONSTRAINT profile_stack_profile_id_stack_item_id_key;

ALTER TABLE profile_translation ADD CONSTRAINT profile_translation_profile_id_backend_language_key UNIQUE (profile_id, backend, language);
ALTER TABLE profile_skill       ADD CONSTRAINT profile_skill_profile_id_backend_skill_id_key      UNIQUE (profile_id, backend, skill_id);
ALTER TABLE profile_stack       ADD CONSTRAINT profile_stack_profile_id_backend_stack_item_id_key UNIQUE (profile_id, backend, stack_item_id);

--  .NET description (eyebrow and footer as for Kotlin)
INSERT INTO profile_translation (profile_id, backend, language, eyebrow, role, bio, footer)
SELECT
    profile_id,
    'dotnet',
    language,
    eyebrow,
    '<span>C#</span> / ASP.NET Core / Docker / PostgreSQL',
    CASE language
        WHEN 'en' THEN 'Backend developer passionate about clean code and scalable systems. Building REST APIs and microservices in the .NET ecosystem — from design to production in a container.'
        ELSE           'Backend developer z pasją do czystego kodu i skalowalnych systemów. Buduję RESTowe API i mikroserwisy w ekosystemie .NET — od projektu do produkcji w kontenerze.'
    END,
    footer
FROM profile_translation
WHERE backend = 'kotlin';

--  .NET skills (Docker, PostgreSQL, Git, HTML / CSS and WebSocket are shared with Kotlin)
INSERT INTO skill (name, category, icon_id) VALUES
    ('C#',                    'primary language', NULL),
    ('ASP.NET Core',          'web / rest api',   NULL),
    ('Entity Framework Core', 'orm',              (SELECT id FROM icon WHERE key = 'database')),
    ('DbUp',                  'db migrations',    (SELECT id FROM icon WHERE key = 'database')),
    ('async / await',         'concurrency',      NULL),
    ('.NET CLI',              'build tool',       (SELECT id FROM icon WHERE key = 'tool'));

INSERT INTO profile_skill (profile_id, backend, skill_id, tag, sort_order)
SELECT
    (SELECT id FROM profile LIMIT 1),
    'dotnet',
    s.id,
    s.category,
    dotnet.sort_order
FROM (VALUES
    ('C#',                     1),
    ('ASP.NET Core',           2),
    ('Entity Framework Core',  3),
    ('PostgreSQL',             4),
    ('DbUp',                   5),
    ('Docker',                 6),
    ('WebSocket',              7),
    ('async / await',          8),
    ('.NET CLI',               9),
    ('Git',                   10),
    ('HTML / CSS',            11)
) AS dotnet (name, sort_order)
JOIN skill s ON s.name = dotnet.name;

--  .NET stack & tools (Docker Compose, GitHub Actions, REST API and HTML5 Canvas are shared with Kotlin)
INSERT INTO stack_item (label, sort_order) VALUES
    ('.NET 10',                     1),
    ('ASP.NET Core MVC',            2),
    ('ASP.NET Core Health Checks',  3),
    ('Entity Framework Core',       4),
    ('Npgsql',                      5),
    ('DbUp',                        6),
    ('System.Text.Json',            7),
    ('xUnit',                       8),
    ('NSubstitute',                 9),
    ('SQLite (testy)',             10),
    ('ASP.NET Core WebSockets',    14),
    ('async / await (TPL)',        15);

INSERT INTO profile_stack (profile_id, backend, stack_item_id, sort_order)
SELECT
    (SELECT id FROM profile LIMIT 1),
    'dotnet',
    si.id,
    dotnet.sort_order
FROM (VALUES
    ('.NET 10',                     1),
    ('ASP.NET Core MVC',            2),
    ('ASP.NET Core Health Checks',  3),
    ('Entity Framework Core',       4),
    ('Npgsql',                      5),
    ('DbUp',                        6),
    ('System.Text.Json',            7),
    ('xUnit',                       8),
    ('NSubstitute',                 9),
    ('SQLite (testy)',             10),
    ('Docker Compose',             11),
    ('GitHub Actions',             12),
    ('REST API',                   13),
    ('ASP.NET Core WebSockets',    14),
    ('async / await (TPL)',        15),
    ('HTML5 Canvas',               16)
) AS dotnet (label, sort_order)
JOIN stack_item si ON si.label = dotnet.label;
