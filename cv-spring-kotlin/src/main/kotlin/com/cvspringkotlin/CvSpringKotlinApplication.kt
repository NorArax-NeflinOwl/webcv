package com.cvspringkotlin

import org.springframework.boot.autoconfigure.SpringBootApplication
import org.springframework.boot.runApplication

@SpringBootApplication(scanBasePackages = ["com.cvspringkotlin", "com.pokerkotlin"])
class CvSpringKotlinApplication

fun main(args: Array<String>) {
    runApplication<CvSpringKotlinApplication>(*args)
}
