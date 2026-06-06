package com.example.cvspringkotlin.model

data class ProfileDto(
    val title: String,
    val eyebrow: String,
    val name: String,
    val role: String,
    val bio: String,
    val links: Map<String, String>,
    val technologies: Map<String, String>,
    val stacks: List<String>,
    val footer: String
)
