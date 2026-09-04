package __PACKAGE__.config;

import io.swagger.v3.oas.models.Components;
import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.security.SecurityRequirement;
import io.swagger.v3.oas.models.security.SecurityScheme;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/**
 * The API document is the contract a frontend generates its client from, so it is part of the build
 * rather than a page somebody remembers to look at: `./dev openapi` writes docs/openapi.json and CI
 * fails when the committed copy no longer matches the code.
 */
@Configuration
public class OpenApiConfig {

  @Bean
  OpenAPI apiDocument() {
    return new OpenAPI()
        .info(new Info().title("__APP_TITLE__").version("v1").description("__APP_TITLE__ HTTP API"))
        .components(
            new Components()
                .addSecuritySchemes(
                    "basicAuth",
                    new SecurityScheme().type(SecurityScheme.Type.HTTP).scheme("basic")))
        .addSecurityItem(new SecurityRequirement().addList("basicAuth"));
  }
}
