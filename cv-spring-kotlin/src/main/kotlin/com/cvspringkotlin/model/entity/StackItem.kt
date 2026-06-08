package com.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "stack_item")
data class StackItem(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @Column(nullable = false, length = 128)
    val label: String = "",

    @Column(name = "sort_order", nullable = false)
    val sortOrder: Int = 0,

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "icon_id")
    val icon: Icon? = null
)