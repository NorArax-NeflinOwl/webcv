package com.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "profile_translation")
data class ProfileTranslation(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "profile_id", nullable = false)
    val profile: Profile = Profile(),

    @Column(nullable = false, length = 8)
    val language: String = "",

    @Column(length = 128)
    val eyebrow: String? = null,

    @Column(length = 256)
    val role: String? = null,

    @Column(columnDefinition = "TEXT")
    val bio: String? = null,

    @Column(length = 256)
    val footer: String? = null
)
