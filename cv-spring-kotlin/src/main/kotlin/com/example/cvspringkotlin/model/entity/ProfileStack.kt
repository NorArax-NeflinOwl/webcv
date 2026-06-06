package com.example.cvspringkotlin.model.entity

import jakarta.persistence.*
import java.util.UUID

@Entity
@Table(name = "profile_stack")
data class ProfileStack(

    @Id
    @GeneratedValue(strategy = GenerationType.UUID)
    val id: UUID? = null,

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "profile_id", nullable = false)
    val profile: Profile = Profile(),

    @ManyToOne(fetch = FetchType.EAGER)
    @JoinColumn(name = "stack_item_id", nullable = false)
    val stackItem: StackItem = StackItem(),

    @Column(name = "sort_order", nullable = false)
    val sortOrder: Int = 0
)