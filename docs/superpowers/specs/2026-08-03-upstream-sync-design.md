# PaperlessMCP Upstream Sync Design

## Goal

Integrate `barryw/PaperlessMCP:main` through version 0.5.0 into the customized
`gipasoft/PaperlessMCP:main` fork without losing the fork's authenticated
document-content tools or its GitHub Container Registry workflows.

## Approach

Create a synchronization branch from the fork's current `main` and merge the
current upstream `main` with an explicit merge commit. Resolve conflicts at the
feature level: retain both sides where they provide distinct behavior, rather
than accepting either complete file wholesale. The fork remains auditable and
future upstream comparisons retain the true shared history.

Rebase is not used because it would rewrite the fork's ten custom commits.
Resetting or using GitHub's discard-changes synchronization is excluded because
it would remove those customizations. Cherry-picking seventeen upstream commits
would duplicate upstream history and make later synchronization harder.

## Conflict Resolution

- `PaperlessMCP.Tests/Client/PaperlessClientTests.cs`: retain authenticated
  content/download coverage and add upstream custom-field search coverage and
  any new imports required by both sets of tests.
- `PaperlessMCP/Configuration/PaperlessOptions.cs`: retain the authenticated
  content size limit and add the upstream outbox directory option and aliases.
- `PaperlessMCP/Program.cs`: register configuration for both the authenticated
  content limit and the outbox directory.
- `README.md`: document both feature families and both environment variables,
  preserving the fork's GHCR deployment guidance.

Files that Git can merge automatically will be reviewed as part of the final
diff, with special attention to document tools, Docker Compose, API reference,
and the version bump to 0.5.0.

## Verification

The merge is acceptable only when:

1. the repository contains no unresolved conflict markers;
2. the full `dotnet test` suite passes;
3. the existing authenticated-content tests and the new upstream tests both
   remain present and pass;
4. the workflow contract tests still validate publishing and future upstream
   synchronization;
5. `git diff --check` reports no whitespace errors.

## Delivery

Push `sync-upstream-2026-08-03` to the `gipasoft/PaperlessMCP` fork and open a
pull request into `main`. Do not force-push or update `main` directly.
