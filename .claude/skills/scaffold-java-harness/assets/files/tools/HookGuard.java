import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.regex.Pattern;
import java.util.Set;

/**
 * All three Claude Code hooks for this repo, in one file, run straight from source:
 *
 * <pre>
 *   java tools/harness/HookGuard.java block-generated   (PreToolUse: Edit/Write/Bash)
 *   java tools/harness/HookGuard.java verify-on-save    (PostToolUse: Edit/Write)
 *   java tools/harness/HookGuard.java stop-verify       (Stop)
 * </pre>
 *
 * <p>Java is the one runtime a Java repo is guaranteed to have, which is why the hooks are
 * not shell or Node. JDK 11+ runs a single source file with no build step.
 *
 * <p>Exit codes matter more than they look: exit 2 is the only code the agent is shown and
 * must act on. Exit 1 is a warning nobody ever sees, so a gate that exits 1 on failure
 * silently enforces nothing while looking like it works.
 */
public final class HookGuard {

  private static final int MAX_LINES = 30;

  /** Commands that cannot modify a file, so mentioning a protected path is harmless. */
  private static final Set<String> READ_ONLY =
      Set.of(
          "cat", "bat", "head", "tail", "less", "more", "grep", "rg", "ag", "ls", "dir", "find",
          "fd", "wc", "diff", "stat", "file", "tree", "echo", "sort", "uniq", "cut", "awk", "jq",
          "git", "type", "which", "where", "gradle", "mvn", "java", "javac");

  /** Read-only in general, not with these subcommands. */
  private static final String WRITE_SUBCOMMANDS =
      "\\b(checkout|restore|apply|am|rm|mv|clean|reset)\\b";

  /** A bare "generated" path segment anywhere in a shell command. */
  private static final Pattern GENERATED_WORD =
      Pattern.compile("(^|[\\s\"'/])generated([\\s\"'/]|$)");

  public static void main(String[] args) throws Exception {
    String mode = args.length > 0 ? args[0] : "";
    String raw = readAll(System.in);
    switch (mode) {
      case "block-generated" -> blockGenerated(raw);
      case "verify-on-save" -> verifyOnSave(raw);
      case "stop-verify" -> stopVerify(raw);
      default -> {
        System.err.println("usage: java HookGuard.java <block-generated|verify-on-save|stop-verify>");
        System.exit(1);
      }
    }
  }

  // ------------------------------------------------------------------ hooks

  /**
   * Refuses writes to generated sources. Matching Edit/Write alone is not enough: {@code sed
   * -i}, a heredoc redirect, {@code cp} or a one-line python script all walk straight past an
   * Edit-only guard, so Bash is matched too and treated as deny-by-default.
   */
  private static void blockGenerated(String raw) {
    String tool = str(raw, "tool_name");
    List<String> protectedPaths = protectedPaths();

    if (List.of("Edit", "Write", "MultiEdit", "NotebookEdit").contains(tool)) {
      String target = str(raw, "file_path");
      if (target.isEmpty()) {
        target = str(raw, "notebook_path");
      }
      if (isProtected(target, protectedPaths)) {
        block(refusal(target));
      }
      System.exit(0);
    }

    if ("Bash".equals(tool)) {
      String command = str(raw, "command");
      for (String segment : command.split("&&|\\|\\||;|\\n|\\|")) {
        String trimmed = segment.trim().replace('\\', '/');
        if (trimmed.isEmpty() || !mentionsProtected(trimmed, protectedPaths)) {
          continue;
        }
        String head = head(trimmed);
        boolean readOnly =
            READ_ONLY.contains(head)
                && !trimmed.matches("(?s).*" + WRITE_SUBCOMMANDS + ".*")
                && !trimmed.contains(">");
        if (!readOnly) {
          block(refusal(trimmed.length() > 140 ? trimmed.substring(0, 140) : trimmed));
        }
      }
    }
    System.exit(0);
  }

  /**
   * Compiles after a .java edit. Narrow on purpose: the full loop runs on Stop, and this one
   * exists so a compile error comes back within seconds of the edit that caused it, while the
   * agent still holds the context to fix it cheaply.
   */
  private static void verifyOnSave(String raw) throws Exception {
    String file = str(raw, "file_path");
    if (!file.toLowerCase(Locale.ROOT).endsWith(".java")) {
      System.exit(0);
    }
    if (isProtected(file, protectedPaths())) {
      System.exit(0);
    }
    Result result = runDev("compile");
    if (result.code() == 0) {
      System.exit(0);
    }
    System.err.println(
        "Compile failed after editing "
            + file
            + ":\n"
            + result.trimmed()
            + "\nIf this is a half-finished multi-file change, keep going -- the Stop hook is the gate.");
    System.exit(2);
  }

