package __PACKAGE__.api;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.willThrow;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.csrf;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import __PACKAGE__.application.ItemService;
import __PACKAGE__.domain.Item;
import __PACKAGE__.domain.ItemNotFoundException;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.http.MediaType;
import org.springframework.security.test.context.support.WithMockUser;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

/** The HTTP edge: status codes, payload shape, validation. No database, no container. */
@WebMvcTest(ItemController.class)
@WithMockUser
class ItemControllerTest {

  @Autowired private MockMvc mvc;

  @MockitoBean private ItemService items;

  @Test
  void shouldListItems() throws Exception {
    given(items.findAll()).willReturn(List.of(Item.create("one", "")));

    mvc.perform(get("/api/items"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$[0].name").value("one"));
  }

  @Test
  void shouldReturn404ForUnknownItem() throws Exception {
    UUID id = UUID.randomUUID();
    willThrow(new ItemNotFoundException(id)).given(items).get(id);

    mvc.perform(get("/api/items/" + id)).andExpect(status().isNotFound());
  }

  @Test
  void shouldRejectBlankNameWith400() throws Exception {
    mvc.perform(
            post("/api/items")
                .with(csrf())
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"name\":\"\",\"description\":\"x\"}"))
        .andExpect(status().isBadRequest());
  }

  @Test
  void shouldCreateItem() throws Exception {
    given(items.create(any(), any())).willReturn(Item.create("made", "here"));

    mvc.perform(
            post("/api/items")
                .with(csrf())
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"name\":\"made\",\"description\":\"here\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.name").value("made"));
  }
}
