package com.example.cvspringkotlin.repository

import com.example.cvspringkotlin.model.entity.*
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest
import org.springframework.boot.test.autoconfigure.orm.jpa.TestEntityManager
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.context.TestPropertySource
import java.time.LocalDate

@DataJpaTest
@ActiveProfiles("test")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.ANY)
@TestPropertySource(properties = [
    "spring.flyway.enabled=false",
    "spring.jpa.hibernate.ddl-auto=create-drop"
])
class RepositoryTest {

    @Autowired lateinit var em: TestEntityManager
    @Autowired lateinit var profileRepository: ProfileRepository
    @Autowired lateinit var profileSkillRepository: ProfileSkillRepository
    @Autowired lateinit var profileStackRepository: ProfileStackRepository
    @Autowired lateinit var experienceRepository: ExperienceRepository

    private lateinit var savedProfile: Profile

    @BeforeEach
    fun setUp() {
        savedProfile = em.persistAndFlush(
            Profile(name = "Jan Kowalski", eyebrow = "backend dev", email = "jan@example.com",
                githubUrl = "https://github.com/test")
        )
        val savedSkill = em.persistAndFlush(Skill(name = "Kotlin", category = "primary language"))
        em.persistAndFlush(ProfileSkill(profile = savedProfile, skill = savedSkill,
            tag = "primary language", sortOrder = 1))

        val savedStackItem = em.persistAndFlush(StackItem(label = "JVM 17", sortOrder = 1))
        em.persistAndFlush(ProfileStack(profile = savedProfile, stackItem = savedStackItem, sortOrder = 1))

        em.persistAndFlush(Experience(profile = savedProfile, company = "Firma Sp. z o.o.",
            position = "Developer", contractType = "B2B",
            dateFrom = LocalDate.of(2023, 1, 1), isCurrent = true))
        em.clear()
    }

    // ── ProfileRepository ─────────────────────────────────

    @Test
    fun `findFirst zwraca profil gdy istnieje`() {
        val result = profileRepository.findFirst()

        assertNotNull(result)
        assertEquals("Jan Kowalski", result!!.name)
        assertEquals("backend dev", result.eyebrow)
    }

    @Test
    fun `findFirst zwraca null gdy brak profilu`() {
        em.entityManager.createQuery("DELETE FROM ProfileSkill").executeUpdate()
        em.entityManager.createQuery("DELETE FROM ProfileStack").executeUpdate()
        em.entityManager.createQuery("DELETE FROM Experience").executeUpdate()
        em.entityManager.createQuery("DELETE FROM Profile").executeUpdate()

        assertNull(profileRepository.findFirst())
    }

    // ── ProfileSkillRepository ────────────────────────────

    @Test
    fun `findByProfileId zwraca skille dla profilu`() {
        val result = profileSkillRepository.findByProfileId(savedProfile.id!!)

        assertEquals(1, result.size)
        assertEquals("Kotlin", result.first().skill.name)
        assertEquals("primary language", result.first().tag)
    }

    @Test
    fun `findByProfileId zwraca pusta liste dla nieznanego profilu`() {
        val inny = em.persistAndFlush(Profile(name = "Inny"))
        assertTrue(profileSkillRepository.findByProfileId(inny.id!!).isEmpty())
    }

    // ── ProfileStackRepository ────────────────────────────

    @Test
    fun `findByProfileId zwraca stack items dla profilu`() {
        val result = profileStackRepository.findByProfileId(savedProfile.id!!)

        assertEquals(1, result.size)
        assertEquals("JVM 17", result.first().stackItem.label)
    }

    // ── ExperienceRepository ──────────────────────────────

    @Test
    fun `findByProfileIdOrderByDateFromDesc zwraca doswiadczenie`() {
        val result = experienceRepository.findByProfileIdOrderByDateFromDesc(savedProfile.id!!)

        assertEquals(1, result.size)
        assertEquals("Firma Sp. z o.o.", result.first().company)
        assertEquals("B2B", result.first().contractType)
        assertTrue(result.first().isCurrent)
    }

    @Test
    fun `findByProfileIdOrderByDateFromDesc sortuje malejaco po dacie`() {
        em.persistAndFlush(Experience(profile = savedProfile, company = "Starsza Firma",
            position = "Junior", dateFrom = LocalDate.of(2020, 1, 1), isCurrent = false))

        val result = experienceRepository.findByProfileIdOrderByDateFromDesc(savedProfile.id!!)

        assertEquals(2, result.size)
        assertEquals("Firma Sp. z o.o.", result[0].company)
        assertEquals("Starsza Firma",    result[1].company)
    }

    @Test
    fun `findByProfileIdOrderByDateFromDesc zwraca pusta liste dla innego profilu`() {
        val inny = em.persistAndFlush(Profile(name = "Inny"))
        assertTrue(experienceRepository.findByProfileIdOrderByDateFromDesc(inny.id!!).isEmpty())
    }
}