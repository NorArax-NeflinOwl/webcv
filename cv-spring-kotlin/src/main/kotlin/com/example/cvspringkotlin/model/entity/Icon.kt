package com.example.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "icon")
class Icon(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @Column(nullable = false, unique = true, length = 64)
    val key: String,

    @Column(name = "svg_path", nullable = false, columnDefinition = "TEXT")
    val svgPath: String,

    @Column(length = 128)
    val label: String? = null
)
