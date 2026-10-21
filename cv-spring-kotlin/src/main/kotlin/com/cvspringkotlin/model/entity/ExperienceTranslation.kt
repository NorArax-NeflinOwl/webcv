package com.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "experience_translation")
data class ExperienceTranslation(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "experience_id", nullable = false)
    val experience: Experience = Experience(),

    @Column(nullable = false, length = 8)
    val language: String = "",

    @Column(nullable = false, length = 256)
    val position: String = "",

    @Column(name = "contract_type", length = 64)
    val contractType: String? = null,

    @Column(columnDefinition = "TEXT")
    val description: String? = null
)
