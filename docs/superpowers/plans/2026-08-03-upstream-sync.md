# PaperlessMCP Upstream 0.5.0 Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge `barryw/PaperlessMCP:main` through version 0.5.0 into the customized `gipasoft/PaperlessMCP` fork while preserving both upstream and fork-only behavior.

**Architecture:** Use a true Git merge from `origin/main` into a branch created from `fork/main`. Resolve the four overlapping files additively, rely on both projects' existing behavioral tests as the regression contract, then deliver through a pull request rather than updating `main` directly.

**Tech Stack:** Git, .NET 10, xUnit, FluentAssertions, GitHub Actions, GitHub CLI.

## Global Constraints

- Do not rebase, reset, force-push, or discard the fork's ten custom commits.
- Preserve authenticated document-content tools and GHCR publishing workflows.
- Integrate upstream outbox export, custom-field search, document-link parsing, and title validation through version 0.5.0.
- Do not update `gipasoft/main` directly; deliver from `sync-upstream-2026-08-03` through a pull request.
- The full test suite and `git diff --check` must pass before push.

---

### Task 1: Merge upstream and resolve overlapping behavior

**Files:**
- Modify: `PaperlessMCP.Tests/Client/PaperlessClientTests.cs`
- Modify: `PaperlessMCP/Configuration/PaperlessOptions.cs`
- Modify: `PaperlessMCP/Program.cs`
- Modify: `README.md`
- Review auto-merged: `PaperlessMCP/Client/PaperlessClient.cs`
- Review auto-merged: `PaperlessMCP/Tools/DocumentTools.cs`
- Review auto-merged: `PaperlessMCP/docker-compose.yml`
- Review auto-merged: `docs/API_REFERENCE.md`
- Test: `PaperlessMCP.Tests/Client/PaperlessClientTests.cs`
- Test: `PaperlessMCP.Tests/Tools/DocumentToolsTests.cs`
- Test: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`

**Interfaces:**
- Consumes: fork commit `cac5160`, design commit `5a85489`, and upstream `origin/main` at `a3c3f14`.
- Produces: a conflict-free merge containing both `MaxDownloadSizeBytes` and `OutboxDirectory`, both environment-variable bindings, and both authenticated-content and upstream test coverage.

- [ ] **Step 1: Reproduce the CI failure on the synchronization branch**

Run:

```powershell
git merge --no-ff --no-commit origin/main
git diff --name-only --diff-filter=U
```

Expected: merge stops with conflicts only in `PaperlessMCP.Tests/Client/PaperlessClientTests.cs`, `PaperlessMCP/Configuration/PaperlessOptions.cs`, `PaperlessMCP/Program.cs`, and `README.md`.

- [ ] **Step 2: Resolve the client test import conflict additively**

Keep both imports at the top of `PaperlessClientTests.cs`:

```csharp
using System.Net.Http.Headers;
using System.Web;
```

Keep both the fork's authenticated binary-content tests and upstream's `custom_field_query` tests.

- [ ] **Step 3: Resolve configuration conflicts additively**

In `PaperlessOptions.cs`, retain the fork's decoded download-size option and add the upstream outbox option. Preserve the exact public properties supplied by the two sides:

```csharp
public long MaxDownloadSizeBytes { get; set; }
public string OutboxDirectory { get; set; } = DefaultOutboxDirectory;
```

In `Program.cs`, keep the existing `MAX_DOWNLOAD_SIZE_BYTES` binding and add upstream's `PAPERLESS_OUTBOX_DIR` / `OUTBOX_DIR` binding exactly as upstream defines it.

- [ ] **Step 4: Resolve README documentation additively**

Keep both environment table rows:

```markdown
| `MAX_DOWNLOAD_SIZE_BYTES` | | `10485760` | Maximum decoded size returned by the base64 document content tools |
| `PAPERLESS_OUTBOX_DIR` | | `/home/mcp/outbox` | Directory `paperless_documents_export_to_outbox` writes into. Mount it as a shared volume or the exports are unreachable outside the container |
```

Keep the upstream outbox sharing section and the fork's binary document-content section. Preserve the alias sentence with `PAPERLESS_URL`, `PAPERLESS_TOKEN`, and `OUTBOX_DIR`.

- [ ] **Step 5: Verify the merge is structurally resolved**

Run:

```powershell
git diff --name-only --diff-filter=U
rg -n "^(<<<<<<<|=======|>>>>>>>)" -g "!*bin*" -g "!*obj*"
git diff --check
```

Expected: no unresolved paths, no conflict markers, and no whitespace errors.

- [ ] **Step 6: Run focused behavioral tests**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~PaperlessClientTests|FullyQualifiedName~DocumentToolsTests|FullyQualifiedName~GhcrPublishingTests"
```

Expected: all selected tests pass, proving both feature families and both workflow contracts remain intact.

- [ ] **Step 7: Complete the merge commit**

Run:

```powershell
git add PaperlessMCP.Tests/Client/PaperlessClientTests.cs PaperlessMCP/Configuration/PaperlessOptions.cs PaperlessMCP/Program.cs README.md
git add -u
git commit -m "merge: integrate upstream PaperlessMCP 0.5.0"
```

Expected: a two-parent merge commit with no unresolved files.

### Task 2: Verify and deliver the synchronization branch

**Files:**
- Verify: `PaperlessMCP.sln`
- Verify: `.github/workflows/docker-publish.yml`
- Verify: `.github/workflows/upstream-sync.yml`
- Deliver: branch `sync-upstream-2026-08-03`

**Interfaces:**
- Consumes: the merge commit from Task 1.
- Produces: a pushed branch and pull request into `gipasoft/PaperlessMCP:main`.

- [ ] **Step 1: Run the complete regression suite**

Run:

```powershell
dotnet test
```

Expected: every test passes with zero failures.

- [ ] **Step 2: Verify Git history and repository cleanliness**

Run:

```powershell
git status --short --branch
git log -1 --pretty=raw
git diff --check fork/main...HEAD
```

Expected: clean working tree, the latest commit has two parents, and the full branch diff has no whitespace errors.

- [ ] **Step 3: Push the synchronization branch**

Run:

```powershell
git push -u fork sync-upstream-2026-08-03
```

Expected: GitHub accepts the new branch without force-push.

- [ ] **Step 4: Open the pull request**

Run:

```powershell
gh pr create --repo gipasoft/PaperlessMCP --base main --head sync-upstream-2026-08-03 --title "Merge upstream PaperlessMCP 0.5.0" --body-file docs/superpowers/plans/2026-08-03-upstream-sync.md
```

Expected: GitHub returns the URL of a new pull request targeting `gipasoft/main`.
