// The foojay resolver lets Gradle download the toolchain JDK itself, so a fresh machine
// (or a CI runner, or a container) does not need the exact JDK installed up front.
plugins {
  id("org.gradle.toolchains.foojay-resolver-convention") version "0.8.0"
}

rootProject.name = "__ARTIFACT__"
