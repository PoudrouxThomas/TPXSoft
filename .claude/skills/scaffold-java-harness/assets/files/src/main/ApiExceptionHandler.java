package __PACKAGE__.api;

import __PACKAGE__.domain.ItemNotFoundException;
import java.util.stream.Collectors;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

/**
 * One place that turns exceptions into RFC 9457 problem documents. Without it, the default error
 * page leaks stack traces into the API surface and every handler grows its own try/catch.
 */
@RestControllerAdvice
public class ApiExceptionHandler {

  @ExceptionHandler(ItemNotFoundException.class)
  ProblemDetail notFound(ItemNotFoundException exception) {
    ProblemDetail problem =
        ProblemDetail.forStatusAndDetail(HttpStatus.NOT_FOUND, exception.getMessage());
    problem.setTitle("Not found");
    return problem;
  }

  @ExceptionHandler(MethodArgumentNotValidException.class)
  ProblemDetail invalid(MethodArgumentNotValidException exception) {
    String detail =
        exception.getBindingResult().getFieldErrors().stream()
            .map(error -> error.getField() + " " + error.getDefaultMessage())
            .collect(Collectors.joining("; "));
    ProblemDetail problem =
        ProblemDetail.forStatusAndDetail(
            HttpStatus.BAD_REQUEST, detail.isBlank() ? "Invalid request" : detail);
    problem.setTitle("Invalid request");
    return problem;
  }
}
