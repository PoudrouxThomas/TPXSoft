package __PACKAGE__.domain;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

/** Port. The implementation lives in infrastructure and nothing above it knows which one. */
public interface ItemRepository {

  List<Item> findAll();

  Optional<Item> findById(UUID id);

  Item save(Item item);

  boolean deleteById(UUID id);
}
