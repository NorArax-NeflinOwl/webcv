package com.example.cvspringkotlin.controller

import com.example.cvspringkotlin.model.ExperienceDto
import com.example.cvspringkotlin.model.ProfileDto
import com.example.cvspringkotlin.service.ProfileService
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
        bio          = "Bio testowe",
        footer       = "dostępny",
        links        = links,
        technologies = technologies,
        stacks       = stacks,
        experiences  = experiences
    )

    @BeforeEach
    fun setUp() {
        // Resetuj licznik wywołań MockK między testami
        clearMocks(profileService)
    }

    // ── HTTP status ───────────────────────────────────────

    @Test
    fun `GET api profileinfo zwraca 200`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                status { isOk() }
                content { contentType(MediaType.APPLICATION_JSON) }
            }
    }

    // ── Struktura JSON ────────────────────────────────────

    @Test
    fun `GET api profileinfo zwraca poprawne pola w JSON`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.name")    { value("Jan Kowalski") }
                jsonPath("$.eyebrow") { value("backend developer") }
                jsonPath("$.title")   { value("Jan Kowalski — Backend Developer") }
                jsonPath("$.role")    { value("<span>Kotlin</span>") }
                jsonPath("$.bio")     { value("Bio testowe") }
                jsonPath("$.footer")  { value("dostępny") }
            }
    }

    @Test
    fun `GET api profileinfo zwraca linki`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.links.GithubLink") { value("https://github.com/test") }
                jsonPath("$.links.E-mail")     { value("test@example.com") }
            }
    }

    @Test
    fun `GET api profileinfo zwraca technologie`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.technologies.Kotlin") { value("primary language") }
            }
    }

    @Test
    fun `GET api profileinfo zwraca stacks jako tablice`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.stacks")    { isArray() }
                jsonPath("$.stacks[0]") { value("JVM 17") }
                jsonPath("$.stacks[1]") { value("Docker") }
            }
    }

    @Test
    fun `GET api profileinfo zwraca experiences jako tablice`() {
        val exp = ExperienceDto(
            company      = "Firma Sp. z o.o.",
            position     = "Developer",
            contractType = "B2B",
            dateFrom     = "2023-01-01",
            dateTo       = null,
            isCurrent    = true,
            description  = "Opis"
        )
        every { profileService.getProfile() } returns buildProfileDto(experiences = listOf(exp))

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.experiences")                 { isArray() }
                jsonPath("$.experiences[0].company")      { value("Firma Sp. z o.o.") }
                jsonPath("$.experiences[0].position")     { value("Developer") }
                jsonPath("$.experiences[0].contractType") { value("B2B") }
                jsonPath("$.experiences[0].isCurrent")    { value(true) }
            }
    }

    @Test
    fun `GET api profileinfo zwraca pusta liste experiences gdy brak`() {
        every { profileService.getProfile() } returns buildProfileDto(experiences = emptyList())

        mockMvc.get("/api/profileinfo")
            .andExpect {
                jsonPath("$.experiences") { isArray() }
                jsonPath("$.experiences") { isEmpty() }
            }
    }

    // ── Wywołania serwisu ─────────────────────────────────

    @Test
    fun `GET api profileinfo wywoluje serwis dokladnie raz`() {
        every { profileService.getProfile() } returns buildProfileDto()

        mockMvc.get("/api/profileinfo").andExpect { status { isOk() } }

        verify(exactly = 1) { profileService.getProfile() }
    }
}