package com.example.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.time.LocalDate
import java.util.UUID

@Entity
@Table(name = "experience")
class Experience(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "profile_id", nullable = false)
    val profile: Profile,

    @Column(nullable = false, length = 256)
    val company: String,

    @Column(nullable = false, length = 256)
    val position: String,

    @Column(name = "contract_type", length = 64)
    val contractType: String? = null,

    @Column(name = "date_from", nullable = false)
    val dateFrom: LocalDate,

    @Column(name = "date_to")
    val dateTo: LocalDate? = null,

    @Column(name = "is_current", nullable = false)
    val isCurrent: Boolean = false,

    @Column(columnDefinition = "TEXT")
    val description: String? = null
)
