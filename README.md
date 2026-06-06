# 🌐 WebCV

> Portfolio & CV w formie aplikacji webowej — zbiór projektów demonstrujących różne technologie.

![Kotlin](https://img.shields.io/badge/Kotlin-7F52FF?style=flat&logo=kotlin&logoColor=white)
![Spring Boot](https://img.shields.io/badge/Spring_Boot-6DB33F?style=flat&logo=springboot&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)

---

## O projekcie

Repozytorium zawiera serię mini-projektów budujących interaktywne CV w formie strony
internetowej. Każdy projekt skupia się na innym zestawie technologii — od backendu,
przez konteneryzację, po frontend. Docelowo aplikacja zostanie wdrożona na zewnętrznym
serwerze i udostępniona publicznie jako prezentacja umiejętności.

---

## Projekty

### 01 — Strona CV: Kotlin + Spring Boot + Docker
> Status: 🚧 W toku

Serwer WWW serwujący stronę CV. Backend napisany w Kotlinie z frameworkiem Spring Boot,
skonteneryzowany przy pomocy Dockera. Projekt stanowi podstawę całej aplikacji.

📁 Katalog: `/cv-spring-kotlin`

**Technologie:**
- Kotlin
- Spring Boot
- Docker

---

## Uruchomienie — projekt 01

**Wymagania:** Docker

```bash
# Klonowanie repozytorium
git clone https://github.com/NorArax-NeflinOwl/webcv.git
cd webcv/cv-spring-kotlin

# Budowanie obrazu
docker compose up --build -d
```

Aplikacja dostępna pod adresem: `http://localhost:8080`

---

## Roadmap

- [x] Projekt 01 — Kotlin + Spring Boot + Docker
- [ ] Projekt 02 — Panda Game
- [ ] Wdrożenie na hosting

---

## Licencja

[MIT](LICENSE)
