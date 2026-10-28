# 🌐 WebCV

> Portfolio & CV w formie aplikacji webowej  zbiór projektów demonstrujących różne technologie.

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

## O projekcie

Repozytorium zawiera serię mini-projektów budujących interaktywne CV w formie strony
internetowej. Każdy projekt skupia się na innym zestawie technologii  od backendu,
przez konteneryzację, po frontend. Docelowo aplikacja zostanie wdrożona na zewnętrznym
serwerze i udostępniona publicznie jako prezentacja umiejętności.

Ta sama aplikacja istnieje w dwóch wersjach: dwa wymienne backendy za jednym frontendem:

```
webcv/
├── cv-shared/          strony (HTML/CSS/JS) i migracje SQL  wspólne dla obu backendów
├── cv-spring-kotlin/   backend: Kotlin + Spring Boot          → http://localhost:8280
├── cv-asp.net/         backend: C# + ASP.NET Core             → http://localhost:8281
└── run webcv.ps1       buduje i (re)startuje backendy w Dockerze
```

Kontrakt, który musi spełnić backend, opisuje [cv-shared/README.md](cv-shared/README.md).

---

## Projekty

### 01  Strona CV: Kotlin + Spring Boot + PostgreSQL + Docker
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
- HTML / CSS / JavaScript (frontend  strona CV)
- REST API (`/api/profileinfo`, `/api/hello`, `/actuator/health`)

---

### 02  Panda Game
> Status: 🚧 W trakcie

Podstrona z minigierką w stylu Dino z Google Chrome  panda skacząca przez przeszkody.
Gra napisana w czystym HTML + CSS + JavaScript, serwowana przez ten sam serwer Spring Boot.
Wyniki graczy zapisywane są w bazie PostgreSQL (tabela `panda_game_player_score`) i prezentowane
jako tablica wyników.

📁 Katalog: `/cv-shared/web/pandaGame.html`
📁 Katalog: `/cv-shared/web/css/pandaGameStyle.css`
📁 Katalog: `/cv-shared/web/js/pandaGame.js`

**Technologie:**
- HTML5 Canvas
- CSS3 + animacje
- JavaScript (vanilla)
- Spring MVC + Spring Data JPA (zapis i odczyt wyników)
- PostgreSQL + Flyway (migracja `V1`)
- JUnit 5 + MockK + H2 (testy serwisu, kontrolera i repozytorium)
- REST API (`/api/pandagame/scores`)

---

### 03  Poker Game
> Status: 🚧 W trakcie

Multiplayer Texas Hold'em Poker w czasie rzeczywistym. Silnik gry napisany w Kotlinie
z Coroutines, komunikacja przez WebSocket (Spring WebSocket). Przy stole siada gracz
i dwóch botów (RandomBot). Frontend w czystym HTML + CSS + JavaScript.

📁 Katalog: `/cv-spring-kotlin/src/main/kotlin/com/pokerkotlin`

**Technologie:**
- Kotlin + Kotlinx Coroutines (silnik gry: rozdania, zakłady, ocena rąk)
- Spring WebSocket (`TextWebSocketHandler`)  push stanu stołu po każdej akcji
- REST API (`/api/pokertable`, `/api/pokertable/{id}/join`, `/api/pokertable/{id}/start`)
- HTML5 / CSS3 / JavaScript (vanilla)  frontend z widokiem stołu i kartami
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

### 04  Strona CV: C# + ASP.NET Core + PostgreSQL + Docker
> Status: ✅ Gotowy

To samo CV, Panda Game i Poker Game z backendem .NET: te same strony i te same migracje SQL
(z `cv-shared`), to samo API REST/WebSocket, te same testy przeniesione z Kotlina.

📁 Katalog: `/cv-asp.net`

**Technologie:**
- C# 14 + ASP.NET Core 10 (kontrolery MVC, pliki statyczne, WebSockety, health checks)
- PostgreSQL + EF Core (Npgsql) + DbUp (wykonuje skrypty w konwencji Flyway z `cv-shared/db/migration`)
- `CvAspNet.Poker`  silnik Texas Hold'em jako osobna biblioteka (bez ASP.NET i bazy)
- Docker + Docker Compose (własna baza na porcie 5443)
- xUnit + NSubstitute + SQLite in-memory + `WebApplicationFactory` (testy jednostkowe, repozytoriów, API i WebSocketu)

---

## Uruchomienie

**Wymagania:** Docker (oraz PowerShell dla `run webcv.ps1`)

```powershell
# Klonowanie repozytorium
git clone https://github.com/NorArax-NeflinOwl/webcv.git
cd webcv

# Budowa z bieżącego kodu i (re)start obu backendów; dane zostają w wolumenach baz
# (nazwa ma spację, stąd operator wywołania & i cudzysłów)
& ".\run webcv.ps1"                         # oba: Kotlin na :8280, .NET na :8281
& ".\run webcv.ps1" -Backend dotnet         # albo tylko jeden (kotlin | dotnet)
& ".\run webcv.ps1" -Backend kotlin -Logs   # i zostań podpięty do jego logu
```

Przy pierwszym uruchomieniu `run webcv.ps1` tworzy `.env` każdego backendu z `.env.example` z losowym hasłem do bazy.
Bez skryptu każdy backend nadal uruchamia się samodzielnie:

