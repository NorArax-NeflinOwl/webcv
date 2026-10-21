# Build context: the repository root (see docker-compose.yml), so that cv-shared is reachable.

# Stage 1: Build
FROM eclipse-temurin:21-jdk-alpine AS builder
WORKDIR /app/cv-spring-kotlin
COPY cv-spring-kotlin/gradlew cv-spring-kotlin/build.gradle.kts cv-spring-kotlin/settings.gradle.kts ./
COPY cv-spring-kotlin/gradle ./gradle
RUN ./gradlew dependencies --no-daemon # cache warstwy

# "run webcv.ps1" passes a new BUILD_ID every time, so the code below is compiled on every run even when it did not change.
ARG BUILD_ID=manual
RUN echo "build $BUILD_ID"
COPY cv-shared /app/cv-shared
COPY cv-spring-kotlin/src ./src
RUN ./gradlew bootJar --no-daemon

# Stage 2: Runtime
FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
COPY --from=builder /app/cv-spring-kotlin/build/libs/*.jar app.jar
EXPOSE 8280
ENTRYPOINT ["java", "-jar", "app.jar"]
