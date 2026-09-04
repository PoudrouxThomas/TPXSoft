package __PACKAGE__.application;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import __PACKAGE__.domain.Item;
import __PACKAGE__.domain.ItemNotFoundException;
import __PACKAGE__.domain.ItemRepository;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.Test;

/**
 * A plain unit test: no Spring context, no database, so it runs in milliseconds. Test names are the
 * behaviour specification -- shouldRejectBlankName fails out loud when it stops being true.
 */
class ItemServiceTest {

  private final ItemService service = new ItemService(new FakeRepository());

  @Test
  void shouldCreateAndReturnItem() {
    Item created = service.create("first", "and only");

    assertThat(created.name()).isEqualTo("first");
    assertThat(service.findAll()).containsExactly(created);
  }

  @Test
  void shouldRejectBlankName() {
    assertThatThrownBy(() -> service.create(" ", "")).isInstanceOf(IllegalArgumentException.class);
  }

  @Test
  void shouldReportMissingItem() {
    UUID missing = UUID.randomUUID();

    assertThatThrownBy(() -> service.get(missing)).isInstanceOf(ItemNotFoundException.class);
  }

  @Test
  void shouldDeleteExistingItem() {
    Item created = service.create("gone", "soon");

    service.delete(created.id());

    assertThat(service.findAll()).isEmpty();
  }

  /** Hand-written rather than mocked: shorter to read, and it cannot lie about behaviour. */
  private static final class FakeRepository implements ItemRepository {
    private final Map<UUID, Item> store = new LinkedHashMap<>();

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
}
