package com.cvspringkotlin.service

import com.cvspringkotlin.model.ExperienceDto
import com.cvspringkotlin.model.ProfileDto
import com.cvspringkotlin.model.entity.Profile
import com.cvspringkotlin.repository.*
import org.springframework.stereotype.Service
import org.springframework.transaction.annotation.Transactional
import java.util.UUID

@Service
class ProfileService(
    private val profileRepository: ProfileRepository,
    private val profileTranslationRepository: ProfileTranslationRepository,
    private val profileSkillRepository: ProfileSkillRepository,
    private val profileStackRepository: ProfileStackRepository,
    private val experienceRepository: ExperienceRepository,
    private val experienceTranslationRepository: ExperienceTranslationRepository
) {

    @Transactional(readOnly = true)
    fun getProfile(language: String = "pl"): ProfileDto {
        val profile = profileRepository.findFirst()
            ?: error("Profile not found in database")

        val id = profile.id!!

        val translation = profileTranslationRepository.findByProfileIdAndLanguage(id, language)
            ?: profileTranslationRepository.findByProfileIdAndLanguage(id, "pl")

        // Each collection loaded in a separate query — avoids MultipleBagFetchException
        val skills      = profileSkillRepository.findByProfileId(id)
        val stacks      = profileStackRepository.findByProfileId(id)
        val experiences = experienceRepository.findByProfileIdOrderByDateFromDesc(id)

        val experienceTranslations = fetchExperienceTranslations(experiences.mapNotNull { it.id }, language)

        return ProfileDto(
            title        = "${profile.name}  Backend Developer",
            eyebrow      = translation?.eyebrow ?: "",
            name         = profile.name,
            role         = translation?.role ?: "",
            bio          = translation?.bio ?: "",
            footer       = translation?.footer ?: "",
            footerStatus = profile.footerStatus.name,
            links        = buildLinksMap(profile),
            technologies = skills.associate { it.skill.name to (it.tag ?: it.skill.category) },
            stacks       = stacks.map { it.stackItem.label },
            experiences  = experiences.map { exp ->
                val expTranslation = experienceTranslations[exp.id]
                ExperienceDto(
                    company      = exp.company,
                    position     = expTranslation?.position ?: exp.position,
                    contractType = expTranslation?.contractType ?: exp.contractType,
                    dateFrom     = exp.dateFrom.toString(),
                    dateTo       = exp.dateTo?.toString(),
                    isCurrent    = exp.isCurrent,
                    description  = expTranslation?.description ?: exp.description
                )
            }
        )
    }

    private fun fetchExperienceTranslations(ids: List<UUID>, language: String) =
        experienceTranslationRepository
            .findByExperienceIdsAndLanguage(ids, language)
            .associateBy { it.experience.id }

    private fun buildLinksMap(profile: Profile): Map<String, String> =
        buildMap {
            profile.githubUrl?.takeIf   { it.isNotBlank() }?.let { put("GithubLink", it) }
            profile.linkedinUrl?.takeIf { it.isNotBlank() }?.let { put("LinkedIn",   it) }
            profile.email?.takeIf       { it.isNotBlank() }?.let { put("E-mail",     it) }
        }
}