  /** The definition of done: the agent cannot finish on a red tree. */
  private static void stopVerify(String raw) throws Exception {
    if ("true".equals(bool(raw, "stop_hook_active"))) {
      System.exit(0); // already re-entered once; do not loop forever on a red tree
    }
    Result result = runDev("verify");
    if (result.code() == 0) {
      System.exit(0);
    }
    System.err.println("`./dev verify` is red. Fix it before finishing.\n" + result.trimmed());
    System.exit(2);
  }

  // ------------------------------------------------------------- protection

  /**
   * Protected paths come from tools/harness/protected-paths.txt, one prefix per line. Any path
   * segment literally called "generated" is protected whether it is listed or not: a newly
   * generated directory should be safe by default rather than by somebody remembering.
   */
  private static List<String> protectedPaths() {
    List<String> out = new ArrayList<>();
    Path file = Path.of("tools", "harness", "protected-paths.txt");
    try {
      if (Files.exists(file)) {
        for (String line : Files.readAllLines(file, StandardCharsets.UTF_8)) {
          String entry = line.trim();
          if (!entry.isEmpty() && !entry.startsWith("#")) {
            out.add(normalize(entry).replaceAll("/+$", ""));
          }
        }
      }
    } catch (IOException ignored) {
      // an unreadable list must not silently disable the guard; the "generated" rule still holds
    }
    return out;
  }

  private static boolean isProtected(String target, List<String> list) {
    if (target == null || target.isEmpty()) {
      return false;
    }
    String rel = normalize(target);
    Path path = Path.of(rel);
    if (path.isAbsolute()) {
      try {
        rel = normalize(Path.of("").toAbsolutePath().relativize(path).toString());
      } catch (IllegalArgumentException different) {
        rel = normalize(path.toString()); // another drive or root: match on the raw path
      }
    }
    String hay = isWindows() ? rel.toLowerCase(Locale.ROOT) : rel;
    if (hay.matches("(^|.*/)generated(/.*|$)")) {
      return true;
    }
    for (String p : list) {
      String needle = isWindows() ? p.toLowerCase(Locale.ROOT) : p;
      if (hay.equals(needle) || hay.startsWith(needle + "/")) {
        return true;
      }
    }
    return false;
  }

  private static boolean mentionsProtected(String segment, List<String> list) {
    if (GENERATED_WORD.matcher(segment).find()) {
      return true;
    }
    for (String p : list) {
      if (segment.contains(p)) {
        return true;
      }
    }
    return false;
  }

  private static String refusal(String target) {
    return "Blocked: "
        + target
        + " is generated code.\n"
        + "Change the source it is generated from and re-run the generator (`./dev openapi` for"
        + " the OpenAPI document). A hand edit here is erased by the next generation and hides a"
        + " real contract drift in the meantime.";
  }

  private static void block(String message) {
    System.err.println(message);
    System.exit(2);
  }

  // ---------------------------------------------------------------- running

  private record Result(int code, String output) {
    String trimmed() {
      String[] lines = output.split("\\r?\\n");
      List<String> kept = new ArrayList<>();
      for (String line : lines) {
        if (!line.isBlank()) {
          kept.add(line);
        }
      }
      List<String> shown = kept.subList(0, Math.min(MAX_LINES, kept.size()));
      String text = String.join("\n", shown);
      if (kept.size() > shown.size()) {
        text += "\n... " + (kept.size() - shown.size()) + " more lines suppressed";
      }
      return text;
    }
  }

