package com.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "profile")
data class Profile(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @Column(nullable = false, length = 256)
    val name: String = "",

    @Column(length = 128)
    val eyebrow: String? = null,

    @Column(length = 256)
    val role: String? = null,

    @Column(columnDefinition = "TEXT")
    val bio: String? = null,

    @Column(length = 256)
    val footer: String? = null,

    @Column(length = 256)
    val email: String? = null,

    @Column(name = "github_url", length = 512)
    val githubUrl: String? = null,

    @Column(name = "linkedin_url", length = 512)
    val linkedinUrl: String? = null
)