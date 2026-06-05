# ── Stage 1: Build ──────────────────────────
FROM eclipse-temurin:21-jdk-alpine AS builder
WORKDIR /app
COPY gradlew build.gradle.kts settings.gradle.kts ./
COPY gradle ./gradle
RUN ./gradlew dependencies --no-daemon # cache warstwy
COPY src ./src
RUN ./gradlew bootJar --no-daemon

# ── Stage 2: Runtime ─────────────────────────
FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY --from=builder /app/build/libs/*.jar app.jar
EXPOSE 8080
ENTRYPOINT ["java", "-jar", "app.jar"]