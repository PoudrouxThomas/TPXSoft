package __PACKAGE__.it;

import org.junit.jupiter.api.Tag;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.testcontainers.service.connection.ServiceConnection;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;

/**
 * Everything slow lives behind this base class: a real Postgres, the real Flyway migrations, the
 * real HTTP stack. Tagged "it" so `./dev verify` never starts a container -- an inner loop that
 * takes thirty seconds is one an agent stops running.
 */
@Tag("it")
@Testcontainers
@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT,
    properties = {"app.security.api-password=test-password"})
public abstract class IntegrationTestBase {

  @Container @ServiceConnection
  static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>("postgres:16-alpine");

  protected static final String USER = "api";
  protected static final String PASSWORD = "test-password";
}
