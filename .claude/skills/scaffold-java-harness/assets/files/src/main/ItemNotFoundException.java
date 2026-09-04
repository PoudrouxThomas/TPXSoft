package __PACKAGE__.domain;

import java.util.UUID;

public class ItemNotFoundException extends RuntimeException {

  private static final long serialVersionUID = 1L;

  public ItemNotFoundException(UUID id) {
    super("No item with id " + id);
  }
}
