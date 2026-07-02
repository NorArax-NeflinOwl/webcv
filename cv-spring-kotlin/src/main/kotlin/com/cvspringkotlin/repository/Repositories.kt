package com.cvspringkotlin.repository

import com.cvspringkotlin.model.entity.*
import org.springframework.data.jpa.repository.JpaRepository
import org.springframework.data.jpa.repository.Query
import org.springframework.stereotype.Repository
import java.util.UUID

@Repository
interface ProfileTranslationRepository : JpaRepository<ProfileTranslation, UUID> {
    fun findByProfileIdAndLanguage(profileId: UUID, language: String): ProfileTranslation?
}

@Repository
interface ExperienceTranslationRepository : JpaRepository<ExperienceTranslation, UUID> {

    @Query("SELECT et FROM ExperienceTranslation et WHERE et.experience.id IN :ids AND et.language = :language")
    fun findByExperienceIdsAndLanguage(ids: List<UUID>, language: String): List<ExperienceTranslation>
}

@Repository
interface ProfileRepository : JpaRepository<Profile, UUID> {

    @Query("SELECT p FROM Profile p")
    fun findFirst(): Profile?
}

@Repository
interface ProfileSkillRepository : JpaRepository<ProfileSkill, UUID> {

    @Query("""
        SELECT ps FROM ProfileSkill ps
        JOIN FETCH ps.skill
        WHERE ps.profile.id = :profileId
        ORDER BY ps.sortOrder
    """)
    fun findByProfileId(profileId: UUID): List<ProfileSkill>
}

@Repository
interface ProfileStackRepository : JpaRepository<ProfileStack, UUID> {

    @Query("""
        SELECT pt FROM ProfileStack pt
        JOIN FETCH pt.stackItem
        WHERE pt.profile.id = :profileId
        ORDER BY pt.sortOrder
    """)
    fun findByProfileId(profileId: UUID): List<ProfileStack>
}

@Repository
interface ExperienceRepository : JpaRepository<Experience, UUID> {

    fun findByProfileIdOrderByDateFromDesc(profileId: UUID): List<Experience>
}

@Repository
interface SkillRepository : JpaRepository<Skill, UUID>

@Repository
interface StackItemRepository : JpaRepository<StackItem, UUID>

@Repository
interface IconRepository : JpaRepository<Icon, UUID> {
    fun findByKey(key: String): Icon?
}

@Repository
interface PandaGamePlayerScoreRepository : JpaRepository<PandaGamePlayerScore, UUID> {

    fun findTop10ByOrderByScoreDescPlayedAtAsc(): List<PandaGamePlayerScore>
}