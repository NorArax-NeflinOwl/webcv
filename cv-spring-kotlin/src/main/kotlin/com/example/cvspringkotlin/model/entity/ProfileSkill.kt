package com.example.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "profile_skill")
class ProfileSkill(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "profile_id", nullable = false)
    val profile: Profile,

    @ManyToOne(fetch = FetchType.EAGER)
    @JoinColumn(name = "skill_id", nullable = false)
    val skill: Skill,

    @Column(length = 128)
    val tag: String? = null,

    @Column(name = "sort_order", nullable = false)
    val sortOrder: Int = 0
)
