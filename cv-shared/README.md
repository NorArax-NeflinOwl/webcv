# cv-shared

What both backends have in common: they serve the same pages and build the same database from the same
migrations. Nothing here depends on the language of the backend.

| Directory | Contents | cv-spring-kotlin | cv-asp.net |
|---|---|---|---|
| `web/` | The pages (`index.html`, `pandaGame.html`, `pokerGame.html`) with their `css/` and `js/` — plain HTML, no templating | Gradle adds `cv-shared` to the resources; served from `classpath:/web/` | Copied next to the assembly (`web/`) and served by the static files middleware |
| `db/migration/` | PostgreSQL migrations named the Flyway way: `V<version>__<description>.sql` | Applied by Flyway | Embedded in the assembly and applied by DbUp, in version order |

## The contract between the pages and a backend

A backend that serves `web/` has to answer the same requests with the same JSON:

| Request | Answer |
|---|---|
| `GET /`, `/pandaGame`, `/pokerGame` (and the `.html` files) | The pages |
| `GET /actuator/health` | `{"status":"UP"}` (anything else turns the health link red) |
| `GET /api/backend` | `{"label":"…"}` — the footer's name of the backend serving the page |
| `GET /api/hello` | Plain text |
| `GET /api/profileinfo?lang=pl\|en` | The CV (`ProfileDto`) |
| `GET`, `POST /api/pandagame/scores` | Top 10 scores / save a score (`{"nick","score"}` → 201) |
| `/api/pokertable…` and `WS /api/pokertable/{id}/ws?token=…` | Poker — see the protocol in the main README |

## Rules for changes

- **A page change** reaches both backends at their next build — check it on both.
- **A new migration** is a new file with the next version (`V5__…sql`). Never edit a migration that a
  database has already run: Flyway refuses to start when an applied script's checksum changes, and DbUp
  silently skips it.
- **The CV's description, technologies and stack & tools differ per backend**: rows of `profile_translation`,
  `profile_skill` and `profile_stack` carry `backend` = `kotlin` (cv-spring-kotlin) or `dotnet` (cv-asp.net), and
  each backend reads only its own (`ProfileService.BACKEND` / `ProfileService.Backend`). The experience is shared.
- Each backend has its **own database**. Flyway (`flyway_schema_history`) and DbUp (`dbup_schema_history`) keep
  separate journals, so pointing both at one database would run the scripts twice.
