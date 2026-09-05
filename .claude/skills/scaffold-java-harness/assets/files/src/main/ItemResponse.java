package __PACKAGE__.api.dto;

import __PACKAGE__.domain.Item;
import java.time.Instant;
import java.util.UUID;

/**
 * A response type of its own, rather than the domain record. They look identical today, which is
 * exactly when the split is cheap: the alternative is a domain rename silently becoming a breaking
 * API change.
 */
public record ItemResponse(UUID id, String name, String description, Instant createdAt) {

  public static ItemResponse from(Item item) {
    return new ItemResponse(item.id(), item.name(), item.description(), item.createdAt());
  }
}
