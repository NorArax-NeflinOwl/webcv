package com.example.cvspringkotlin.controller

import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController

@RestController
class HelloController {

    @GetMapping("/api/hello")
    fun hello(): String = "Hello World from Kotlin + Spring Boot! 🚀"
}