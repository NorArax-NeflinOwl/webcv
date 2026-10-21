package com.cvspringkotlin.controller

import com.cvspringkotlin.model.BackendInfoDto
import com.cvspringkotlin.model.ProfileDto
import com.cvspringkotlin.service.ProfileService
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController

@RestController
class ProfileController(private val profileService: ProfileService) {

    @GetMapping("/api/hello")
    fun hello(): String = "Hello World from Kotlin + Spring Boot! 🚀"

    /** Footer label of the shared CV page: tells which backend serves it. */
    @GetMapping("/api/backend")
    fun backend(): BackendInfoDto = BackendInfoDto(label = BACKEND_LABEL)

    @GetMapping("/api/profileinfo")
    fun profile(@RequestParam(defaultValue = "pl") lang: String): ProfileDto =
        profileService.getProfile(lang)

    companion object {
        const val BACKEND_LABEL = "Kotlin + Spring Boot + Docker  cv-spring-kotlin"
    }
}
