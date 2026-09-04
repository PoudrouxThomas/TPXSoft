package __PACKAGE__.it;

import static org.assertj.core.api.Assertions.assertThat;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.web.client.TestRestTemplate;

/**
 * Keeps docs/openapi.json honest.
 *
 * <p>The document is what a frontend generates its client from, so a stale copy is not a
 * documentation problem, it is a broken build in another repository. `./dev openapi` rewrites the
 * file; CI runs this same test without the write flag, so a contract change nobody regenerated
 * fails here instead of drifting quietly for a month.
 */
class OpenApiDocIT extends IntegrationTestBase {

  private static final Path DOC = Path.of("docs", "openapi.json");

  @Autowired private TestRestTemplate rest;

  @Test
  void apiDocumentMatchesTheCommittedCopy() throws Exception {
    String pretty = liveDocument();

    if (Boolean.getBoolean("openapi.write")) {
      Files.createDirectories(DOC.getParent());
      Files.writeString(DOC, pretty, StandardCharsets.UTF_8);
      return;
    }

    assertThat(Files.exists(DOC)).as("run ./dev openapi to create %s", DOC).isTrue();
    assertThat(normalize(Files.readString(DOC, StandardCharsets.UTF_8)))
        .as("docs/openapi.json is stale -- run ./dev openapi and commit the result")
        .isEqualTo(pretty);
  }

  /**
   * Two things are stripped so the file is stable across machines and runs: the server list, which
   * carries the random test port, and CRLF, which Jackson emits on Windows. Without that the
   * committed copy could never match and this test would only ever cry wolf.
   */
  private String liveDocument() throws Exception {
    String live = rest.getForObject("/v3/api-docs", String.class);
    assertThat(live).as("springdoc returned no document").isNotBlank();

    ObjectMapper mapper = new ObjectMapper();
    ObjectNode root = (ObjectNode) mapper.readTree(live);
    root.remove("servers");
    return normalize(mapper.writerWithDefaultPrettyPrinter().writeValueAsString(root));
  }

  private static String normalize(String json) {
    return json.replace("\r\n", "\n").stripTrailing() + "\n";
  }
}
