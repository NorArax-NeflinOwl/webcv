package com.cvspringkotlin.controller

import org.springframework.stereotype.Controller
import org.springframework.web.bind.annotation.GetMapping

/**
 * The pages are plain HTML from cv-shared/web, served as static resources.
 * These routes only keep the extension-less addresses working.
 */
@Controller
class PageController {

    @GetMapping("/")
    fun index(): String = "forward:/index.html"

    @GetMapping("/pandaGame")
    fun pandaGame(): String = "forward:/pandaGame.html"

    @GetMapping("/pokerGame")
    fun pokerGame(): String = "forward:/pokerGame.html"
}
