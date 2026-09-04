package __PACKAGE__.it;

import org.junit.jupiter.api.Tag;
import org.springframework.boot.test.context.SpringBootTest;

/**
 * Everything slow lives behind this base class: the full application context and the real HTTP
 * stack. Tagged "it" so `./dev verify` stays fast -- an inner loop that takes thirty seconds is one
 * an agent stops running.
 */
@Tag("it")
@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT,
    properties = {"app.security.api-password=test-password"})
public abstract class IntegrationTestBase {

  protected static final String USER = "api";
  protected static final String PASSWORD = "test-password";
}
