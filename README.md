# 🌐 WebCV

> Portfolio & CV as a web application  a collection of projects showcasing different technologies.

![Kotlin](https://img.shields.io/badge/Kotlin-7F52FF?style=flat&logo=kotlin&logoColor=white)
![Spring Boot](https://img.shields.io/badge/Spring_Boot-6DB33F?style=flat&logo=springboot&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=flat&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)
![WebSocket](https://img.shields.io/badge/WebSocket-010101?style=flat&logo=socketdotio&logoColor=white)
![Coroutines](https://img.shields.io/badge/Coroutines-7F52FF?style=flat&logo=kotlin&logoColor=white)
![Flyway](https://img.shields.io/badge/Flyway-CC0200?style=flat&logo=flyway&logoColor=white)
![Gradle](https://img.shields.io/badge/Gradle-02303A?style=flat&logo=gradle&logoColor=white)
![HTML5](https://img.shields.io/badge/HTML5-E34F26?style=flat&logo=html5&logoColor=white)
![C#](https://img.shields.io/badge/C%23-512BD4?style=flat&logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?style=flat&logo=dotnet&logoColor=white)

---

## About

A series of mini-projects building an interactive CV as a web application. Each project focuses on a different technology stack  from backend and containerisation to real-time communication and frontend. The application will eventually be deployed on a public server as a skills showcase.

The same application exists twice, with two interchangeable backends behind one frontend:

```
webcv/
├── cv-shared/          pages (HTML/CSS/JS) and SQL migrations  used by both backends
├── cv-spring-kotlin/   backend: Kotlin + Spring Boot          → http://localhost:8280
├── cv-asp.net/         backend: C# + ASP.NET Core             → http://localhost:8281
└── run.ps1             builds and (re)starts the backends in Docker
```

See [cv-shared/README.md](cv-shared/README.md) for the contract a backend has to fulfil.

---

## Projects

### 01  CV Page: Kotlin + Spring Boot + PostgreSQL + Docker
> Status: ✅ Done

A web server serving the CV page. Backend written in Kotlin with Spring Boot, data stored in PostgreSQL with Flyway migrations, containerised with Docker.

📁 Directory: `/cv-spring-kotlin`

**Technologies:**
- Kotlin + Spring Boot (Spring MVC, Spring Data JPA, Spring Actuator)
- PostgreSQL + Flyway (migrations) + Hibernate + HikariCP
- Docker + Docker Compose
- Gradle (build tool)
- JUnit 5 + MockK + H2 (unit & integration tests)
- HTML / CSS / JavaScript (frontend  CV page)
- REST API (`/api/profileinfo`, `/api/hello`, `/actuator/health`)

---

### 02  Panda Game
> Status: 🚧 In progress

A mini-game in the style of the Google Chrome Dino  a panda jumping over obstacles.
Built with plain HTML + CSS + JavaScript, served by the same Spring Boot server.
Player scores are saved to PostgreSQL (`panda_game_player_score`) and displayed as a leaderboard.

📁 Directory: `/cv-shared/web/pandaGame.html`
📁 Directory: `/cv-shared/web/css/pandaGameStyle.css`
📁 Directory: `/cv-shared/web/js/pandaGame.js`

**Technologies:**
- HTML5 Canvas
- CSS3 + animations
- JavaScript (vanilla)
- Spring MVC + Spring Data JPA (saving & reading scores)
- PostgreSQL + Flyway (migration `V1`)
- JUnit 5 + MockK + H2 (service, controller & repository tests)
- REST API (`/api/pandagame/scores`)

---

### 03  Poker Game
> Status: 🚧 In progress

Real-time multiplayer Texas Hold'em Poker. The game engine is written in Kotlin with Coroutines;
communication runs over WebSocket (Spring WebSocket). One human player sits at the table alongside
two bots (RandomBot). Frontend in plain HTML + CSS + JavaScript.

📁 Directory: `/cv-spring-kotlin/src/main/kotlin/com/pokerkotlin`

**Technologies:**
- Kotlin + Kotlinx Coroutines (game engine: dealing, betting, hand evaluation)
- Spring WebSocket (`TextWebSocketHandler`)  table state pushed after every action
- REST API (`/api/pokertable`, `/api/pokertable/{id}/join`, `/api/pokertable/{id}/start`)
- HTML5 / CSS3 / JavaScript (vanilla)  table view with card rendering
- JUnit 5 + MockK (engine tests, hand evaluation tests, integration tests)

**Protocol:**
```
POST /api/pokertable                    → { tableId }           (creates a table with 2 bots)
POST /api/pokertable/{id}/join          → { sessionToken, playerId }
POST /api/pokertable/{id}/start?token=… → 204
WS   /api/pokertable/{id}/ws?token=…   ← TableSnapshotDto (pushed on every state change)
                                → { type: "Fold"|"Check"|"Call"|"Raise", amount? }
```

---

### 04  CV Page: C# + ASP.NET Core + PostgreSQL + Docker
> Status: ✅ Done

The same CV, Panda Game and Poker Game with a .NET backend: the same pages and the same SQL migrations
(from `cv-shared`), the same REST/WebSocket API, the same tests ported from Kotlin.

📁 Directory: `/cv-asp.net`

**Technologies:**
- C# 14 + ASP.NET Core 10 (MVC controllers, static files, WebSockets, health checks)
- PostgreSQL + EF Core (Npgsql) + DbUp (runs the Flyway-named scripts from `cv-shared/db/migration`)
- `CvAspNet.Poker`  the Texas Hold'em engine as a separate library (no ASP.NET, no database)
- Docker + Docker Compose (own database on port 5443)
- xUnit + NSubstitute + SQLite in-memory + `WebApplicationFactory` (unit, repository, API and WebSocket tests)

---

## Running the app

**Requirements:** Docker (and PowerShell for `run.ps1`)

```powershell
# Clone the repository
git clone https://github.com/NorArax-NeflinOwl/webcv.git
cd webcv

# Build from the current code and (re)start both backends; the data stays in the database volumes
.\run.ps1                     # both: Kotlin on :8280, .NET on :8281
.\run.ps1 -Backend dotnet     # or only one (kotlin | dotnet)
.\run.ps1 -Backend kotlin -Logs   # and stay attached to its log
```

On the first run `run.ps1` creates each backend's `.env` from `.env.example` with a random database password.
Without the script, each backend still runs on its own:

```bash
cd cv-spring-kotlin             # or cv-asp.net
docker compose up --build -d    # needs .env (copy .env.example)

# Tear everything down and remove images
docker compose down -v --rmi all
```

Application available at: `http://localhost:8280` (Kotlin) and `http://localhost:8281` (.NET)  the endpoints are the same:

| Endpoint | Description |
|---|---|
| `http://localhost:8280/` | CV page |
| `http://localhost:8280/pandaGame` | Panda mini-game |
| `http://localhost:8280/pokerGame` | Poker Game |
| `http://localhost:8280/api/profileinfo` | Profile data (JSON) |
| `http://localhost:8280/api/hello` | Hello World |
| `http://localhost:8280/api/backend` | Which backend serves the page (footer label) |
| `http://localhost:8280/api/pandagame/scores` | Panda Game scores (GET  all scores, POST  save score) |
| `http://localhost:8280/api/pokertable` | List poker tables (GET) / Create table (POST) |
| `http://localhost:8280/api/pokertable/{id}/join` | Join a table (POST) |
| `http://localhost:8280/api/pokertable/{id}/start` | Start a hand (POST) |
| `ws://localhost:8280/api/pokertable/{id}/ws` | WebSocket  real-time game state |
| `http://localhost:8280/actuator/health` | Application health |

---

## Running locally (without Docker)

**Kotlin**  requirements: JDK 21, PostgreSQL 16

```bash
cd cv-spring-kotlin
docker compose up db -d     # PostgreSQL via Docker (port 5442)
./gradlew bootRun           # DATABASE_URL / DATABASE_USERNAME / DATABASE_PASSWORD as in .env.example
```

**.NET**  requirements: .NET 10 SDK, PostgreSQL 16

```bash
cd cv-asp.net
docker compose up db -d     # PostgreSQL via Docker (port 5443)
dotnet run --project src/CvAspNet.Web   # Database__Password (and ConnectionStrings__CvDatabase) as in .env.example
```

## Tests

```bash
cd cv-spring-kotlin && ./gradlew test
cd cv-asp.net && dotnet test
```

**.NET:** the same cases as below, ported to xUnit + NSubstitute; the repository tests use SQLite in-memory
instead of H2. On top of that: API tests on the real application (`WebApplicationFactory`, SQLite), the poker
REST + WebSocket protocol end to end, the shared pages being served, and the order of the shared migrations.

**Kotlin:**

Unit tests (`ProfileServiceTest`, `PandaGameServiceTest`) and controller tests
(`ProfileControllerTest`, `PandaGameControllerTest`) use MockK.
Repository tests (`RepositoryTest`, `PandaScoreRepositoryTest`) use an H2 in-memory database 
no PostgreSQL required.
Poker tests (`GameEngineTest`, `HandEvaluatorTest`, `HandRankTest`) cover the game engine
and hand evaluation. The integration test (`PokerGameIntegrationTest`) verifies a complete hand flow.

---

## Roadmap

- [x] Project 01  Kotlin + Spring Boot + Docker
- [x] Project 01  PostgreSQL + Flyway integration
- [x] Project 01  REST API + unit tests
- [x] Project 02  Panda Game
- [x] Project 02  Leaderboard (PostgreSQL + REST API)
- [ ] Project 02  Leaderboard visualisation
- [x] Project 03  Poker Game (Texas Hold'em engine + WebSocket)
- [x] Project 03  Bots (RandomBot) + engine & integration tests
- [ ] Project 03  Advanced bot difficulty levels
- [ ] Project 03  Multiplayer
- [x] Project 04  C# + ASP.NET Core backend (parity with Kotlin, shared pages and migrations)
- [ ] Deploy to hosting

---

## License

[MIT](LICENSE)
