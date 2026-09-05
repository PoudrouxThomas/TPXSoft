package __PACKAGE__.infrastructure.persistence;

import __PACKAGE__.domain.Item;
import __PACKAGE__.domain.ItemRepository;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.stereotype.Repository;

/** Adapter: Spring Data on one side, the domain port on the other. */
@Repository
public class JpaItemRepository implements ItemRepository {

  private final ItemJpaRepository jpa;

  JpaItemRepository(ItemJpaRepository jpa) {
    this.jpa = jpa;
  }

  @Override
  public List<Item> findAll() {
    return jpa.findAll().stream().map(ItemEntity::toDomain).toList();
  }

  @Override
  public Optional<Item> findById(UUID id) {
    return jpa.findById(id).map(ItemEntity::toDomain);
  }

  @Override
  public Item save(Item item) {
    return jpa.save(ItemEntity.from(item)).toDomain();
  }

  @Override
  public boolean deleteById(UUID id) {
    if (!jpa.existsById(id)) {
      return false;
    }
    jpa.deleteById(id);
    return true;
  }
}
