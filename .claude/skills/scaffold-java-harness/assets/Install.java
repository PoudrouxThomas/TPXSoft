import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.attribute.PosixFilePermission;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

/**
 * Installs the Java agent harness. Run it with the JDK you already have, no build step:
 *
 * <pre>
 *   java Install.java --root . --files &lt;skill&gt;/assets/files --package com.acme.orders
 * </pre>
 *
 * <p>Everything here is mechanical and idempotent, so a partial install can simply be re-run.
 * The judgement -- what the layering should be, whether CLAUDE.md is true, which rules this
 * codebase actually needs -- belongs to the caller, not to this script.
 *
 * <p>Flags: --root . --files &lt;dir&gt; --package com.example.app --name my-api --group
 * com.example --build gradle|maven --java 21 --port 8080 --db postgres|none --db-port 5432
 * --title "My API" --force --harness-only
 */
public final class Install {

  private static final List<String> LOG = new ArrayList<>();

  private static Path root;
  private static Path files;
  private static boolean force;
  private static Map<String, String> vars = new HashMap<>();

  public static void main(String[] args) throws Exception {
    Map<String, String> flags = parse(args);

    root = Path.of(flags.getOrDefault("root", ".")).toAbsolutePath().normalize();
    String filesFlag = flags.get("files");
    if (filesFlag == null) {
      fail("--files <skill-dir>/assets/files is required so the installer can find its templates");
    }
    files = Path.of(filesFlag).toAbsolutePath().normalize();
    if (!Files.isDirectory(files)) {
      fail("--files does not point at a directory: " + files);
    }
    force = flags.containsKey("force");

    String pkg = flags.getOrDefault("package", "com.example.app");
    String artifact = flags.getOrDefault("name", root.getFileName().toString());
    String group = flags.getOrDefault("group", pkg.contains(".") ? pkg.substring(0, pkg.lastIndexOf('.')) : pkg);
    // Maven is the default: it is what most Spring Boot teams already run, and a forgotten
    // --build flag scaffolding the other tool is a worse failure than a slower inner loop.
    String build = flags.getOrDefault("build", "maven").toLowerCase(Locale.ROOT);
    boolean maven = build.equals("maven");
    boolean db = !"none".equals(flags.getOrDefault("db", "postgres"));
    String dbName = artifact.replaceAll("[^A-Za-z0-9_]", "_").toLowerCase(Locale.ROOT);

    boolean existingBuild =
        Files.exists(root.resolve("build.gradle.kts"))
            || Files.exists(root.resolve("build.gradle"))
            || Files.exists(root.resolve("pom.xml"));
    boolean harnessOnly = flags.containsKey("harness-only") || existingBuild;

    vars = new LinkedHashMap<>();
    vars.put("__PACKAGE__", pkg);
    vars.put("__GROUP__", group);
    vars.put("__ARTIFACT__", artifact);
    vars.put("__APP_TITLE__", flags.getOrDefault("title", artifact));
    vars.put("__JAVA__", flags.getOrDefault("java", "21"));
    vars.put("__PORT__", flags.getOrDefault("port", "8080"));
    vars.put("__DB_NAME__", dbName);
    vars.put("__DB_PORT__", flags.getOrDefault("db-port", "5432"));
    vars.put("__DATE__", LocalDate.now().toString());
    vars.put("__ECOSYSTEM__", maven ? "maven" : "gradle");
    vars.put("__CACHE_STEP__", maven ? MAVEN_CACHE_STEP : GRADLE_CACHE_STEP);
    // Without a database there is no transaction manager, so an @Transactional service would
    // fail at the first call rather than at compile time. Cheaper to leave the annotation out.
    vars.put("__TX_IMPORT__", db ? "import org.springframework.transaction.annotation.Transactional;" : "");
    vars.put("__TX_READ__", db ? "@Transactional(readOnly = true)" : "");
    vars.put("__TX_WRITE__", db ? "@Transactional" : "");
    vars.put("__DB_DEPS__", db ? (maven ? MAVEN_DB_DEPS : GRADLE_DB_DEPS) : "");
    vars.put("__DB_TEST_DEPS__", db ? (maven ? MAVEN_DB_TEST_DEPS : GRADLE_DB_TEST_DEPS) : "");

    String pkgPath = pkg.replace('.', '/');
    String main = "src/main/java/" + pkgPath;
    String test = "src/test/java/" + pkgPath;

    // ------------------------------------------------------------ the harness

    copy("dev", "dev", true);
    makeExecutable(root.resolve("dev"));
    copy("dev.cmd", "dev.cmd", true);
    copy("tools/HookGuard.java", "tools/harness/HookGuard.java", true);
    copy("tools/HookSelfTest.java", "tools/harness/HookSelfTest.java", true);
    copy("protected-paths.txt", "tools/harness/protected-paths.txt", false);
    copy("config/checkstyle.xml", "config/checkstyle/checkstyle.xml", false);
    copy("gitattributes", ".gitattributes", false);

    copy("claude/settings.json", ".claude/settings.json", false, ".claude/settings.harness.json");
    copy("claude/launch.json", ".claude/launch.json", false);
    copy("claude/agent-java-investigator.md", ".claude/agents/java-investigator.md", false);
    copy("CLAUDE.md", "CLAUDE.md", false, "CLAUDE.harness.md");

    copy("ci/workflow-verify.yml", ".github/workflows/verify.yml", false);
    copy("ci/dependabot.yml", ".github/dependabot.yml", false);
    copy("adr/0001-record-architecture-decisions.md", "docs/adr/0001-record-architecture-decisions.md", false);
    copy("adr/0002-the-verification-loop.md", "docs/adr/0002-the-verification-loop.md", false);

    copy("src/test/ArchitectureTest.java", test + "/ArchitectureTest.java", false);

    // ------------------------------------------------------- build + scaffold

    if (harnessOnly) {
      note(existingBuild ? "note    existing build file kept -- add the harness plugins by hand" : "note    harness-only install");
      // The exports are additive and harmless to an existing build, and they are the thing
      // people hit first when they add Error Prone or google-java-format to it.
      if (Files.exists(root.resolve("pom.xml"))) {
        copy("maven/jvm.config", ".mvn/jvm.config", false);
      }
    } else if (maven) {
      copy("maven/pom.xml", "pom.xml", false);
      // Error Prone and google-java-format both reach into javac internals; on JDK 16+ the
      // build JVM needs these exports or the very first verify dies with an IllegalAccessError.
      copy("maven/jvm.config", ".mvn/jvm.config", false);
    } else {
      copy("gradle/build.gradle.kts", "build.gradle.kts", false);
      copy("gradle/settings.gradle.kts", "settings.gradle.kts", false);
      copy("gradle/gradle.properties", "gradle.properties", false);
    }

    if (!harnessOnly) {
      copy("src/main/Application.java", main + "/Application.java", false);
      copy("src/main/SecurityConfig.java", main + "/config/SecurityConfig.java", false);
      copy("src/main/OpenApiConfig.java", main + "/config/OpenApiConfig.java", false);
      copy("src/main/ItemController.java", main + "/api/ItemController.java", false);
      copy("src/main/ApiExceptionHandler.java", main + "/api/ApiExceptionHandler.java", false);
      copy("src/main/CreateItemRequest.java", main + "/api/dto/CreateItemRequest.java", false);
      copy("src/main/ItemResponse.java", main + "/api/dto/ItemResponse.java", false);
      copy("src/main/ItemService.java", main + "/application/ItemService.java", false);
      copy("src/main/Item.java", main + "/domain/Item.java", false);
      copy("src/main/ItemRepository.java", main + "/domain/ItemRepository.java", false);
      copy("src/main/ItemNotFoundException.java", main + "/domain/ItemNotFoundException.java", false);

      if (db) {
        copy("src/main/ItemEntity.java", main + "/infrastructure/persistence/ItemEntity.java", false);
        copy("src/main/ItemJpaRepository.java", main + "/infrastructure/persistence/ItemJpaRepository.java", false);
        copy("src/main/JpaItemRepository.java", main + "/infrastructure/persistence/JpaItemRepository.java", false);
        copy("src/resources/V1__create_items.sql", "src/main/resources/db/migration/V1__create_items.sql", false);
        copy("src/resources/compose.yaml", "compose.yaml", false);
        copy("src/resources/application-db.yml", "src/main/resources/application.yml", false);
      } else {
        copy("src/main/InMemoryItemRepository.java", main + "/infrastructure/persistence/InMemoryItemRepository.java", false);
        copy("src/resources/application-nodb.yml", "src/main/resources/application.yml", false);
      }

      copy("src/test/ItemServiceTest.java", test + "/application/ItemServiceTest.java", false);
      copy("src/test/ItemControllerTest.java", test + "/api/ItemControllerTest.java", false);
      copy("src/test/IntegrationTestBase-" + (db ? "db" : "nodb") + ".java", test + "/it/IntegrationTestBase.java", false);
      copy("src/test/ItemApiIT.java", test + "/it/ItemApiIT.java", false);
      copy("src/test/OpenApiDocIT.java", test + "/it/OpenApiDocIT.java", false);
    }

    gitignore(maven);

    // ------------------------------------------------------------------ report

    System.out.println(String.join("\n", LOG));
    System.out.println();
    System.out.println("next:");
    int step = 1;
    if (!harnessOnly && !maven && !Files.exists(root.resolve("gradlew"))) {
      System.out.println("  " + step++ + ". gradle wrapper --gradle-version 8.12   (no wrapper here yet)");
    }
    if (!harnessOnly && maven && !Files.exists(root.resolve("mvnw"))) {
      System.out.println("  " + step++ + ". mvn -N wrapper:wrapper                 (no wrapper here yet)");
    }
    if (harnessOnly) {
      System.out.println("  " + step++ + ". add the build additions from references/existing-project.md");
    }
    System.out.println("  " + step++ + ". ./dev format && ./dev verify           (expect real findings on existing code)");
    System.out.println("  " + step++ + ". java tools/harness/HookSelfTest.java    (prove the write guard blocks)");
    System.out.println("  " + step + ". ./dev openapi                          (write docs/openapi.json, then commit it)");
  }

