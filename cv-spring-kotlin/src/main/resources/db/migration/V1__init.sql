-- ── Extensions ────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ── icon ──────────────────────────────────────────────────
CREATE TABLE icon (
                      id        UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                      key       VARCHAR(64) NOT NULL UNIQUE,
                      svg_path  TEXT        NOT NULL,
                      label     VARCHAR(128)
);

-- ── skill ─────────────────────────────────────────────────
CREATE TABLE skill (
                       id        UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                       name      VARCHAR(128) NOT NULL,
                       category  VARCHAR(64)  NOT NULL,
                       icon_id   UUID         REFERENCES icon(id)
);

-- ── stack_item ────────────────────────────────────────────
CREATE TABLE stack_item (
                            id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                            label      VARCHAR(128) NOT NULL,
                            sort_order INT          NOT NULL DEFAULT 0,
                            icon_id    UUID         REFERENCES icon(id)
);

CREATE TYPE footer_status_enum AS ENUM ('GREEN', 'YELLOW', 'RED');

-- ── profile ───────────────────────────────────────────────
CREATE TABLE profile (
                         id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                         name          VARCHAR(256) NOT NULL,
                         eyebrow       VARCHAR(128),
                         role          VARCHAR(256),
                         bio           TEXT,
                         footer        VARCHAR(256),
                         footer_status footer_status_enum NOT NULL DEFAULT 'RED',
                         email         VARCHAR(256),
                         github_url    VARCHAR(512),
                         linkedin_url  VARCHAR(512)
);

-- ── profile_skill ─────────────────────────────────────────
CREATE TABLE profile_skill (
                               id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                               profile_id UUID        NOT NULL REFERENCES profile(id) ON DELETE CASCADE,
                               skill_id   UUID        NOT NULL REFERENCES skill(id)   ON DELETE CASCADE,
                               tag        VARCHAR(128),
                               sort_order INT         NOT NULL DEFAULT 0,
                               UNIQUE (profile_id, skill_id)
);

-- ── profile_stack ─────────────────────────────────────────
CREATE TABLE profile_stack (
                               id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                               profile_id    UUID NOT NULL REFERENCES profile(id)    ON DELETE CASCADE,
                               stack_item_id UUID NOT NULL REFERENCES stack_item(id) ON DELETE CASCADE,
                               sort_order    INT  NOT NULL DEFAULT 0,
                               UNIQUE (profile_id, stack_item_id)
);

-- ── experience ────────────────────────────────────────────
CREATE TABLE experience (
                            id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                            profile_id    UUID         NOT NULL REFERENCES profile(id) ON DELETE CASCADE,
                            company       VARCHAR(256) NOT NULL,
                            position      VARCHAR(256) NOT NULL,
                            contract_type VARCHAR(64),
                            date_from     DATE         NOT NULL,
                            date_to       DATE,
                            is_current    BOOLEAN      NOT NULL DEFAULT FALSE,
                            description   TEXT
);