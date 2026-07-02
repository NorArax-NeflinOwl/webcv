--  profile_translation
CREATE TABLE profile_translation (
    id         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id UUID         NOT NULL REFERENCES profile(id) ON DELETE CASCADE,
    language   VARCHAR(8)   NOT NULL,
    eyebrow    VARCHAR(128),
    role       VARCHAR(256),
    bio        TEXT,
    footer     VARCHAR(256),
    UNIQUE (profile_id, language)
);

--  experience_translation
CREATE TABLE experience_translation (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    experience_id UUID         NOT NULL REFERENCES experience(id) ON DELETE CASCADE,
    language      VARCHAR(8)   NOT NULL,
    position      VARCHAR(256) NOT NULL,
    contract_type VARCHAR(64),
    description   TEXT,
    UNIQUE (experience_id, language)
);

--  Seed PL translations from existing profile data
INSERT INTO profile_translation (profile_id, language, eyebrow, role, bio, footer)
SELECT id, 'pl', eyebrow, role, bio, footer FROM profile;

--  Seed EN translations for profile
INSERT INTO profile_translation (profile_id, language, eyebrow, role, bio, footer)
SELECT
    id,
    'en',
    'backend developer',
    '<span>Kotlin</span> / Spring Boot / Docker / PostgreSQL',
    'Backend developer passionate about clean code and scalable systems. Building REST APIs and microservices in the JVM ecosystem — from design to production in a container.',
    'available for collaboration within 3 months'
FROM profile;

--  Seed PL experience translations from existing data
INSERT INTO experience_translation (experience_id, language, position, contract_type, description)
SELECT id, 'pl', position, contract_type, description FROM experience;

--  Seed EN experience translations
INSERT INTO experience_translation (experience_id, language, position, contract_type, description)
SELECT
    id,
    'en',
    'Junior Backend Developer',
    contract_type,
    'Development of microservices in Kotlin + Spring Boot. Maintenance of Docker infrastructure.'
FROM experience;
