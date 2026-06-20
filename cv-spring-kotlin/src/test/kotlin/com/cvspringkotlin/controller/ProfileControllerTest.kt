package com.cvspringkotlin.controller

import com.cvspringkotlin.controller.ProfileController
import com.cvspringkotlin.model.ExperienceDto
import com.cvspringkotlin.model.ProfileDto
import com.cvspringkotlin.service.ProfileService
import io.mockk.clearMocks
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.springframework.http.MediaType
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.get

@WebMvcTest(ProfileController::class)
class ProfileControllerTest {

    @TestConfiguration
    class MockConfig {
        @Bean
        fun profileService(): ProfileService = mockk()
    }

    @Autowired lateinit var mockMvc: MockMvc
    @Autowired lateinit var profileService: ProfileService

    private fun buildProfileDto(
        name: String = "Jan Kowalski",
        footerStatus: String = "YELLOW",
        links: Map<String, String> = mapOf(
            "GithubLink" to "https://github.com/test",
            "E-mail"     to "test@example.com"
        ),
        technologies: Map<String, String> = mapOf("Kotlin" to "primary language"),
        stacks: List<String> = listOf("JVM 17", "Docker"),
        experiences: List<ExperienceDto> = emptyList()
    ) = ProfileDto(
        title        = "$name — Backend Developer",
        eyebrow      = "backend developer",
        name         = name,
        role         = "<span>Kotlin</span>",
        bio          = "Test bio",
        footer       = "available",
        footerStatus = footerStatus,
        links        = links,
        technologies = technologies,
        stacks       = stacks,
        experiences  = experiences
    )

    @BeforeEach
    fun setUp() {
        clearMocks(profileService)
    }

    // ── HTTP status ───────────────────────────────────────

    @Test
    fun `GET api profileinfo returns 200`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                status { isOk() }
                content { contentType(MediaType.APPLICATION_JSON) }
            }
    }

    // ── JSON structure ────────────────────────────────────

    @Test
    fun `GET api profileinfo returns correct fields in JSON`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.name")    { value("Jan Kowalski") }
                jsonPath("$.eyebrow") { value("backend developer") }
                jsonPath("$.title")   { value("Jan Kowalski — Backend Developer") }
                jsonPath("$.role")    { value("<span>Kotlin</span>") }
                jsonPath("$.bio")     { value("Test bio") }
                jsonPath("$.footer")  { value("available") }
            }
    }

    // ── Footer status ─────────────────────────────────────

    @Test
    fun `GET api profileinfo returns footerStatus YELLOW by default`() {
        every { profileService.getProfile() } returns buildProfileDto(footerStatus = "YELLOW")

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.footerStatus") { value("YELLOW") }
            }
    }

    @Test
    fun `GET api profileinfo returns footerStatus GREEN`() {
        every { profileService.getProfile() } returns buildProfileDto(footerStatus = "GREEN")

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.footerStatus") { value("GREEN") }
            }
    }

    @Test
    fun `GET api profileinfo returns footerStatus RED`() {
        every { profileService.getProfile() } returns buildProfileDto(footerStatus = "RED")

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.footerStatus") { value("RED") }
            }
    }

    // ── Links ─────────────────────────────────────────────

    @Test
    fun `GET api profileinfo returns links`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.links.GithubLink") { value("https://github.com/test") }
                jsonPath("$.links.E-mail")     { value("test@example.com") }
            }
    }

    @Test
    fun `GET api profileinfo returns technologies`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.technologies.Kotlin") { value("primary language") }
            }
    }

    @Test
    fun `GET api profileinfo returns stacks as array`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.stacks")    { isArray() }
                jsonPath("$.stacks[0]") { value("JVM 17") }
                jsonPath("$.stacks[1]") { value("Docker") }
            }
    }

    @Test
    fun `GET api profileinfo returns experiences as array`() {
        val exp = ExperienceDto(
            company      = "Test Company Ltd.",
            position     = "Developer",
            contractType = "B2B",
            dateFrom     = "2023-01-01",
            dateTo       = null,
            isCurrent    = true,
            description  = "Description"
        )
        every { profileService.getProfile() } returns buildProfileDto(experiences = listOf(exp))

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.experiences")                 { isArray() }
                jsonPath("$.experiences[0].company")      { value("Test Company Ltd.") }
                jsonPath("$.experiences[0].position")     { value("Developer") }
                jsonPath("$.experiences[0].contractType") { value("B2B") }
                jsonPath("$.experiences[0].isCurrent")    { value(true) }
            }
    }

    @Test
    fun `GET api profileinfo returns empty experiences list when none exist`() {
        every { profileService.getProfile() } returns buildProfileDto(experiences = emptyList())

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.experiences") { isArray() }
                jsonPath("$.experiences") { isEmpty() }
            }
    }

    // ── Service calls ─────────────────────────────────────

    @Test
    fun `GET api profileinfo calls service exactly once`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo").andExpect { status { isOk() } }

        verify(exactly = 1) { profileService.getProfile() }
    }
}