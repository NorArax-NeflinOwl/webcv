package com.cvspringkotlin.model

data class ProfileDto(
    val title: String,
    val eyebrow: String,
    val name: String,
    val role: String,
    val bio: String,
    val links: Map<String, String>,
    val technologies: Map<String, String>,
    val stacks: List<String>,
    val footer: String,
    val footerStatus: String,          // "GREEN" | "YELLOW" | "RED"
    val experiences: List<ExperienceDto> = emptyList()
)

data class ExperienceDto(
    val company: String,
    val position: String,
    val contractType: String?,
    val dateFrom: String,
    val dateTo: String?,
    val isCurrent: Boolean,
    val description: String?
)
