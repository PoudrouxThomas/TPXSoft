import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.List;

/**
 * Proves the PreToolUse guard actually blocks, by driving the real hook the way Claude Code
 * drives it: a JSON payload on stdin, an exit code back.
 *
 * <pre>
 *   java tools/harness/HookSelfTest.java
 * </pre>
 *
 * <p>A gate that has only been read has not been verified. This one is easy to break in ways
 * that look fine -- match only Edit and a `sed -i` walks through; forget the redirect check and
 * `echo ... &gt; file` walks through -- and a broken gate reports success while enforcing
 * nothing, which is worse than having no gate at all.
 */
public final class HookSelfTest {

  private record Case(String name, String payload, int expected) {}

  public static void main(String[] args) throws Exception {
    String gen = "src/main/generated"; // built here so this file is not self-blocking to edit
    List<Case> cases =
        List.of(
            new Case("Edit into the generated tree", edit(gen + "/Api.java"), 2),
            new Case("Write into the generated tree", write(gen + "/Api.java"), 2),
            new Case("sed -i", bash("sed -i 's/a/b/' " + gen + "/Api.java"), 2),
            new Case("heredoc redirect", bash("cat > " + gen + "/Api.java <<'EOF'\\nx\\nEOF"), 2),
            new Case("python one-liner", bash("python -c \\\"open('" + gen + "/Api.java','w')\\\""), 2),
            new Case("rm -rf", bash("rm -rf " + gen), 2),
            new Case("mv over it", bash("mv /tmp/Api.java " + gen + "/Api.java"), 2),
            new Case("cp over it", bash("cp /tmp/Api.java " + gen + "/Api.java"), 2),
            new Case("echo redirect", bash("echo x > " + gen + "/Api.java"), 2),
            new Case("tee", bash("echo x | tee " + gen + "/Api.java"), 2),
            new Case("chained after a safe command", bash("ls && sed -i s/a/b/ " + gen + "/A.java"), 2),
            new Case("reading it is fine", bash("cat " + gen + "/Api.java"), 0),
            new Case("grepping it is fine", bash("grep -r Api " + gen), 0),
            new Case("normal source edit", edit("src/main/java/App.java"), 0),
            new Case("running verify", bash("./dev verify"), 0));

    int failures = 0;
    for (Case c : cases) {
      int actual = run(c.payload());
      boolean ok = actual == c.expected();
      if (!ok) {
        failures++;
      }
      System.out.printf(
          "%-4s %-34s expected %d, got %d%n", ok ? "ok" : "FAIL", c.name(), c.expected(), actual);
    }
    System.out.println(
        failures == 0
            ? cases.size() + " cases, all pass"
            : failures + " of " + cases.size() + " FAILED -- the guard is bypassable");
    System.exit(failures == 0 ? 0 : 1);
  }

  private static int run(String payload) throws Exception {
    ProcessBuilder builder =
        new ProcessBuilder(
            List.of(
                javaBinary(),
                "tools/harness/HookGuard.java",
                "block-generated"));
    builder.redirectErrorStream(true);
    Process process = builder.start();
    try (OutputStream stdin = process.getOutputStream()) {
      stdin.write(payload.getBytes(StandardCharsets.UTF_8));
    }
    process.getInputStream().readAllBytes(); // drain, the message itself is not asserted here
    return process.waitFor();
  }

  private static String javaBinary() {
    String home = System.getProperty("java.home");
    String exe = System.getProperty("os.name", "").toLowerCase().contains("win") ? "java.exe" : "java";
    return home + "/bin/" + exe;
  }

  private static String edit(String path) {
    return "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"" + path + "\"}}";
  }

  private static String write(String path) {
    return "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"" + path + "\",\"content\":\"x\"}}";
  }

  private static String bash(String command) {
    return "{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"" + command + "\"}}";
  }

  private HookSelfTest() {}
}
