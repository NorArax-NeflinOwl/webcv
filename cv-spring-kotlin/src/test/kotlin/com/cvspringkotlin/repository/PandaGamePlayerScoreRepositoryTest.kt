package com.cvspringkotlin.repository

import com.cvspringkotlin.model.entity.PandaGamePlayerScore
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest
import org.springframework.boot.test.autoconfigure.orm.jpa.TestEntityManager
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.context.TestPropertySource
import java.time.LocalDateTime

@DataJpaTest
@ActiveProfiles("test")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.ANY)
@TestPropertySource(properties = [
    "spring.flyway.enabled=false",
    "spring.jpa.hibernate.ddl-auto=create-drop"
])
class PandaGamePlayerScoreRepositoryTest {

    @Autowired lateinit var em: TestEntityManager
    @Autowired lateinit var pandaScoreRepository: PandaGamePlayerScoreRepository

    @BeforeEach
    fun setUp() {
        em.entityManager.createQuery("DELETE FROM PandaGamePlayerScore").executeUpdate()
        em.clear()
    }

    @Test
    fun `findTop10ByOrderByScoreDescPlayedAtAsc zwraca wyniki w kolejnosci malejacej`() {
        val now = LocalDateTime.now()
        em.persistAndFlush(PandaGamePlayerScore(nick = "Niski", score = 50, playedAt = now))
        em.persistAndFlush(PandaGamePlayerScore(nick = "Wysoki", score = 300, playedAt = now))
        em.persistAndFlush(PandaGamePlayerScore(nick = "Sredni", score = 150, playedAt = now))
        em.clear()

        val result = pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc()

        assertEquals(3, result.size)
        assertEquals("Wysoki", result[0].nick)
        assertEquals("Sredni", result[1].nick)
        assertEquals("Niski",  result[2].nick)
    }

    @Test
    fun `findTop10ByOrderByScoreDescPlayedAtAsc ogranicza do 10 wynikow`() {
        val now = LocalDateTime.now()
        repeat(15) { i ->
            em.persistAndFlush(PandaGamePlayerScore(nick = "Gracz$i", score = i, playedAt = now))
        }
        em.clear()

        val result = pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc()

        assertEquals(10, result.size)
        assertEquals(14, result.first().score)
    }

    @Test
    fun `findTop10ByOrderByScoreDescPlayedAtAsc przy remisie sortuje po dacie rosnaco`() {
        val older = LocalDateTime.now().minusHours(1)
        val newer = LocalDateTime.now()
        em.persistAndFlush(PandaGamePlayerScore(nick = "Pozniej", score = 100, playedAt = newer))
        em.persistAndFlush(PandaGamePlayerScore(nick = "Wczesniej", score = 100, playedAt = older))
        em.clear()

        val result = pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc()

        assertEquals("Wczesniej", result[0].nick)
        assertEquals("Pozniej",   result[1].nick)
    }

    @Test
    fun `findTop10ByOrderByScoreDescPlayedAtAsc zwraca pusta liste gdy brak wynikow`() {
        assertTrue(pandaScoreRepository.findTop10ByOrderByScoreDescPlayedAtAsc().isEmpty())
    }
}
