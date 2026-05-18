`pwsh` (cross-platform PowerShell) command is available on any OS you run, so use it.

Before starting any task, read AGENTS.md files in every directory starting from the current one and above, up to the root one (project directory).

# Execution policy after plan approval

Once a plan is approved and the open questions in it have been resolved,
**push it to completion without stopping for confirmation between steps.**
Don't ask permission to move from one pre-approved step to the next.
Don't pause to summarize "I'm about to do X" between pre-agreed phases.
Don't ask the user to choose when the choice has minimal impact.

You stop and ask only when **all** of these are true:

1. You hit a **real obstacle** you can't resolve from context alone.
2. The choice **likely obsoletes the plan or forces significant rework** —
   not "minor implementation detail," but "the path branches into two very
   different futures."
3. Your best guess at the right answer has a **non-trivial chance of being
   wrong in a way that's hard to revert**.

Concretely, do NOT ask when:
- The next step is a mechanical consequence of an earlier approved step.
- Two options exist and either is reversible in a few minutes.
- One option is clearly best (≥ ~80% probability) on the available evidence.
- You're already mid-plan and the next step is just "keep going."
- The build is broken between phases and the user already said that's fine.

When in doubt, **act**, then briefly note the choice in the result so the
user can correct course if needed. A short "I picked X because Y; flag if
you'd prefer Z" beats a question that stalls progress.

# Building

If a `*.CI.slnf` (solution filter) file exists in the project root, use it
instead of the main `*.sln` file for building. The CI solution filter
excludes projects that require additional workloads (like MAUI) that may
not be installed in your environment.

```bash
# Preferred — uses CI solution filter (excludes workload-heavy projects)
dotnet build <Project>.CI.slnf

# Only if you have all workloads installed (including maui-android, etc.)
dotnet build <Project>.sln
```

# Testing

## Debugging Test Failures

**Start with the simplest test**: If tests take too long, hang, or multiple tests fail, find the simplest failing test in the group and debug that one first. Once fixed, move on to larger/more complex tests.

**Isolate issues with small tests**: If a larger test fails and you have a reasonable guess why, write a small dedicated test that isolates the specific issue. This gives you faster iteration cycles. Keep these isolation tests in the codebase—they have value as regression tests.

## Running Single Test Cases from Theories

xUnit `[Theory]` tests with `[InlineData]` don't allow running a single test case in isolation. To debug a specific case:

1. Create a temporary `[Fact]` helper that calls the theory method with the specific arguments
2. Debug using this helper fact
3. **Remove the helper fact** after you've finished debugging—these are temporary scaffolding only

```csharp
// Temporary helper - DELETE after debugging
[Fact]
public void MyTheory_SpecificCase() => MyTheory("specificArg", 42);

[Theory]
[InlineData("case1", 1)]
[InlineData("specificArg", 42)]  // The case you're debugging
public void MyTheory(string arg1, int arg2) { /* ... */ }
```

## Timeouts

Choose reasonable timeouts based on expected execution time. If a test should complete in seconds, don't set a 5-minute timeout—use 30 seconds or less. This helps you iterate faster.

**Rule of thumb**: When working on a single test, you shouldn't wait more than 1 minute if you know it should run faster. Pick a timeout that matches your expectations.

## Logging

If you're missing information in test logs:

1. Use `Warning` level logging—it's more likely to appear in output
2. Worst case: use `Console.Error.WriteLine()` to ensure messages appear in test output

# Temporary Files

**Important:** Do not create temporary files in the project root. Use the `<projectRoot>/tmp` folder instead for any temporary files, test scripts, debug outputs, screenshots, etc. This keeps the project root clean and makes it easier to gitignore temporary artifacts.

If AC_OS environment variable is defined, you're started with Claude Launcher (c.ps1),
so your actual OS is specified in this environment variable.