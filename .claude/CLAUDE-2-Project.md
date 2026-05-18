# Project-specific Rules for ActualLab.Fusion

**YOU MUST READ [CODING_STYLE.md](CODING_STYLE.md) before writing or
modifying any C# code.** It's not optional. This project
**deviates from standard .NET conventions** on several points (notably:
no `Async` suffix on async methods; no XML docs on members; mixed brace
style). Default instincts from elsewhere will produce code that gets
rejected. If you haven't opened that file yet in this session, stop and
read it now.

**You MUST NOT write a single comment, docstring, or XML doc** without
first reading [CODING_STYLE.md → "Regular comments, docstrings, XML
documentation comments"](CODING_STYLE.md#regular-comments-docstrings-xml-documentation-comments).
You have a strong tendency to over-comment and to restate what the code
already says; that section explains exactly when a comment is justified
and when it isn't. Re-read it any time you're tempted to add a `//` or `///`.

# Type Catalog — Reuse Existing Abstractions (CRITICAL)

This codebase is mature. **Reusing what already exists is more important
than writing something new.** A new helper that duplicates an existing one
is a defect, not a feature. **Always look first.**

Use [`docs/api-index.md`](docs/api-index.md) to discover existing
abstractions before writing new code. For the complete list, see
[`docs/api-index-full.md`](docs/api-index-full.md).

## Planning rule (mandatory)

**Every implementation plan MUST include a "Reuse" section** with two parts:

1. **Existing abstractions to reuse.** Research first. List the concrete
   types/functions you intend to call from the indexes above. 
   If you cannot find a fit, say so explicitly — silence is not acceptable.

2. **Reusability of new components.** For every new component the plan
   introduces, ask: *is this likely useful elsewhere?* If yes, the plan
   **must list an option to put it in a shared project** instead of the
   feature-specific one:
   - **C#**: `ActualLab.Core`
   - **TypeScript**: `ts/actuallab-core`.

   The plan should compare the local-vs-shared placement and recommend
   one. Default to shared when in doubt — promoting later is harder than
   placing correctly the first time.

If the work is small enough that you skip a written plan, you still owe
yourself the "look first" step: search the indexes for keywords related
to what you're about to write.
