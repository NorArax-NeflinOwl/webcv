package com.example.cvspringkotlin.service

import com.example.cvspringkotlin.model.ProfileDto
import org.springframework.stereotype.Service

@Service
class ProfileService {

    fun getProfile(): ProfileDto {
        return ProfileDto(
            title = "Patryk N Pudwel — Backend Developer",
            eyebrow = "backend developer",
            name = "Patryk Norbert Pudwel",
            role = "<span>Kotlin</span> / Spring Boot / Docker / PostgreSQL",
            bio = "Backend developer z pasją do czystego kodu i skalowalnych systemów. Buduję RESTowe API i mikroserwisy w ekosystemie JVM — od projektu do produkcji w kontenerze.",
            links = mapOf(
                "GithubLink" to "https://github.com/NorArax-NeflinOwl",
                "LinkedIn" to "https://www.linkedin.com/in/ppudwel199527/",
                "RocketJob" to "https://profile.rocketjobs.pl/profile",
                "E-mail" to "pudwel.n.patryk@gmail.com"
            ),
            technologies = mapOf(
                "Kotlin" to "primary language",
                "Spring Boot" to "web / rest api",
                "Docker" to "containerization",
                "PostgreSQL" to "database",
                "Gradle" to "build tool",
                "Git" to "version control"
            ),
            stacks = listOf(
                "JVM 17",
                "Spring MVC",
                "Spring Actuator",
                "Jackson",
                "JUnit 5",
                "MockK",
                "Docker Compose",
                "IntelliJ IDEA",
                "GitHub Actions",
                "REST API"
            ),
            footer = "dostępny do współpracy w ciągu 3 miesiący"
        )
    }
}