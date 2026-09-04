# Installing into an existing Java project

The installer detects an existing `build.gradle.kts` / `pom.xml` and switches to harness-only
mode: it writes `dev`, the hooks, checkstyle config, `.claude/`, CI, ADRs and `ArchitectureTest`,
and leaves your build file alone. Patching an arbitrary build file mechanically is how a working
build gets broken; the edits below are small, and you can see what each one does.

## 1. Add the plugins and checks (Maven)

Four plugin blocks, copied from the bundled `maven/pom.xml`:

```xml
<plugin>
  <groupId>org.apache.maven.plugins</groupId>
  <artifactId>maven-compiler-plugin</artifactId>
  <configuration>
    <fork>true</fork>
    <compilerArgs>
      <arg>-Xlint:all</arg>
      <arg>-parameters</arg>
      <arg>-XDcompilePolicy=simple</arg>
      <arg>--should-stop=ifError=FLOW</arg>
      <!-- the eight -J add-exports / two add-opens args from maven/pom.xml go here -->
      <arg>-Xplugin:ErrorProne -XepOpt:NullAway:AnnotatedPackages=your.base.package -Xep:NullAway:ERROR</arg>
    </compilerArgs>
    <annotationProcessorPaths>
      <path><groupId>com.google.errorprone</groupId><artifactId>error_prone_core</artifactId><version>2.36.0</version></path>
      <path><groupId>com.uber.nullaway</groupId><artifactId>nullaway</artifactId><version>0.12.3</version></path>
    </annotationProcessorPaths>
  </configuration>
  <executions>
    <execution>
      <id>default-testCompile</id>
      <configuration>
        <compilerArgs><arg>-Xlint:all</arg><arg>-parameters</arg></compilerArgs>
        <annotationProcessorPaths combine.self="override"/>
      </configuration>
    </execution>
  </executions>
</plugin>
```

Plus `spotless-maven-plugin` (googleJavaFormat, removeUnusedImports), `maven-checkstyle-plugin`
pointed at `config/checkstyle/checkstyle.xml` with `violationSeverity=warning`, surefire with
`<excludedGroups>it</excludedGroups>`, and failsafe with `<groups>it</groups>`. Add
`archunit-junit5` as a test dependency.

Two things that look optional and are not: `<fork>true</fork>` (without it Maven reports every
Error Prone finding as "An unknown compilation problem occurred", message discarded) and
`.mvn/jvm.config` with the same exports (google-java-format runs in the Maven JVM). The
installer writes `.mvn/jvm.config` for you even in harness-only mode.

Leave `-Werror` out of this first commit; it goes in last, see below.

### Gradle instead

If the repo is on Gradle, the equivalent block is:

```kotlin
plugins {
  id("com.diffplug.spotless") version "6.25.0"
  id("net.ltgt.errorprone") version "4.1.0"
  checkstyle
}

dependencies {
  errorprone("com.google.errorprone:error_prone_core:2.36.0")
  errorprone("com.uber.nullaway:nullaway:0.12.3")
  testImplementation("com.tngtech.archunit:archunit-junit5:1.3.0")
}

spotless { java { googleJavaFormat(); removeUnusedImports(); target("src/**/*.java") } }
checkstyle { toolVersion = "10.20.2"; configFile = file("config/checkstyle/checkstyle.xml"); maxWarnings = 0 }

tasks.withType<JavaCompile>().configureEach {
  options.compilerArgs.addAll(listOf("-Xlint:all", "-parameters"))
  options.errorprone {
    check("NullAway", net.ltgt.gradle.errorprone.CheckSeverity.ERROR)
    option("NullAway:AnnotatedPackages", "<your.base.package>")
  }
}
tasks.named<JavaCompile>("compileTestJava") { options.errorprone.isEnabled.set(false) }
tasks.test { useJUnitPlatform { excludeTags("it") }; failFast = true }
```

plus the `--add-exports` lines from the bundled `gradle/gradle.properties`, and an
`integrationTest` task with `includeTags("it")`.

## 2. Turn the screws in the right order

On a codebase with history, everything at once produces hundreds of findings and the honest
reaction is to switch it all off again. Land it in this order, each as its own commit:

1. `./dev format` -- one large, mechanical, reviewable diff. Do it first and alone.
2. Checkstyle. Delete rules that fight this codebase; keep the ones that caught something.
3. Error Prone at warning level, then NullAway at error level once the main package is clean.
4. **`-Werror` last.** It is the rule that makes the others stick, and the only one that is
   painful to add first.

## 3. Make the architecture rules describe *this* repo

`ArchitectureTest` ships with layer names (`api`, `application`, `domain`, `infrastructure`).
If your packages are named differently, rename the layers -- do not delete the rule. A layered
rule that matches nothing passes silently, which is the failure mode to avoid.

Start with the rules that already hold. For a rule you want but cannot pass yet, use
`ArchRule.allowEmptyShould(true)` or freeze it with ArchUnit `FreezingArchRule`, which records
current violations and fails only on new ones -- much better than deleting the rule.

## 4. Check the loop is still fast

```bash
time ./dev verify
```

Over 60 seconds on an existing codebase usually means the unit test suite is not really a unit
test suite: something in it is starting Spring contexts or touching a database. Tag those `it`
and move them to `./dev verify-it`. On Maven, install `mvnd` before you start cutting checks --
`dev` uses it automatically and it removes the per-run JVM startup. See `troubleshooting.md`.

## 5. Existing CLAUDE.md

The installer writes `CLAUDE.harness.md` beside it rather than overwriting. Merge by hand,
keep the result under 2 KB, and delete anything the repo now says better.
