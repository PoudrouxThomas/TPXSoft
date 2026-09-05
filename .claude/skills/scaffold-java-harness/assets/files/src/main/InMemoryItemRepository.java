package __PACKAGE__.infrastructure.persistence;

import __PACKAGE__.domain.Item;
import __PACKAGE__.domain.ItemRepository;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import org.springframework.stereotype.Repository;

/** No database in this scaffold: swap this adapter for a real one, nothing above it changes. */
@Repository
public class InMemoryItemRepository implements ItemRepository {

  private final Map<UUID, Item> store = new ConcurrentHashMap<>();

  @Override
  public List<Item> findAll() {
    return List.copyOf(store.values());
  }

  @Override
  public Optional<Item> findById(UUID id) {
    return Optional.ofNullable(store.get(id));
  }

  @Override
  public Item save(Item item) {
    store.put(item.id(), item);
    return item;
  }

  @Override
  public boolean deleteById(UUID id) {
    return store.remove(id) != null;
  }
}