  // ------------------------------------------------------------------ helpers

  private static final String GRADLE_DB_DEPS =
      """
        implementation("org.springframework.boot:spring-boot-starter-data-jpa")
        implementation("org.flywaydb:flyway-core")
        implementation("org.flywaydb:flyway-database-postgresql")
        runtimeOnly("org.postgresql:postgresql")""";

  private static final String GRADLE_DB_TEST_DEPS =
      """
        testImplementation("org.springframework.boot:spring-boot-testcontainers")
        testImplementation("org.testcontainers:junit-jupiter")
        testImplementation("org.testcontainers:postgresql")""";

  private static final String MAVEN_DB_DEPS =
      """
          <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-data-jpa</artifactId></dependency>
          <dependency><groupId>org.flywaydb</groupId><artifactId>flyway-core</artifactId></dependency>
          <dependency><groupId>org.flywaydb</groupId><artifactId>flyway-database-postgresql</artifactId></dependency>
          <dependency><groupId>org.postgresql</groupId><artifactId>postgresql</artifactId><scope>runtime</scope></dependency>""";

  private static final String MAVEN_DB_TEST_DEPS =
      """
          <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-testcontainers</artifactId><scope>test</scope></dependency>
          <dependency><groupId>org.testcontainers</groupId><artifactId>junit-jupiter</artifactId><scope>test</scope></dependency>
          <dependency><groupId>org.testcontainers</groupId><artifactId>postgresql</artifactId><scope>test</scope></dependency>""";

