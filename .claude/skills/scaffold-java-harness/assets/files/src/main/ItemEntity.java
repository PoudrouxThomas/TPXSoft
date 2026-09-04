package __PACKAGE__.infrastructure.persistence;

import __PACKAGE__.domain.Item;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.UUID;

/** The persistence shape, kept apart from the domain record on purpose. */
@Entity
@Table(name = "items")
public class ItemEntity {

  @Id private UUID id;

  @Column(nullable = false)
  private String name;

  @Column(nullable = false, length = 2000)
  private String description;

  @Column(name = "created_at", nullable = false)
  private Instant createdAt;

  protected ItemEntity() {
    // required by JPA
  }

  ItemEntity(UUID id, String name, String description, Instant createdAt) {
    this.id = id;
    this.name = name;
    this.description = description;
    this.createdAt = createdAt;
  }

  static ItemEntity from(Item item) {
    return new ItemEntity(item.id(), item.name(), item.description(), item.createdAt());
  }

  Item toDomain() {
    return new Item(id, name, description, createdAt);
  }
}
