# 🌐 WebCV

> Portfolio & CV w formie aplikacji webowej — zbiór projektów demonstrujących różne technologie.

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

## O projekcie

Repozytorium zawiera serię mini-projektów budujących interaktywne CV w formie strony
internetowej. Każdy projekt skupia się na innym zestawie technologii — od backendu,
przez konteneryzację, po frontend. Docelowo aplikacja zostanie wdrożona na zewnętrznym
serwerze i udostępniona publicznie jako prezentacja umiejętności.

---

## Projekty

### 01 — Strona CV: Kotlin + Spring Boot + PostgreSQL + Docker
> Status: ✅ Gotowy

Serwer WWW serwujący stronę CV. Backend napisany w Kotlinie z frameworkiem Spring Boot,
dane przechowywane w PostgreSQL z migracjami Flyway, skonteneryzowany przy pomocy Dockera.

📁 Katalog: `/cv-spring-kotlin`

**Technologie:**
- Kotlin + Spring Boot (Spring MVC, Spring Data JPA, Spring Actuator)
- PostgreSQL + Flyway (migracje) + Hibernate + HikariCP
- Docker + Docker Compose
- Gradle (build tool)
- JUnit 5 + MockK + H2 (testy jednostkowe i integracyjne)
- HTML / CSS / JavaScript (frontend — strona CV)
- REST API (`/api/profileinfo`, `/api/hello`, `/actuator/health`)

---

### 02 — Panda Game
> Status: 🚧 W trakcie

Podstrona z minigierką w stylu Dino z Google Chrome — panda skacząca przez przeszkody.
Gra napisana w czystym HTML + CSS + JavaScript, serwowana przez ten sam serwer Spring Boot.
Wyniki graczy zapisywane są w bazie PostgreSQL (tabela `panda_game_player_score`) i prezentowane
jako tablica wyników.

📁 Katalog: `/cv-spring-kotlin/src/main/resources/templates/pandaGame.html`
📁 Katalog: `/cv-spring-kotlin/src/main/resources/static/css/pandaGameStyle.css`
📁 Katalog: `/cv-spring-kotlin/src/main/resources/static/js/pandaGame.js`

**Technologie:**
- HTML5 Canvas
- CSS3 + animacje
- JavaScript (vanilla)
- Spring MVC + Spring Data JPA (zapis i odczyt wyników)
- PostgreSQL + Flyway (migracja `V1`)
- JUnit 5 + MockK + H2 (testy serwisu, kontrolera i repozytorium)
- REST API (`/api/pandagame/scores`)

---

### 03 — Poker Game
> Status: 🚧 W trakcie

Multiplayer Texas Hold'em Poker w czasie rzeczywistym. Silnik gry napisany w Kotlinie
z Coroutines, komunikacja przez WebSocket (Spring WebSocket). Przy stole siada gracz
i dwóch botów (RandomBot). Frontend w czystym HTML + CSS + JavaScript.

📁 Katalog: `/cv-spring-kotlin/src/main/kotlin/com/pokerkotlin`

**Technologie:**
- Kotlin + Kotlinx Coroutines (silnik gry: rozdania, zakłady, ocena rąk)
- Spring WebSocket (`TextWebSocketHandler`) — push stanu stołu po każdej akcji
- REST API (`/api/pokertable`, `/api/pokertable/{id}/join`, `/api/pokertable/{id}/start`)
- HTML5 / CSS3 / JavaScript (vanilla) — frontend z widokiem stołu i kartami
- JUnit 5 + MockK (testy silnika, oceny rąk, integracyjne)

**Protokół:**
```
POST /api/pokertable                    → { tableId }           (tworzy stół z 2 botami)
POST /api/pokertable/{id}/join          → { sessionToken, playerId }
POST /api/pokertable/{id}/start?token=… → 204
WS   /api/pokertable/{id}/ws?token=…   ← TableSnapshotDto (push po każdej zmianie stanu)
                                → { type: "Fold"|"Check"|"Call"|"Raise", amount? }
```

---

## Uruchomienie

**Wymagania:** Docker

```bash
# Klonowanie repozytorium
git clone https://github.com/NorArax-NeflinOwl/webcv.git
cd webcv/cv-spring-kotlin

# Uruchomienie całego stacku (aplikacja + baza danych)
docker compose up --build -d 

#ubicie wszystkiego wraz z usunięciem images
docker compose down -v --rmi all
```

Aplikacja dostępna pod adresem: `http://localhost:8080`

| Endpoint | Opis |
|---|---|
| `http://localhost:8080/` | Strona CV |
| `http://localhost:8080/panda-game/` | Minigierka Panda |
| `http://localhost:8080/poker-game/` | Poker Game |
| `http://localhost:8080/api/profileinfo` | Dane profilu (JSON) |
| `http://localhost:8080/api/hello` | Hello World |
| `http://localhost:8080/api/pandagame/scores` | Wyniki Panda Game (GET — wszystkie wyniki, POST — zapis wyniku) |
| `http://localhost:8080/api/pokertable` | Lista stołów pokerowych (GET) / Nowy stół (POST) |
| `http://localhost:8080/api/pokertable/{id}/join` | Dołącz do stołu (POST) |
| `http://localhost:8080/api/pokertable/{id}/start` | Rozpocznij rozdanie (POST) |
| `ws://localhost:8080/api/pokertable/{id}/ws` | WebSocket — stan gry w czasie rzeczywistym |
| `http://localhost:8080/actuator/health` | Status aplikacji |

---

## Uruchomienie lokalne (bez Dockera)

**Wymagania:** JDK 17+, PostgreSQL 16

```bash
# Uruchom PostgreSQL lokalnie lub przez Docker
docker compose up db -d

# Uruchom aplikację
./gradlew bootRun
```

## Testy

```bash
./gradlew test
```

Testy jednostkowe (`ProfileServiceTest`, `PandaGameServiceTest`) i kontrolerów
(`ProfileControllerTest`, `PandaGameControllerTest`) używają MockK.
Testy repozytoriów (`RepositoryTest`, `PandaScoreRepositoryTest`) używają bazy H2 in-memory —
nie wymagają PostgreSQL.
Testy pokera (`GameEngineTest`, `HandEvaluatorTest`, `HandRankTest`) weryfikują silnik gry
i ocenę rąk. Test integracyjny (`PokerGameIntegrationTest`) sprawdza pełny przebieg rozdania.

---

## Roadmap

- [x] Projekt 01 — Kotlin + Spring Boot + Docker
- [x] Projekt 01 — Integracja PostgreSQL + Flyway
- [x] Projekt 01 — REST API + testy jednostkowe
- [x] Projekt 02 — Panda Game
- [x] Projekt 02 — Tablica wyników (PostgreSQL + REST API)
- [ ] Projekt 02 — Tablica wyników (Wizualizacja)
- [x] Projekt 03 — Poker Game (silnik Texas Hold'em + WebSocket)
- [x] Projekt 03 — Boty (RandomBot) + testy silnika i integracyjne
- [ ] Projekt 03 — Nowe poziomy trudności Botów
- [ ] Projekt 03 — Multiplayer
- [ ] Wdrożenie na hosting

---

## Licencja

[MIT](LICENSE)