  private static final String GRADLE_CACHE_STEP = "      - uses: gradle/actions/setup-gradle@v4";

  // Plain concatenation, not a text block: the workflow is indentation-sensitive YAML and a
  // text block strips incidental indentation, which silently produced an unparseable file.
  private static final String MAVEN_CACHE_STEP =
      "      - uses: actions/cache@v4\n"
          + "        with:\n"
          + "          path: ~/.m2/repository\n"
          + "          key: maven-${{ hashFiles('**/pom.xml') }}";

  private static Map<String, String> parse(String[] args) {
    Map<String, String> flags = new LinkedHashMap<>();
    for (int i = 0; i < args.length; i++) {
      if (!args[i].startsWith("--")) {
        continue;
      }
      String key = args[i].substring(2);
      String value = (i + 1 < args.length && !args[i + 1].startsWith("--")) ? args[++i] : "true";
      flags.put(key, value);
    }
    return flags;
  }

  /**
   * Substitutes placeholders. A placeholder whose value is empty takes its whole line with it:
   * a stray blank line inside an import block is a formatter violation, so the first
   * `./dev verify` of a fresh install would fail for a reason that has nothing to do with code.
   */
  private static String substitute(String text) {
    String out = text;
    for (Map.Entry<String, String> entry : vars.entrySet()) {
      if (entry.getValue().isEmpty()) {
        out = out.replaceAll("(?m)^[ \\t]*" + entry.getKey() + "[ \\t]*\\r?\\n", "");
      }
      out = out.replace(entry.getKey(), entry.getValue());
    }
    return out;
  }

