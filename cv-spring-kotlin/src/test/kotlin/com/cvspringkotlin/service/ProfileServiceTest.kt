package com.cvspringkotlin.service

import com.cvspringkotlin.model.FooterStatus
import com.cvspringkotlin.model.entity.*
import com.cvspringkotlin.repository.*
import io.mockk.*
import io.mockk.impl.annotations.InjectMockKs
import io.mockk.impl.annotations.MockK
import io.mockk.junit5.MockKExtension
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.assertThrows
import org.junit.jupiter.api.extension.ExtendWith
import java.time.LocalDate
import java.util.UUID

@ExtendWith(MockKExtension::class)
class ProfileServiceTest {

    @MockK lateinit var profileRepository: ProfileRepository
    @MockK lateinit var profileSkillRepository: ProfileSkillRepository
    @MockK lateinit var profileStackRepository: ProfileStackRepository
    @MockK lateinit var experienceRepository: ExperienceRepository

    @InjectMockKs
    lateinit var profileService: ProfileService

    private val profileId = UUID.randomUUID()

    private val defaultProfile = Profile(
        id           = profileId,
        name         = "Jan Kowalski",
        eyebrow      = "backend developer",
        role         = "<span>Kotlin</span>",
        bio          = "Test bio",
        footer       = "available",
        footerStatus = FooterStatus.YELLOW,
        email        = "test@example.com",
        githubUrl    = "https://github.com/test",
        linkedinUrl  = "https://linkedin.com/in/test"
    )

    private val defaultSkills = listOf(
        ProfileSkill(
            profile   = defaultProfile,
            skill     = Skill(name = "Kotlin", category = "primary language"),
            tag       = "primary language",
            sortOrder = 1
        ),
        ProfileSkill(
            profile   = defaultProfile,
            skill     = Skill(name = "Spring Boot", category = "web / rest api"),
            tag       = "web / rest api",
            sortOrder = 2
        )
    )

    private val defaultStacks = listOf(
        ProfileStack(profile = defaultProfile, stackItem = StackItem(label = "JVM 17",  sortOrder = 1), sortOrder = 1),
        ProfileStack(profile = defaultProfile, stackItem = StackItem(label = "Docker",  sortOrder = 2), sortOrder = 2)
    )

    private val defaultExperience = Experience(
        profile      = defaultProfile,
        company      = "Test Company Ltd.",
        position     = "Developer",
        contractType = "B2B",
        dateFrom     = LocalDate.of(2023, 1, 1),
        isCurrent    = true,
        description  = "Description"
    )

    @BeforeEach
    fun setUp() {
        every { profileRepository.findFirst() } returns defaultProfile
        every { profileSkillRepository.findByProfileId(profileId) } returns defaultSkills
        every { profileStackRepository.findByProfileId(profileId) } returns defaultStacks
        every { experienceRepository.findByProfileIdOrderByDateFromDesc(profileId) } returns listOf(defaultExperience)
    }

    // ── Basic data ────────────────────────────────────────

    @Test
    fun `getProfile returns correct basic data`() {
        val result = profileService.getProfile()

        assertEquals("Jan Kowalski — Backend Developer", result.title)
        assertEquals("backend developer", result.eyebrow)
        assertEquals("Jan Kowalski", result.name)
        assertEquals("<span>Kotlin</span>", result.role)
        assertEquals("Test bio", result.bio)
        assertEquals("available", result.footer)
    }

    // ── Footer status ─────────────────────────────────────

    @Test
    fun `getProfile returns YELLOW footerStatus by default`() {
        val result = profileService.getProfile()

        assertEquals("YELLOW", result.footerStatus)
    }

    @Test
    fun `getProfile returns GREEN footerStatus`() {
        every { profileRepository.findFirst() } returns defaultProfile.copy(footerStatus = FooterStatus.GREEN)

        val result = profileService.getProfile()

        assertEquals("GREEN", result.footerStatus)
    }

    @Test
    fun `getProfile returns RED footerStatus`() {
        every { profileRepository.findFirst() } returns defaultProfile.copy(footerStatus = FooterStatus.RED)

        val result = profileService.getProfile()

        assertEquals("RED", result.footerStatus)
    }

    // ── Links ─────────────────────────────────────────────

    @Test
    fun `getProfile returns correct links`() {
        val result = profileService.getProfile()

        assertEquals("https://github.com/test", result.links["GithubLink"])
        assertEquals("https://linkedin.com/in/test", result.links["LinkedIn"])
        assertEquals("test@example.com", result.links["E-mail"])
    }

