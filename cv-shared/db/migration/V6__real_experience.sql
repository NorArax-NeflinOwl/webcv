--  Real work experience in place of V2's placeholder rows (their experience_translation rows go with them: ON DELETE CASCADE).
--  From 05/2019 every Softsystem position ran on two contracts at once, B2B and an employment contract (UoP), so each
--  position has one row per contract and the page shows the two side by side for the same period.
--  Months: date_from is the first month, date_to the last one (NULL while current).

DELETE FROM experience;

WITH position_row (company, position, date_from, date_to, description_pl, description_en) AS (
    VALUES
    (
        'Softsystem', 'Senior Software Developer', DATE '2025-07-01', NULL::DATE,
        '• Rozwój i utrzymanie aplikacji klienckiej dla szpitali i laboratoriów w Windows Forms (WinForms, .NET Core, MySQL, DevExpress).<br>'
        || '• Rozwój i utrzymanie aplikacji serwerowej integrującej aplikację kliencką z systemami i urządzeniami zewnętrznymi (Java, Hibernate, Oracle, Spring, SOAP, REST).<br>'
        || '• Tworzenie i aktualizacja wymagań systemowych dla przyszłych funkcjonalności.<br>'
        || '• Dostarczanie klientom modułowych pakietów systemu do indywidualnego użytku.<br>'
        || '• Integracja istniejącego systemu z botami i agentami AI.',
        '• Developing and maintaining a client application for hospital and laboratory use on Windows Forms (WinForms, .NET Core, MySQL, DevExpress).<br>'
        || '• Developing and maintaining a server application to integrate the client application with external systems and devices (Java, Hibernate, Oracle, Spring, SOAP, REST).<br>'
        || '• Creating and updating system requirements for future functionalities.<br>'
        || '• Delivering modular system packages to clients for custom use.<br>'
        || '• Integrating the existing system with AI bots and agents.'
    ),
    (
        'Softsystem', 'Software Developer', DATE '2021-06-01', DATE '2025-06-01',
        '• Rozwój i utrzymanie aplikacji klienckiej dla szpitali i laboratoriów w Windows Forms (WinForms, .NET Core, MySQL, DevExpress).<br>'
        || '• Rozwój i utrzymanie aplikacji serwerowej integrującej aplikację kliencką z systemami i urządzeniami zewnętrznymi (Java, Hibernate, Oracle, Spring, SOAP).<br>'
        || '• Tworzenie i aktualizacja wymagań systemowych dla przyszłych funkcjonalności.<br>'
        || '• Dostarczanie klientom modułowych pakietów systemu do indywidualnego użytku.',
        '• Developing and maintaining a client application for hospital and laboratory use on Windows Forms (WinForms, .NET Core, MySQL, DevExpress).<br>'
        || '• Developing and maintaining a server application to integrate the client application with external systems and devices (Java, Hibernate, Oracle, Spring, SOAP).<br>'
        || '• Creating and updating system requirements for future functionalities.<br>'
        || '• Delivering modular system packages to clients for custom use.'
    ),
    (
        'Softsystem', 'Junior Software Developer', DATE '2019-05-01', DATE '2021-05-01',
        '• Rozwój i utrzymanie aplikacji klienckiej dla szpitali i laboratoriów w Windows Forms (WinForms, .NET Core, MySQL, DevExpress).<br>'
        || '• Rozwój i utrzymanie aplikacji serwerowej integrującej aplikację kliencką z systemami i urządzeniami zewnętrznymi (Java, Hibernate, Oracle, SOAP).',
        '• Developing and maintaining a client application for hospital and laboratory use on Windows Forms (WinForms, .NET Core, MySQL, DevExpress).<br>'
        || '• Developing and maintaining a server application to integrate the client application with external systems and devices (Java, Hibernate, Oracle, SOAP).'
    ),
    (
        'eLeader', 'Junior Software Developer', DATE '2017-10-01', DATE '2019-03-01',
        '• Rozwój i utrzymanie aplikacji klient-serwer do zarządzania dostawami i magazynem na Windows (Desktop i Mobile – UWP/.NET, Web – ASP.NET Core, SQLite).',
        '• Developing and maintaining a client-server application for delivery and storage management on Windows (Desktop & Mobile – UWP/.NET, Web – ASP.NET Core, SQLite).'
    )
),
contract_row (company, contract_type, contract_type_en) AS (
    VALUES
    ('Softsystem', 'B2B', 'B2B'),
    ('Softsystem', 'UoP', 'Employment contract'),
    ('eLeader',    'UoP', 'Employment contract')
),
inserted AS (
    INSERT INTO experience (profile_id, company, position, contract_type, date_from, date_to, is_current, description)
    SELECT (SELECT id FROM profile LIMIT 1), p.company, p.position, c.contract_type, p.date_from, p.date_to, p.date_to IS NULL, p.description_pl
    FROM position_row p
    JOIN contract_row c ON c.company = p.company
    RETURNING id, company, position, contract_type, date_from
)
INSERT INTO experience_translation (experience_id, language, position, contract_type, description)
SELECT
    i.id,
    t.language,
    i.position,
    CASE t.language WHEN 'en' THEN c.contract_type_en ELSE c.contract_type END,
    CASE t.language WHEN 'en' THEN p.description_en   ELSE p.description_pl END
FROM inserted i
JOIN position_row p ON p.company = i.company AND p.position = i.position AND p.date_from = i.date_from
JOIN contract_row c ON c.company = i.company AND c.contract_type = i.contract_type
CROSS JOIN (VALUES ('pl'), ('en')) AS t (language);
