import net.ltgt.gradle.errorprone.CheckSeverity
import net.ltgt.gradle.errorprone.errorprone

// Everything here exists to make `./dev verify` fast, quiet and honest.
// Versions are pinned on purpose: an unpinned tool turns a green build red on
// somebody else's machine on a day nobody changed any code.
plugins {
  java
  id("org.springframework.boot") version "3.5.0"
  id("io.spring.dependency-management") version "1.1.7"
  id("com.diffplug.spotless") version "6.25.0"
  id("net.ltgt.errorprone") version "4.1.0"
  checkstyle
}

group = "__GROUP__"
version = "0.1.0-SNAPSHOT"

java {
  toolchain { languageVersion = JavaLanguageVersion.of(__JAVA__) }
}

repositories { mavenCentral() }

dependencies {
  implementation("org.springframework.boot:spring-boot-starter-web")
  implementation("org.springframework.boot:spring-boot-starter-validation")
  implementation("org.springframework.boot:spring-boot-starter-security")
  implementation("org.springframework.boot:spring-boot-starter-actuator")
  implementation("org.springdoc:springdoc-openapi-starter-webmvc-ui:2.8.6")
__DB_DEPS__
  compileOnly("org.jspecify:jspecify:1.0.0")

  errorprone("com.google.errorprone:error_prone_core:2.36.0")
  errorprone("com.uber.nullaway:nullaway:0.12.3")

  testImplementation("org.springframework.boot:spring-boot-starter-test")
  testImplementation("org.springframework.security:spring-security-test")
  testImplementation("com.tngtech.archunit:archunit-junit5:1.3.0")
__DB_TEST_DEPS__
  testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

// Warnings as errors is the rule that stops "I will clean that up later", which
// agents say and never do. NullAway turns the whole main source set into a
// null-checked one at compile time -- no test required, no runtime surprise.
tasks.withType<JavaCompile>().configureEach {
  options.compilerArgs.addAll(listOf("-Xlint:all", "-Werror", "-parameters"))
  options.errorprone {
    disableWarningsInGeneratedCode.set(true)
    excludedPaths.set(".*/build/generated/.*")
    check("NullAway", CheckSeverity.ERROR)
    option("NullAway:AnnotatedPackages", "__PACKAGE__")
    // Fields the framework fills in after construction: JPA entities and injected beans
    // never initialize them in a constructor, and NullAway would otherwise be right but useless.
    option(
        "NullAway:ExcludedFieldAnnotations",
        "jakarta.persistence.Id,jakarta.persistence.Column,jakarta.persistence.ManyToOne," +
            "jakarta.persistence.OneToMany,jakarta.persistence.JoinColumn," +
            "org.springframework.beans.factory.annotation.Autowired," +
            "org.springframework.beans.factory.annotation.Value")
  }
}
tasks.named<JavaCompile>("compileTestJava") {
  // Tests are allowed to be blunt: mocks, nulls on purpose, deliberate misuse.
  options.errorprone.isEnabled.set(false)
}

spotless {
  java {
    googleJavaFormat()
    removeUnusedImports()
    formatAnnotations()
    target("src/**/*.java")
    targetExclude("build/generated/**")
  }
}

checkstyle {
  toolVersion = "10.20.2"
  configFile = file("config/checkstyle/checkstyle.xml")
  maxWarnings = 0
  isIgnoreFailures = false
}

// Unit + architecture tests. Anything tagged "it" starts a container and belongs
// in `./dev verify-it`, never in the loop an agent runs after every edit.
tasks.test {
  useJUnitPlatform { excludeTags("it") }
  failFast = true
  testLogging {
    events("failed")
    exceptionFormat = org.gradle.api.tasks.testing.logging.TestExceptionFormat.SHORT
    showStackTraces = false
  }
  mustRunAfter("spotlessCheck", "checkstyleMain")
}

val integrationTest by tasks.registering(Test::class) {
  description = "Integration tests (Testcontainers). Not part of verify."
  group = "verification"
  useJUnitPlatform { includeTags("it") }
  testClassesDirs = sourceSets.test.get().output.classesDirs
  classpath = sourceSets.test.get().runtimeClasspath
  systemProperty("openapi.write", System.getProperty("openapi.write", "false"))
  shouldRunAfter(tasks.test)
  outputs.upToDateWhen { false }
}

tasks.named<org.springframework.boot.gradle.tasks.run.BootRun>("bootRun") {
  systemProperty("spring.profiles.active", "dev")
}
