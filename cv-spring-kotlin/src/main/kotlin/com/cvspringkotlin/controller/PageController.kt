package com.cvspringkotlin.controller

import org.springframework.stereotype.Controller
import org.springframework.web.bind.annotation.GetMapping

@Controller
class PageController {

    @GetMapping("/")
    fun index(): String = "index"

    @GetMapping("/pandaGame", "/pandaGame.html")
    fun game(): String = "pandaGame"
}