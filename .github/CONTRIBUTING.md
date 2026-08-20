# Contributing to Rhombus.WinFormsMcp

Thanks for your interest in contributing! This document covers how to get started.

## Getting Started

1. Fork the repository at [github.com/fnrhombus/WinFormsMcp](https://github.com/fnrhombus/WinFormsMcp)
2. Clone your fork locally
3. Create a feature or fix branch from `main` for your work

## Build and Test

All build, test, and run commands are documented in [`CLAUDE.md`](../CLAUDE.md) at the repository root. Refer to that file for the authoritative command reference -- it stays up to date as the project evolves.

Quick summary:

```bash
dotnet build Rhombus.WinFormsMcp.sln
dotnet test Rhombus.WinFormsMcp.sln
```

## Branching Strategy

- **main** -- default integration and stable release branch. Never commit directly to main.
- **feature/** and **fix/** -- short-lived work branches. Branch from `main`, merge back to `main` through a PR.
- **release/** -- optional non-main branch for a manually dispatched beta release.

## Submitting a Pull Request

1. Ensure your branch is up to date with `main`
2. Run `dotnet build Rhombus.WinFormsMcp.sln` -- no errors
3. Run `dotnet test Rhombus.WinFormsMcp.sln` -- all tests pass
4. Push your branch and open a PR against `main`
5. Fill out the PR template

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/) to help with automated version bumping:

- `feat:` -- new features (triggers minor version bump)
- `fix:` -- bug fixes (triggers patch version bump)
- `BREAKING CHANGE:` -- breaking changes (triggers major version bump)
- `chore:` -- maintenance tasks
- `docs:` -- documentation changes
- `test:` -- test additions or changes

## Code Style

- The project uses `.editorconfig` for formatting rules
- Target framework is .NET 8.0 (Windows)
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Wrap operations in try-catch with meaningful error messages

## Reporting Issues

- **Bugs**: Use the [bug report template](https://github.com/fnrhombus/WinFormsMcp/issues/new?template=bug_report.yml)
- **Features**: Use the [feature request template](https://github.com/fnrhombus/WinFormsMcp/issues/new?template=feature_request.yml)
- **Security**: See [SECURITY.md](SECURITY.md) for vulnerability reporting

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
