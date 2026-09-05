package __PACKAGE__.api;

import __PACKAGE__.api.dto.CreateItemRequest;
import __PACKAGE__.api.dto.ItemResponse;
import __PACKAGE__.application.ItemService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import java.net.URI;
import java.util.List;
import java.util.UUID;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * The OpenAPI annotations are not decoration: this document is what a frontend generates its client
 * from, so a summary or a response code missing here is a hole in another team build.
 */
@RestController
@RequestMapping("/api/items")
@Tag(name = "Items", description = "Example resource -- replace with the real one")
public class ItemController {

  private final ItemService items;

  public ItemController(ItemService items) {
    this.items = items;
  }

  @GetMapping
  @Operation(summary = "List all items")
  public List<ItemResponse> list() {
    return items.findAll().stream().map(ItemResponse::from).toList();
  }

  @GetMapping("/{id}")
  @Operation(summary = "Get one item")
  @ApiResponse(responseCode = "200", description = "The item")
  @ApiResponse(responseCode = "404", description = "No item with that id")
  public ItemResponse get(@PathVariable UUID id) {
    return ItemResponse.from(items.get(id));
  }

  @PostMapping
  @Operation(summary = "Create an item")
  @ApiResponse(responseCode = "201", description = "Created")
  @ApiResponse(responseCode = "400", description = "Invalid payload")
  public ResponseEntity<ItemResponse> create(@Valid @RequestBody CreateItemRequest request) {
    ItemResponse created = ItemResponse.from(items.create(request.name(), request.description()));
    return ResponseEntity.created(URI.create("/api/items/" + created.id())).body(created);
  }

  @DeleteMapping("/{id}")
  @Operation(summary = "Delete an item")
  @ApiResponse(responseCode = "204", description = "Deleted")
  @ApiResponse(responseCode = "404", description = "No item with that id")
  public ResponseEntity<Void> delete(@PathVariable UUID id) {
    items.delete(id);
    return ResponseEntity.noContent().build();
  }
}
