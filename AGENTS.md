# Build and test instructions for ActualLab.Fusion (Fusion) repository

## Scope

This file applies to the entire repository: all code, documentation,
and scripts under this directory tree.

IMPORTANT: Read `README.md` to learn what Fusion is.

## Technology Stack

- **Language and Platform**: by default, the project is compiled with .NET 10 (and C# 13), but all the code in `/src` folder is ready to target .NET Standard 2.0 and above, .NET Core 3.1 and above, and .NET Framework 4.7 and above. `-p:UseMultitargeting=true` enables this multi-targeting mode.
- **Databases**: some parts of Fusion use Entity Framework Core; the tests are using Sqlite, PostgreSQL, MariaDB, and Microsoft SQL Server containers to verify that.
- **Other technologies**: projects with `Blazor` in their name (e.g., )`ActualLab.Fusion.Blazor`) use Blazor.
- **Testing**: all tests are based on xUnit.
- **Documentation**: The documentation source is in the `docs/` folder and is published to https://fusion.actuallab.net/. Any external links to documentation (in README.md, etc.) should point to https://fusion.actuallab.net/ with appropriate paths (e.g., `/PartF` for `docs/PartF.md`).
  - **IMPORTANT**: Every `.md` file in `/docs` (excluding nested folders like `node_modules/`, `outdated/`, etc.) MUST be added to the VitePress sidebar TOC in `/docs/.vitepress/config.mts`. If you create a new documentation file, add it to the appropriate section in the `sidebar` array.

## Project Structure

- Main solution: `ActualLab.Fusion.sln`
- Files are organized as:
    - `src/` contains Fusion's source code
    - `samples/` folder contains sample projects
    - `docs/` folder contains the documentation - a set of `Part*.md` files. The source code snippets there are updated from corresponding `Part*.cs` files using `dotnet mdsnippets` tool. The documentation is a work-in-progress at this point.
    - `tests/` folder contain two test projects: `ActualLab.Tests` (all tests except Fusion-related ones) and `ActualLab.Fusion.Tests` (Fusion-related tests).
    - `build/` folder contains Bullseye-based `Build.csproj` - a project responsible for a set of advanced build tasks (e.g., building NuGet packages).

## Build Prerequisites

- Install .NET 10 SDK (RC1 or later)
- Run:
  ```powershell
  dotnet restore
  dotnet tool restore
  ```
- And if you're going to build the documentation:
  ```powershell
  cd Docs
  npm -i
  ```

## Building

The most important files related to build process are:
- `*.sln` and `*.csproj` files
- `Directory.Build.props` (also located in some of sub-folders) and `Directory.Build.targets` files
- `Directory.Packages.props` file listing versions of C# project dependencies.
- You can also look at `.github/workflows/Build.yml` and `.config/dotnet-tools.json`.

- To build the main solution, use:
  ```powershell
  dotnet build ActualLab.Fusion.sln
  ```

- To verify the syntax of code snippets used in the documentation:
  ```powershell
  dotnet build docs/Docs.csproj
  ```

- Do build the documentation:
  ```powershell
  cd Docs
  dotnet build Docs.csproj
  dotnet mdsnippets
  call npm run docs:build
  ```

- You can build individual projects by specifying their `.csproj` file:
  ```powershell
  dotnet build path/to/project.csproj
  ```

## Testing

Tests are located under `tests/`.

- To run all tests:
  ```powershell
  docker compose up -d redis postgres sqlserver mariadb
  dotnet test ActualLab.Fusion.sln
  ```
- To run a specific .csproj with xUnit tests:
  ```powershell
  dotnet test tests/<TestProjectName>/<TestProjectName>.csproj
  ```

## Coding Conventions

See [`CODING_STYLE.md`](CODING_STYLE.md) for complete coding style guidelines.

YOU MUST ABSOLUTELY FOLLOW THESE CONVENTIONS.

## Pull Request Messages
- When creating a PR, include a brief summary of changes with a standard "feat:", "fix:", "refactor:", "chore:", or "docs:" prefix.
- Reference related issues or discussions if applicable.

## Programmatic Checks
- After making changes, run at least `dotnet build ActualLab.Fusion.sln` to verify they at least don't break the build.
- Ensure all builds pass before submitting changes.

## Type Catalog

If `docs/Api-Index.md` exists, use it to discover existing abstractions before writing new code. It lists all useful public types across all non-test projects, organized by namespace.

## Additional Notes

AGENTS.md in other folders may extend and override instructions provided here.
