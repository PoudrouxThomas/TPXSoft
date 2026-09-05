package __PACKAGE__;

import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.classes;
import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.fields;
import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noClasses;
import static com.tngtech.archunit.library.Architectures.layeredArchitecture;
import static com.tngtech.archunit.library.GeneralCodingRules.NO_CLASSES_SHOULD_ACCESS_STANDARD_STREAMS;
import static com.tngtech.archunit.library.dependencies.SlicesRuleDefinition.slices;

import com.tngtech.archunit.core.importer.ImportOption;
import com.tngtech.archunit.junit.AnalyzeClasses;
import com.tngtech.archunit.junit.ArchTest;
import com.tngtech.archunit.lang.ArchRule;

/**
 * The architecture, written down where it can fail. These are the rules an agent is most likely to
 * break, they are close to invisible in a diff review, and checking them costs milliseconds --
 * which is the whole argument for keeping them inside verify rather than beside it. A rule that
 * runs only in CI is outside the definition of done.
 *
 * <p>Add a rule when you catch yourself explaining a convention twice.
 */
@AnalyzeClasses(packages = "__PACKAGE__", importOptions = ImportOption.DoNotIncludeTests.class)
class ArchitectureTest {

  @ArchTest
  static final ArchRule LAYERS_POINT_INWARDS =
      layeredArchitecture()
          .consideringOnlyDependenciesInLayers()
          .layer("Api")
          .definedBy("..api..")
          .layer("Application")
          .definedBy("..application..")
          .layer("Domain")
          .definedBy("..domain..")
          .layer("Infrastructure")
          .definedBy("..infrastructure..")
          .whereLayer("Api")
          .mayNotBeAccessedByAnyLayer()
          .whereLayer("Application")
          .mayOnlyBeAccessedByLayers("Api")
          .whereLayer("Infrastructure")
          .mayNotBeAccessedByAnyLayer();

  @ArchTest
  static final ArchRule DOMAIN_IS_FRAMEWORK_FREE =
      noClasses()
          .that()
          .resideInAPackage("..domain..")
          .should()
          .dependOnClassesThat()
          .resideInAnyPackage(
              "org.springframework..",
              "jakarta.persistence..",
              "jakarta.validation..",
              "com.fasterxml.jackson..");

  @ArchTest
  static final ArchRule PERSISTENCE_STAYS_IN_INFRASTRUCTURE =
      noClasses()
          .that()
          .resideOutsideOfPackage("..infrastructure..")
          .should()
          .dependOnClassesThat()
          .resideInAnyPackage("jakarta.persistence..", "org.springframework.data..", "java.sql..");

  @ArchTest
  static final ArchRule CONTROLLERS_DO_NOT_REACH_FOR_REPOSITORIES =
      noClasses()
          .that()
          .resideInAPackage("..api..")
          .should()
          .dependOnClassesThat()
          .haveSimpleNameEndingWith("Repository");

  @ArchTest
  static final ArchRule CONTROLLERS_ARE_NAMED_AND_PLACED_CONSISTENTLY =
      classes()
          .that()
          .areAnnotatedWith(org.springframework.web.bind.annotation.RestController.class)
          .should()
          .resideInAPackage("..api..")
          .andShould()
          .haveSimpleNameEndingWith("Controller");

  @ArchTest
  static final ArchRule NO_FIELD_INJECTION =
      fields()
          .should()
          .notBeAnnotatedWith(org.springframework.beans.factory.annotation.Autowired.class)
          .because("constructor injection makes a missing dependency a compile error");

  @ArchTest static final ArchRule NO_STANDARD_STREAMS = NO_CLASSES_SHOULD_ACCESS_STANDARD_STREAMS;

  @ArchTest
  static final ArchRule USE_JAVA_TIME =
      noClasses()
          .should()
          .dependOnClassesThat()
          .haveFullyQualifiedName("java.util.Date")
          .because("java.time is the one clock in this codebase");

  @ArchTest
  static final ArchRule PACKAGES_ARE_FREE_OF_CYCLES =
      slices().matching("__PACKAGE__.(*)..").should().beFreeOfCycles();
}