```bash
cd cv-spring-kotlin             # albo cv-asp.net
docker compose up --build -d    # wymaga .env (skopiuj .env.example)

#ubicie wszystkiego wraz z usunięciem images
docker compose down -v --rmi all
```

Aplikacja dostępna pod adresem: `http://localhost:8280` (Kotlin) i `http://localhost:8281` (.NET)  endpointy są te same:

| Endpoint | Opis |
|---|---|
| `http://localhost:8280/` | Strona CV |
| `http://localhost:8280/pandaGame` | Minigierka Panda |
| `http://localhost:8280/pokerGame` | Poker Game |
| `http://localhost:8280/api/profileinfo` | Dane profilu (JSON) |
| `http://localhost:8280/api/hello` | Hello World |
| `http://localhost:8280/api/backend` | Który backend serwuje stronę (etykieta w stopce) |
| `http://localhost:8280/api/pandagame/scores` | Wyniki Panda Game (GET  wszystkie wyniki, POST  zapis wyniku) |
| `http://localhost:8280/api/pokertable` | Lista stołów pokerowych (GET) / Nowy stół (POST) |
| `http://localhost:8280/api/pokertable/{id}/join` | Dołącz do stołu (POST) |
| `http://localhost:8280/api/pokertable/{id}/start` | Rozpocznij rozdanie (POST) |
| `ws://localhost:8280/api/pokertable/{id}/ws` | WebSocket  stan gry w czasie rzeczywistym |
| `http://localhost:8280/actuator/health` | Status aplikacji |

### Na serwerze domowym (za bramą)

Publiczne wdrożenie to backend .NET w Dockerze na domowym serwerze z Linuksem, za bramą na Raspberry Pi, której
Caddy kończy TLS i przekazuje ruch dalej (także `wss:` - strona wybiera schemat WebSocketa z własnego adresu).
Różnica jest jedna: gdzie publikowane są porty, ustawiane w `cv-asp.net/.env` (wzór w `.env.example`) - aplikacja na
adresie LAN serwera, do którego dostęp ma tylko brama, a baza tylko na loopbacku.

```bash
cd cv-asp.net
# .env jak w .env.example, z losowym POSTGRES_PASSWORD, a do tego:
#   APP_HOST_PORT=192.168.1.5:8281
#   POSTGRES_HOST_PORT=127.0.0.1:5443
sudo docker compose up --build -d
```

---

## Uruchomienie lokalne (bez Dockera)

**Kotlin**  wymagania: JDK 21, PostgreSQL 16

```bash
cd cv-spring-kotlin
docker compose up db -d     # PostgreSQL przez Docker (port 5442)
./gradlew bootRun           # DATABASE_URL / DATABASE_USERNAME / DATABASE_PASSWORD jak w .env.example
```

**.NET**  wymagania: .NET 10 SDK, PostgreSQL 16

```bash
cd cv-asp.net
docker compose up db -d     # PostgreSQL przez Docker (port 5443)
dotnet run --project src/CvAspNet.Web   # Database__Password (i ConnectionStrings__CvDatabase) jak w .env.example
```

## Testy

```bash
cd cv-spring-kotlin && ./gradlew test
cd cv-asp.net && dotnet test
```

**.NET:** te same przypadki co niżej, przeniesione na xUnit + NSubstitute; testy repozytoriów używają SQLite
in-memory zamiast H2. Dodatkowo: testy API na prawdziwej aplikacji (`WebApplicationFactory`, SQLite), protokół
pokera REST + WebSocket od początku do końca, serwowanie wspólnych stron i kolejność wspólnych migracji.

**Kotlin:**

Testy jednostkowe (`ProfileServiceTest`, `PandaGameServiceTest`) i kontrolerów
(`ProfileControllerTest`, `PandaGameControllerTest`) używają MockK.
Testy repozytoriów (`RepositoryTest`, `PandaScoreRepositoryTest`) używają bazy H2 in-memory 
nie wymagają PostgreSQL.
Testy pokera (`GameEngineTest`, `HandEvaluatorTest`, `HandRankTest`) weryfikują silnik gry
i ocenę rąk. Test integracyjny (`PokerGameIntegrationTest`) sprawdza pełny przebieg rozdania.

---

## Roadmap

- [x] Projekt 01  Kotlin + Spring Boot + Docker
- [x] Projekt 01  Integracja PostgreSQL + Flyway
- [x] Projekt 01  REST API + testy jednostkowe
- [x] Projekt 02  Panda Game
- [x] Projekt 02  Tablica wyników (PostgreSQL + REST API)
- [ ] Projekt 02  Tablica wyników (Wizualizacja)
- [x] Projekt 03  Poker Game (silnik Texas Hold'em + WebSocket)
- [x] Projekt 03  Boty (RandomBot) + testy silnika i integracyjne
- [ ] Projekt 03  Nowe poziomy trudności Botów
- [ ] Projekt 03  Multiplayer
- [x] Projekt 04  Backend C# + ASP.NET Core (zgodny z Kotlinem, wspólne strony i migracje)
- [ ] Wdrożenie na hosting

---

## Licencja

[MIT](LICENSE)
