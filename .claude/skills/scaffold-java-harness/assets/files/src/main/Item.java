package __PACKAGE__.domain;

import java.time.Instant;
import java.util.UUID;

/**
 * The domain model: plain Java, no framework, no persistence annotations. Keeping it that way is
 * what lets the architecture test state a rule instead of a preference.
 */
public record Item(UUID id, String name, String description, Instant createdAt) {

  public Item {
    if (name == null || name.isBlank()) {
      throw new IllegalArgumentException("name must not be blank");
    }
  }

  public static Item create(String name, String description) {
    return new Item(UUID.randomUUID(), name, description, Instant.now());
  }
}
