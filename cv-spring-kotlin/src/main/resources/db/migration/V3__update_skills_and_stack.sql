-- ── New skills from Poker Game ───────────────────────────
INSERT INTO skill (name, category, icon_id) VALUES
    ('WebSocket',           'real-time',  (SELECT id FROM icon WHERE key = 'pulse')),
    ('Kotlinx Coroutines',  'concurrency', (SELECT id FROM icon WHERE key = 'kotlin'));

-- ── New stack items from Poker Game / Panda Game ─────────
INSERT INTO stack_item (label, sort_order) VALUES
    ('Spring WebSocket',    16),
    ('Kotlinx Coroutines',  17),
    ('HTML5 Canvas',        18);

-- ── Attach new skills to profile ─────────────────────────
INSERT INTO profile_skill (profile_id, skill_id, tag, sort_order)
SELECT
    (SELECT id FROM profile LIMIT 1),
    s.id,
    s.category,
    (SELECT COALESCE(MAX(sort_order), 0) FROM profile_skill) + ROW_NUMBER() OVER (ORDER BY s.name)
FROM skill s
WHERE s.name IN ('WebSocket', 'Kotlinx Coroutines');

-- ── Attach new stack items to profile ────────────────────
INSERT INTO profile_stack (profile_id, stack_item_id, sort_order)
SELECT
    (SELECT id FROM profile LIMIT 1),
    si.id,
    si.sort_order
FROM stack_item si
WHERE si.label IN ('Spring WebSocket', 'Kotlinx Coroutines', 'HTML5 Canvas');
