# 🌐 WebCV

> Portfolio & CV w formie aplikacji webowej — zbiór projektów demonstrujących różne technologie.

![Kotlin](https://img.shields.io/badge/Kotlin-7F52FF?style=flat&logo=kotlin&logoColor=white)
![Spring Boot](https://img.shields.io/badge/Spring_Boot-6DB33F?style=flat&logo=springboot&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=flat&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)

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
> Status: 🚧 W toku

Podstrona z minigierką w stylu Dino z Google Chrome — panda skacząca przez przeszkody.
Gra napisana w czystym HTML + CSS + JavaScript, serwowana przez ten sam serwer Spring Boot.

📁 Katalog: `/cv-spring-kotlin/src/main/resources/static/panda-game`

**Technologie:**
- HTML5 Canvas
- CSS3 + animacje
- JavaScript (vanilla)

---

## Uruchomienie

**Wymagania:** Docker

```bash
# Klonowanie repozytorium
git clone https://github.com/NorArax-NeflinOwl/webcv.git
cd webcv/cv-spring-kotlin

# Uruchomienie całego stacku (aplikacja + baza danych)
docker compose up --build
```

Aplikacja dostępna pod adresem: `http://localhost:8080`

| Endpoint | Opis |
|---|---|
| `http://localhost:8080/` | Strona CV |
| `http://localhost:8080/panda-game/` | Minigierka Panda |
| `http://localhost:8080/api/profileinfo` | Dane profilu (JSON) |
| `http://localhost:8080/api/hello` | Hello World |
| `http://localhost:8080/actuator/health` | Status aplikacji |

---

## Uruchomienie lokalne (bez Dockera)

**Wymagania:** JDK 17+, PostgreSQL 16

```bash
# Uruchom PostgreSQL lokalnie lub przez Docker
docker compose up db -d

# Uruchom aplikację
./gradlew bootRun

#albo wszystko razem
docker compose up --build -d
```

## Testy

```bash
./gradlew test
```

Testy jednostkowe (`ProfileServiceTest`) i kontrolera (`ProfileControllerTest`) używają MockK.
Testy repozytoriów (`RepositoryTest`) używają bazy H2 in-memory — nie wymagają PostgreSQL.

---

## Roadmap

- [x] Projekt 01 — Kotlin + Spring Boot + Docker
- [x] Projekt 01 — Integracja PostgreSQL + Flyway
- [x] Projekt 01 — REST API + testy jednostkowe
- [x] Projekt 02 — Panda Game
- [ ] Wdrożenie na hosting

---

## Licencja

[MIT](LICENSE)
