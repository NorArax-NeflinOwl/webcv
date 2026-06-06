package com.example.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "skill")
class Skill(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @Column(nullable = false, length = 128)
    val name: String,

    @Column(nullable = false, length = 64)
    val category: String,

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "icon_id")
    val icon: Icon? = null
)
