# Design

Template for new Novolis .NET package repositories.

Published docs: [https://novolis-platform.github.io/.github/novolis-template-dotnet/](https://novolis-platform.github.io/.github/novolis-template-dotnet/)

## Layer placement

Org / template / CI infrastructure — not a closed-spine library.

## Goals

- Keep public APIs documented and packable as `Novolis.*` on GitHub Packages (when applicable).
- Prefer BCL types and existing Novolis packages over parallel abstractions.
- Document restore and ProjectReference-mode builds without local NuGet folder feeds.

## Non-goals

- Local NuGet folder feeds or committed cross-repo `ProjectReference` into sibling checkouts.
- Avalonia package references outside `Novolis.Avalonia.*`.
- Upward spine dependencies (e.g. Math → Simulation).

## Packages

- (no packable ``Novolis.*`` projects detected in ``src/`` / ``codegen/``)

## Topics

- `dotnet`
- `template`
- `novolis`