  /**
   * Runs `./dev <command>`.
   *
   * <p>Finding bash on Windows is the fiddly part: hooks are launched from cmd, which cannot
   * follow the POSIX PATH a Git Bash session exports, so "bash" alone resolves only sometimes.
   * The candidates below cover a normal Git for Windows install. If none of them start, the hook
   * fails loudly rather than passing -- a gate that quietly stops gating is the failure this
   * whole file exists to prevent.
   */
  private static Result runDev(String command) throws Exception {
    IOException last = null;
    for (String bash : bashCandidates()) {
      try {
        ProcessBuilder builder =
            new ProcessBuilder(List.of(bash, "./dev", command)).redirectErrorStream(true);
        builder.environment().put("NO_COLOR", "1");
        Process process = builder.start();
        String output = readAll(process.getInputStream());
        int code = process.waitFor();
        // On Windows, a bare "bash" often resolves to the WSL launcher, which cannot see this
        // filesystem and fails in a way that looks exactly like a red build. Try the next one.
        if (code != 0 && (output.contains("execvpe") || output.contains("WSL ("))) {
          continue;
        }
        return new Result(code, output);
      } catch (IOException notHere) {
        last = notHere;
      }
    }
    return new Result(
        2,
        "harness: could not run ./dev -- no usable bash found ("
            + (last == null ? "every candidate failed to start the script" : last.getMessage())
            + ").\nInstall Git for Windows, or point bashCandidates() at your shell in"
            + " tools/harness/HookGuard.java.");
  }

  private static List<String> bashCandidates() {
    if (!isWindows()) {
      return List.of("bash");
    }
    // Git Bash first: a bare "bash" on Windows PATH is frequently the WSL launcher.
    List<String> candidates = new ArrayList<>();
    String programFiles = System.getenv("ProgramFiles");
    if (programFiles != null) {
      candidates.add(programFiles + "\\Git\\bin\\bash.exe");
      candidates.add(programFiles + "\\Git\\usr\\bin\\bash.exe");
    }
    candidates.add("C:\\Program Files\\Git\\bin\\bash.exe");
    candidates.add("C:\\Program Files (x86)\\Git\\bin\\bash.exe");
    candidates.add("bash");
    return candidates;
  }

  private static boolean isWindows() {
    return System.getProperty("os.name", "").toLowerCase(Locale.ROOT).contains("win");
  }

  // ------------------------------------------------------------------- json

  /**
   * Pulls one string value out of the hook payload. A full JSON parser is not worth a
   * dependency here: the keys we need (tool_name, file_path, command) are unambiguous, and the
   * only thing that genuinely needs care is unescaping, because a blocked command is often
   * quoted.
   */
  private static String str(String json, String key) {
    int at = json.indexOf('"' + key + '"');
    if (at < 0) {
      return "";
    }
    int i = json.indexOf(':', at);
    if (i < 0) {
      return "";
    }
    i++;
    while (i < json.length() && Character.isWhitespace(json.charAt(i))) {
      i++;
    }
    if (i >= json.length() || json.charAt(i) != '"') {
      return "";
    }
    StringBuilder out = new StringBuilder();
    for (i++; i < json.length(); i++) {
      char c = json.charAt(i);
      if (c == '\\' && i + 1 < json.length()) {
        char next = json.charAt(++i);
        switch (next) {
          case 'n' -> out.append('\n');
          case 't' -> out.append('\t');
          case 'r' -> out.append('\r');
          case 'b' -> out.append('\b');
          case 'f' -> out.append('\f');
          case 'u' -> {
            out.append((char) Integer.parseInt(json.substring(i + 1, i + 5), 16));
            i += 4;
          }
          default -> out.append(next);
        }
      } else if (c == '"') {
        break;
      } else {
        out.append(c);
      }
    }
    return out.toString();
  }

  private static String bool(String json, String key) {
    int at = json.indexOf('"' + key + '"');
    if (at < 0) {
      return "false";
    }
    int i = json.indexOf(':', at);
    if (i < 0) {
      return "false";
    }
    String rest = json.substring(i + 1).trim();
    return rest.startsWith("true") ? "true" : "false";
  }

  private static String head(String segment) {
    String first = segment.split("\\s+")[0];
    if (first.startsWith("(")) {
      first = first.substring(1);
    }
    String[] parts = first.split("/");
    return parts[parts.length - 1];
  }

  private static String normalize(String p) {
    String s = p.replace('\\', '/');
    return s.startsWith("./") ? s.substring(2) : s;
  }

  private static String readAll(InputStream in) throws IOException {
    ByteArrayOutputStream buffer = new ByteArrayOutputStream();
    byte[] chunk = new byte[8192];
    int read;
    while ((read = in.read(chunk)) > 0) {
      buffer.write(chunk, 0, read);
    }
    return buffer.toString(StandardCharsets.UTF_8);
  }

  private HookGuard() {}
}
