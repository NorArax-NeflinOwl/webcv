package com.cvspringkotlin.repository

import com.cvspringkotlin.model.entity.*
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
    @Autowired lateinit var profileTranslationRepository: ProfileTranslationRepository
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
        em.persistAndFlush(ProfileSkill(profile = savedProfile, backend = "kotlin", skill = savedSkill,
            tag = "primary language", sortOrder = 1))
        val dotnetSkill = em.persistAndFlush(Skill(name = "C#", category = "primary language"))
        em.persistAndFlush(ProfileSkill(profile = savedProfile, backend = "dotnet", skill = dotnetSkill,
            tag = "primary language", sortOrder = 1))

        val savedStackItem = em.persistAndFlush(StackItem(label = "JVM 17", sortOrder = 1))
        em.persistAndFlush(ProfileStack(profile = savedProfile, backend = "kotlin", stackItem = savedStackItem, sortOrder = 1))
        val dotnetStackItem = em.persistAndFlush(StackItem(label = ".NET 10", sortOrder = 1))
        em.persistAndFlush(ProfileStack(profile = savedProfile, backend = "dotnet", stackItem = dotnetStackItem, sortOrder = 1))

        em.persistAndFlush(ProfileTranslation(profile = savedProfile, backend = "kotlin", language = "pl", bio = "Bio Kotlin"))
        em.persistAndFlush(ProfileTranslation(profile = savedProfile, backend = "dotnet", language = "pl", bio = "Bio .NET"))

        em.persistAndFlush(Experience(profile = savedProfile, company = "Firma Sp. z o.o.",
            position = "Developer", contractType = "B2B",
            dateFrom = LocalDate.of(2023, 1, 1), isCurrent = true))
        em.clear()
    }

    //  ProfileRepository 

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
        em.entityManager.createQuery("DELETE FROM ProfileTranslation").executeUpdate()
        em.entityManager.createQuery("DELETE FROM Experience").executeUpdate()
        em.entityManager.createQuery("DELETE FROM Profile").executeUpdate()

        assertNull(profileRepository.findFirst())
    }

    //  ProfileTranslationRepository

    @Test
    fun `findByProfileIdAndBackendAndLanguage zwraca tlumaczenie danego backendu`() {
        assertEquals("Bio Kotlin", profileTranslationRepository.findByProfileIdAndBackendAndLanguage(savedProfile.id!!, "kotlin", "pl")?.bio)
        assertEquals("Bio .NET", profileTranslationRepository.findByProfileIdAndBackendAndLanguage(savedProfile.id!!, "dotnet", "pl")?.bio)
        assertNull(profileTranslationRepository.findByProfileIdAndBackendAndLanguage(savedProfile.id!!, "kotlin", "en"))
    }

    //  ProfileSkillRepository

    @Test
    fun `findByProfileIdAndBackend zwraca skille dla profilu i backendu`() {
        val result = profileSkillRepository.findByProfileIdAndBackend(savedProfile.id!!, "kotlin")

        assertEquals(1, result.size)
        assertEquals("Kotlin", result.first().skill.name)
        assertEquals("primary language", result.first().tag)
    }

    @Test
    fun `findByProfileIdAndBackend zwraca skille innego backendu osobno`() {
        val result = profileSkillRepository.findByProfileIdAndBackend(savedProfile.id!!, "dotnet")

        assertEquals(listOf("C#"), result.map { it.skill.name })
    }

    @Test
    fun `findByProfileIdAndBackend zwraca pusta liste dla nieznanego profilu`() {
        val inny = em.persistAndFlush(Profile(name = "Inny"))
        assertTrue(profileSkillRepository.findByProfileIdAndBackend(inny.id!!, "kotlin").isEmpty())
    }

    //  ProfileStackRepository

    @Test
    fun `findByProfileIdAndBackend zwraca stack items dla profilu i backendu`() {
        val result = profileStackRepository.findByProfileIdAndBackend(savedProfile.id!!, "kotlin")

        assertEquals(1, result.size)
        assertEquals("JVM 17", result.first().stackItem.label)
    }

    @Test
    fun `findByProfileIdAndBackend zwraca stack items innego backendu osobno`() {
        val result = profileStackRepository.findByProfileIdAndBackend(savedProfile.id!!, "dotnet")

        assertEquals(listOf(".NET 10"), result.map { it.stackItem.label })
    }

    //  ExperienceRepository 

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