  /** Copies one template, substituting placeholders. Never silently overwrites your work. */
  private static void copy(String from, String to, boolean alwaysOverwrite) throws IOException {
    copy(from, to, alwaysOverwrite, null);
  }

  private static void copy(String from, String to, boolean alwaysOverwrite, String fallback)
      throws IOException {
    Path source = files.resolve(from);
    if (!Files.exists(source)) {
      note("MISSING template " + from);
      return;
    }
    Path target = root.resolve(to);
    if (Files.exists(target) && !alwaysOverwrite && !force) {
      if (fallback == null) {
        note("kept    " + to + " (already exists)");
        return;
      }
      Files.createDirectories(root.resolve(fallback).getParent());
      Files.writeString(root.resolve(fallback), render(source), StandardCharsets.UTF_8);
      note("wrote   " + fallback + " (" + to + " exists -- merge by hand)");
      return;
    }
    boolean existed = Files.exists(target);
    Files.createDirectories(target.getParent());
    Files.writeString(target, render(source), StandardCharsets.UTF_8);
    note((existed ? "updated " : "wrote   ") + to);
  }

  /**
   * Reads a template, substitutes, and forces LF. Line endings are not cosmetic here: the
   * formatter check compares them, so a CRLF copy written on Windows fails the very first
   * verify for a reason that has nothing to do with the code.
   */
  private static String render(Path source) throws IOException {
    String text = substitute(Files.readString(source, StandardCharsets.UTF_8));
    return text.replace("\r\n", "\n").replace("\r", "\n");
  }

  private static void makeExecutable(Path path) {
    try {
      Set<PosixFilePermission> perms = Files.getPosixFilePermissions(path);
      perms.add(PosixFilePermission.OWNER_EXECUTE);
      perms.add(PosixFilePermission.GROUP_EXECUTE);
      perms.add(PosixFilePermission.OTHERS_EXECUTE);
      Files.setPosixFilePermissions(path, perms);
    } catch (IOException | UnsupportedOperationException windows) {
      note("note    could not chmod +x dev (fine on Windows; on git: git update-index --chmod=+x dev)");
    }
  }

  private static void gitignore(boolean maven) throws IOException {
    Path path = root.resolve(".gitignore");
    List<String> wanted =
        maven
            ? List.of("target/", "*.log", ".env", ".DS_Store", "bin/")
            : List.of("build/", ".gradle/", "*.log", ".env", ".DS_Store", "bin/");
    List<String> current = Files.exists(path) ? Files.readAllLines(path) : new ArrayList<>();
    List<String> missing = new ArrayList<>(wanted);
    missing.removeAll(current);
    if (missing.isEmpty()) {
      return;
    }
    List<String> merged = new ArrayList<>(current);
    merged.addAll(missing);
    Files.write(path, merged, StandardCharsets.UTF_8);
    note("patched .gitignore (" + String.join(", ", missing) + ")");
  }

  private static void note(String line) {
    LOG.add(line);
  }

  private static void fail(String message) {
    System.err.println("install: " + message);
    System.exit(1);
  }

  private Install() {}
}