    @Test
    fun `getProfile skips null and empty links`() {
        every { profileRepository.findFirst() } returns defaultProfile.copy(githubUrl = null, linkedinUrl = "")

        val result = profileService.getProfile()

        assertFalse(result.links.containsKey("GithubLink"))
        assertFalse(result.links.containsKey("LinkedIn"))
        assertTrue(result.links.containsKey("E-mail"))
    }

    @Test
    fun `getProfile returns empty links when no contact data`() {
        every { profileRepository.findFirst() } returns defaultProfile.copy(
            githubUrl = null, linkedinUrl = null, email = null
        )

        val result = profileService.getProfile()

        assertTrue(result.links.isEmpty())
    }

    // ── Technologies and stack ────────────────────────────

    @Test
    fun `getProfile returns correct technologies`() {
        val result = profileService.getProfile()

        assertEquals("primary language", result.technologies["Kotlin"])
        assertEquals("web / rest api", result.technologies["Spring Boot"])
    }

    @Test
    fun `getProfile returns correct stack`() {
        val result = profileService.getProfile()

        assertEquals(listOf("JVM 17", "Docker"), result.stacks)
    }

    // ── Null values ───────────────────────────────────────

    @Test
    fun `getProfile returns empty strings when fields are null`() {
        every { profileRepository.findFirst() } returns defaultProfile.copy(
            eyebrow = null, role = null, bio = null, footer = null
        )

        val result = profileService.getProfile()

        assertEquals("", result.eyebrow)
        assertEquals("", result.role)
        assertEquals("", result.bio)
        assertEquals("", result.footer)
    }

    // ── Experience ────────────────────────────────────────

    @Test
    fun `getProfile returns correct experience`() {
        val result = profileService.getProfile()

        assertEquals(1, result.experiences.size)
        val exp = result.experiences.first()
        assertEquals("Test Company Ltd.", exp.company)
        assertEquals("Developer", exp.position)
        assertEquals("B2B", exp.contractType)
        assertEquals("2023-01-01", exp.dateFrom)
        assertNull(exp.dateTo)
        assertTrue(exp.isCurrent)
    }

    @Test
    fun `getProfile experience dateTo is set when job has ended`() {
        every { experienceRepository.findByProfileIdOrderByDateFromDesc(profileId) } returns listOf(
            defaultExperience.copy(isCurrent = false, dateTo = LocalDate.of(2024, 6, 30))
        )

        val result = profileService.getProfile()

        assertEquals("2024-06-30", result.experiences.first().dateTo)
        assertFalse(result.experiences.first().isCurrent)
    }

    // ── Errors ────────────────────────────────────────────

    @Test
    fun `getProfile throws exception when profile not in database`() {
        every { profileRepository.findFirst() } returns null

        val ex = assertThrows<IllegalStateException> { profileService.getProfile() }
        assertEquals("Profile not found in database", ex.message)
    }

    @Test
    fun `getProfile returns empty technologies when no skills`() {
        every { profileSkillRepository.findByProfileId(profileId) } returns emptyList()

        assertTrue(profileService.getProfile().technologies.isEmpty())
    }

    @Test
    fun `getProfile returns empty stacks when no stack items`() {
        every { profileStackRepository.findByProfileId(profileId) } returns emptyList()

        assertTrue(profileService.getProfile().stacks.isEmpty())
    }

    @Test
    fun `getProfile returns empty experiences when none exist`() {
        every { experienceRepository.findByProfileIdOrderByDateFromDesc(profileId) } returns emptyList()

        assertTrue(profileService.getProfile().experiences.isEmpty())
    }

    // ── Call verification ─────────────────────────────────

    @Test
    fun `getProfile calls all repositories exactly once`() {
        profileService.getProfile()

        verify(exactly = 1) { profileRepository.findFirst() }
        verify(exactly = 1) { profileSkillRepository.findByProfileId(profileId) }
        verify(exactly = 1) { profileStackRepository.findByProfileId(profileId) }
        verify(exactly = 1) { experienceRepository.findByProfileIdOrderByDateFromDesc(profileId) }
    }

    @Test
    fun `getProfile does not call other repositories when profile is missing`() {
        every { profileRepository.findFirst() } returns null

        assertThrows<IllegalStateException> { profileService.getProfile() }

        verify(exactly = 0) { profileSkillRepository.findByProfileId(any()) }
        verify(exactly = 0) { profileStackRepository.findByProfileId(any()) }
        verify(exactly = 0) { experienceRepository.findByProfileIdOrderByDateFromDesc(any()) }
    }
}