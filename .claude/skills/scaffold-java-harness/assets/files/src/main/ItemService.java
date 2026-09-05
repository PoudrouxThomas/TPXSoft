package __PACKAGE__.application;

import __PACKAGE__.domain.Item;
import __PACKAGE__.domain.ItemNotFoundException;
import __PACKAGE__.domain.ItemRepository;
import java.util.List;
import java.util.UUID;
import org.springframework.stereotype.Service;
__TX_IMPORT__

@Service
public class ItemService {

  private final ItemRepository items;

  public ItemService(ItemRepository items) {
    this.items = items;
  }

  __TX_READ__
  public List<Item> findAll() {
    return items.findAll();
  }

  __TX_READ__
  public Item get(UUID id) {
    return items.findById(id).orElseThrow(() -> new ItemNotFoundException(id));
  }

  __TX_WRITE__
  public Item create(String name, String description) {
    return items.save(Item.create(name, description));
  }

  __TX_WRITE__
  public void delete(UUID id) {
    if (!items.deleteById(id)) {
      throw new ItemNotFoundException(id);
    }
  }
}
