package com.cvspringkotlin.service

import com.cvspringkotlin.model.ExperienceDto
import com.cvspringkotlin.model.ProfileDto
import com.cvspringkotlin.repository.*
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional

@Service
class ProfileService(
    private val profileRepository: ProfileRepository,
    private val profileSkillRepository: ProfileSkillRepository,
    private val profileStackRepository: ProfileStackRepository,
    private val experienceRepository: ExperienceRepository
) {

    @Transactional(readOnly = true)
    fun getProfile(): ProfileDto {
        val profile = profileRepository.findFirst()
            ?: error("Brak profilu w bazie danych")

        val id = profile.id!!

        // Każda kolekcja ładowana osobnym zapytaniem — brak MultipleBagFetchException
        val skills  = profileSkillRepository.findByProfileId(id)
        val stacks  = profileStackRepository.findByProfileId(id)
        val experiences = experienceRepository.findByProfileIdOrderByDateFromDesc(id)

        return ProfileDto(
            title   = "${profile.name} — Backend Developer",
            eyebrow = profile.eyebrow ?: "",
            name    = profile.name,
            role    = profile.role ?: "",
            bio     = profile.bio ?: "",
            footer  = profile.footer ?: "",
            links   = buildLinksMap(profile),
            technologies = skills.associate { it.skill.name to (it.tag ?: it.skill.category) },
            stacks  = stacks.map { it.stackItem.label },
            experiences = experiences.map { exp ->
                ExperienceDto(
                    company      = exp.company,
                    position     = exp.position,
                    contractType = exp.contractType,
                    dateFrom     = exp.dateFrom.toString(),
                    dateTo       = exp.dateTo?.toString(),
                    isCurrent    = exp.isCurrent,
                    description  = exp.description
                )
            }
        )
    }

    private fun buildLinksMap(profile: com.cvspringkotlin.model.entity.Profile): Map<String, String> =
        buildMap {
            profile.githubUrl?.takeIf   { it.isNotBlank() }?.let { put("GithubLink", it) }
            profile.linkedinUrl?.takeIf { it.isNotBlank() }?.let { put("LinkedIn",   it) }
            profile.email?.takeIf       { it.isNotBlank() }?.let { put("E-mail",     it) }
        }
}