package com.cvspringkotlin.controller

import com.cvspringkotlin.model.ProfileDto
import com.cvspringkotlin.service.ProfileService
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController

@RestController
class ProfileController(private val profileService: ProfileService) {

    @GetMapping("/api/hello")
    fun hello(): String = "Hello World from Kotlin + Spring Boot! 🚀"

    @GetMapping("/api/profileinfo")
    fun profile(): ProfileDto {
        return profileService.getProfile();
    }
}