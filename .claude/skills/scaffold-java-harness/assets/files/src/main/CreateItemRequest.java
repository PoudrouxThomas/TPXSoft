package __PACKAGE__.api.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record CreateItemRequest(
    @NotBlank @Size(max = 200) String name, @Size(max = 2000) String description) {

  public CreateItemRequest {
    description = description == null ? "" : description;
  }
}
