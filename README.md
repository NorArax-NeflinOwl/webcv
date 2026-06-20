# 🌐 WebCV

> Portfolio & CV as a web application — a collection of projects showcasing different technologies.

![Kotlin](https://img.shields.io/badge/Kotlin-7F52FF?style=flat&logo=kotlin&logoColor=white)
![Spring Boot](https://img.shields.io/badge/Spring_Boot-6DB33F?style=flat&logo=springboot&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=flat&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)
![WebSocket](https://img.shields.io/badge/WebSocket-010101?style=flat&logo=socketdotio&logoColor=white)
![Coroutines](https://img.shields.io/badge/Coroutines-7F52FF?style=flat&logo=kotlin&logoColor=white)
![Flyway](https://img.shields.io/badge/Flyway-CC0200?style=flat&logo=flyway&logoColor=white)
![Gradle](https://img.shields.io/badge/Gradle-02303A?style=flat&logo=gradle&logoColor=white)
![HTML5](https://img.shields.io/badge/HTML5-E34F26?style=flat&logo=html5&logoColor=white)

---

## About

A series of mini-projects building an interactive CV as a web application. Each project focuses on a different technology stack — from backend and containerisation to real-time communication and frontend. The application will eventually be deployed on a public server as a skills showcase.

---

## Projects

### 01 — CV Page: Kotlin + Spring Boot + PostgreSQL + Docker
> Status: ✅ Done

A web server serving the CV page. Backend written in Kotlin with Spring Boot, data stored in PostgreSQL with Flyway migrations, containerised with Docker.

📁 Directory: `/cv-spring-kotlin`

**Technologies:**
- Kotlin + Spring Boot (Spring MVC, Spring Data JPA, Spring Actuator)
- PostgreSQL + Flyway (migrations) + Hibernate + HikariCP
- Docker + Docker Compose
- Gradle (build tool)
- JUnit 5 + MockK + H2 (unit & integration tests)
- HTML / CSS / JavaScript (frontend — CV page)
- REST API (`/api/profileinfo`, `/api/hello`, `/actuator/health`)

---

### 02 — Panda Game
> Status: 🚧 In progress

A mini-game in the style of the Google Chrome Dino — a panda jumping over obstacles.
Built with plain HTML + CSS + JavaScript, served by the same Spring Boot server.
Player scores are saved to PostgreSQL (`panda_game_player_score`) and displayed as a leaderboard.

📁 Directory: `/cv-spring-kotlin/src/main/resources/templates/pandaGame.html`
📁 Directory: `/cv-spring-kotlin/src/main/resources/static/css/pandaGameStyle.css`
📁 Directory: `/cv-spring-kotlin/src/main/resources/static/js/pandaGame.js`

**Technologies:**
- HTML5 Canvas
- CSS3 + animations
- JavaScript (vanilla)
- Spring MVC + Spring Data JPA (saving & reading scores)
- PostgreSQL + Flyway (migration `V1`)
- JUnit 5 + MockK + H2 (service, controller & repository tests)
- REST API (`/api/pandagame/scores`)

---

### 03 — Poker Game
> Status: 🚧 In progress

Real-time multiplayer Texas Hold'em Poker. The game engine is written in Kotlin with Coroutines;
communication runs over WebSocket (Spring WebSocket). One human player sits at the table alongside
two bots (RandomBot). Frontend in plain HTML + CSS + JavaScript.

📁 Directory: `/cv-spring-kotlin/src/main/kotlin/com/pokerkotlin`

**Technologies:**
- Kotlin + Kotlinx Coroutines (game engine: dealing, betting, hand evaluation)
- Spring WebSocket (`TextWebSocketHandler`) — table state pushed after every action
- REST API (`/tables`, `/tables/{id}/join`, `/tables/{id}/start`)
- HTML5 / CSS3 / JavaScript (vanilla) — table view with card rendering
- JUnit 5 + MockK (engine tests, hand evaluation tests, integration tests)

**Protocol:**
```
POST /tables                    → { tableId }           (creates a table with 2 bots)
POST /tables/{id}/join          → { sessionToken, playerId }
POST /tables/{id}/start?token=… → 204
WS   /tables/{id}/ws?token=…   ← TableSnapshotDto (pushed on every state change)
                                → { type: "Fold"|"Check"|"Call"|"Raise", amount? }
```

---

## Running the app

**Requirements:** Docker

```bash
# Clone the repository
git clone https://github.com/NorArax-NeflinOwl/webcv.git
cd webcv/cv-spring-kotlin

# Start the full stack (app + database)
docker compose up --build -d

# Tear everything down and remove images
docker compose down -v --rmi all
```

Application available at: `http://localhost:8080`

| Endpoint | Description |
|---|---|
| `http://localhost:8080/` | CV page |
| `http://localhost:8080/panda-game/` | Panda mini-game |
| `http://localhost:8080/poker-game/` | Poker Game |
| `http://localhost:8080/api/profileinfo` | Profile data (JSON) |
| `http://localhost:8080/api/hello` | Hello World |
| `http://localhost:8080/api/pandagame/scores` | Panda Game scores (GET — all scores, POST — save score) |
| `http://localhost:8080/tables` | List poker tables (GET) / Create table (POST) |
| `http://localhost:8080/tables/{id}/join` | Join a table (POST) |
| `http://localhost:8080/tables/{id}/start` | Start a hand (POST) |
| `ws://localhost:8080/tables/{id}/ws` | WebSocket — real-time game state |
| `http://localhost:8080/actuator/health` | Application health |

---

## Running locally (without Docker)

**Requirements:** JDK 17+, PostgreSQL 16

```bash
# Start PostgreSQL via Docker
docker compose up db -d

# Run the application
./gradlew bootRun
```

## Tests

```bash
./gradlew test
```

Unit tests (`ProfileServiceTest`, `PandaGameServiceTest`) and controller tests
(`ProfileControllerTest`, `PandaGameControllerTest`) use MockK.
Repository tests (`RepositoryTest`, `PandaScoreRepositoryTest`) use an H2 in-memory database —
no PostgreSQL required.
Poker tests (`GameEngineTest`, `HandEvaluatorTest`, `HandRankTest`) cover the game engine
and hand evaluation. The integration test (`PokerGameIntegrationTest`) verifies a complete hand flow.

---

## Roadmap

- [x] Project 01 — Kotlin + Spring Boot + Docker
- [x] Project 01 — PostgreSQL + Flyway integration
- [x] Project 01 — REST API + unit tests
- [x] Project 02 — Panda Game
- [x] Project 02 — Leaderboard (PostgreSQL + REST API)
- [ ] Project 02 — Leaderboard visualisation
- [x] Project 03 — Poker Game (Texas Hold'em engine + WebSocket)
- [x] Project 03 — Bots (RandomBot) + engine & integration tests
- [ ] Project 03 — Advanced bot difficulty levels
- [ ] Project 03 — Multiplayer
- [ ] Deploy to hosting

---

## License

[MIT](LICENSE)
