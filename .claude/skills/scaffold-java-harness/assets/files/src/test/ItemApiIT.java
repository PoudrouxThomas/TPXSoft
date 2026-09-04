package __PACKAGE__.it;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.Map;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.web.client.TestRestTemplate;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;

/** End to end over real HTTP: security, serialization and persistence, all at once. */
class ItemApiIT extends IntegrationTestBase {

  @Autowired private TestRestTemplate rest;

  @Test
  void shouldRejectAnonymousAccess() {
    ResponseEntity<String> response = rest.getForEntity("/api/items", String.class);

    assertThat(response.getStatusCode()).isEqualTo(HttpStatus.UNAUTHORIZED);
  }

  @Test
  void shouldCreateThenReadItem() {
    TestRestTemplate authenticated = rest.withBasicAuth(USER, PASSWORD);

    ResponseEntity<String> created =
        authenticated.postForEntity(
            "/api/items",
            Map.of("name", "written", "description", "to a real database"),
            String.class);

    assertThat(created.getStatusCode()).isEqualTo(HttpStatus.CREATED);
    String location = created.getHeaders().getFirst("Location");
    assertThat(location).as("POST must return a Location header").isNotNull();

    ResponseEntity<String> fetched = authenticated.getForEntity(location, String.class);

    assertThat(fetched.getStatusCode()).isEqualTo(HttpStatus.OK);
    assertThat(fetched.getBody()).contains("written");
  }

  @Test
  void shouldExposeHealthWithoutCredentials() {
    ResponseEntity<String> health = rest.getForEntity("/actuator/health", String.class);

    assertThat(health.getStatusCode()).isEqualTo(HttpStatus.OK);
  }
}
