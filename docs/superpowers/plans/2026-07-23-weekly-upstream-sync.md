# Weekly Upstream Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Check `barryw/PaperlessMCP:main` every Monday and open or update a tested pull request in the fork without changing `main` directly.

**Architecture:** A pinned GitHub Actions workflow fetches the official upstream, merges it into a dedicated automation branch, runs the .NET suite, and uses `gh` to create a PR. The existing Docker workflow validates PRs without publishing and keeps publishing GHCR only after changes reach `main`.

**Tech Stack:** GitHub Actions, GitHub CLI, Git, .NET 10, xUnit, Docker Buildx.

## Global Constraints

- Run weekly on Monday and via `workflow_dispatch`.
- Never force-push, rebase, discard fork commits, approve a PR, or push directly to `main`.
- Stop before push when merge conflicts or tests occur.
- Build PR images for `linux/amd64` without publishing them.
- Preserve current `latest` and SHA GHCR publication on `main`.
- Pin every referenced Action to an immutable 40-character SHA.

---

### Task 1: Define the upstream-sync deployment contract

**Files:**
- Modify: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`

**Interfaces:**
- Consumes: `.github/workflows/upstream-sync.yml` and `.github/workflows/docker-publish.yml`.
- Produces: static contract tests preventing direct main updates, force pushes, untested PRs, or PR image publication.

- [ ] Add tests that assert:

```csharp
[Fact]
public void UpstreamSync_IsWeeklyManualAndPullRequestOnly()
{
    var workflow = ReadRepositoryFile(".github", "workflows", "upstream-sync.yml");

    Assert.Contains("cron: \"0 5 * * 1\"", workflow);
    Assert.Contains("workflow_dispatch:", workflow);
    Assert.Contains("https://github.com/barryw/PaperlessMCP.git", workflow);
    Assert.Contains("automation/sync-upstream-", workflow);
    Assert.Contains("dotnet test PaperlessMCP.sln --configuration Release --no-restore", workflow);
    Assert.Contains("gh pr create", workflow);
    Assert.DoesNotContain("--force", workflow);
    Assert.DoesNotContain("HEAD:main", workflow);
    Assert.DoesNotContain("push origin main", workflow);
}

[Fact]
public void DockerWorkflow_ValidatesPullRequestsWithoutPublishing()
{
    var workflow = ReadRepositoryFile(".github", "workflows", "docker-publish.yml");

    Assert.Contains("pull_request:", workflow);
    Assert.Contains("if: github.event_name != 'pull_request'", workflow);
    Assert.Contains("push: ${{ github.event_name != 'pull_request' }}", workflow);
}
```

Refactor the existing repository-root lookup into:

```csharp
private static string ReadRepositoryFile(params string[] segments) =>
    File.ReadAllText(Path.Combine([RepositoryRoot, .. segments]));
```

- [ ] Run:

```bash
dotnet test PaperlessMCP.sln --no-restore \
  --filter FullyQualifiedName~GhcrPublishingTests
```

Expected: the new tests fail because `upstream-sync.yml` and PR validation do
not exist.

- [ ] Commit:

```bash
git add PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs
git commit -m "test: define weekly upstream sync contract"
```

---

### Task 2: Implement weekly PR synchronization

**Files:**
- Create: `.github/workflows/upstream-sync.yml`
- Modify: `.github/workflows/docker-publish.yml`
- Test: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`

**Interfaces:**
- Consumes: fork `main`, `barryw/PaperlessMCP:main`, and repository `GITHUB_TOKEN`.
- Produces: an open `automation/sync-upstream-*` PR or a successful no-update run.

- [ ] Create `.github/workflows/upstream-sync.yml` with:

```yaml
name: Check upstream updates

on:
  schedule:
    - cron: "0 5 * * 1"
  workflow_dispatch:

concurrency:
  group: weekly-upstream-sync
  cancel-in-progress: false

permissions:
  contents: write
  pull-requests: write

jobs:
  sync:
    runs-on: ubuntu-latest
    steps:
      - name: Check out fork
        uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803 # v6
        with:
          fetch-depth: 0
      - name: Set up .NET
        uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5
        with:
          dotnet-version: 10.0.x
      - name: Fetch upstream
        run: |
          git remote add upstream https://github.com/barryw/PaperlessMCP.git
          git fetch upstream main
      - name: Check for updates
        id: updates
        shell: bash
        run: |
          if git merge-base --is-ancestor upstream/main origin/main; then
            echo "available=false" >> "$GITHUB_OUTPUT"
            echo "Fork already contains upstream/main." >> "$GITHUB_STEP_SUMMARY"
          else
            echo "available=true" >> "$GITHUB_OUTPUT"
          fi
      - name: Select synchronization branch
        if: steps.updates.outputs.available == 'true'
        id: branch
        env:
          GH_TOKEN: ${{ github.token }}
        shell: bash
        run: |
          existing="$(gh pr list --state open --base main --json headRefName \
            --jq '[.[] | select(.headRefName | startswith("automation/sync-upstream-"))][0].headRefName // ""')"
          if [[ -n "$existing" ]]; then
            git fetch origin "$existing"
            git checkout -B "$existing" "origin/$existing"
            branch="$existing"
          else
            branch="automation/sync-upstream-$(git rev-parse --short=7 upstream/main)"
            git checkout -b "$branch" origin/main
          fi
          echo "name=$branch" >> "$GITHUB_OUTPUT"
      - name: Merge fork main and upstream
        if: steps.updates.outputs.available == 'true'
        shell: bash
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
          if ! git merge --no-edit origin/main; then
            git merge --abort || true
            echo "Merge conflict with fork main." >> "$GITHUB_STEP_SUMMARY"
            exit 1
          fi
          if ! git merge --no-edit upstream/main; then
            git merge --abort || true
            echo "Merge conflict with upstream/main; resolve manually." >> "$GITHUB_STEP_SUMMARY"
            exit 1
          fi
      - name: Restore
        if: steps.updates.outputs.available == 'true'
        run: dotnet restore PaperlessMCP.sln
      - name: Test
        if: steps.updates.outputs.available == 'true'
        run: dotnet test PaperlessMCP.sln --configuration Release --no-restore
      - name: Push synchronization branch
        if: steps.updates.outputs.available == 'true'
        run: git push origin "HEAD:${{ steps.branch.outputs.name }}"
      - name: Open pull request
        if: steps.updates.outputs.available == 'true'
        env:
          GH_TOKEN: ${{ github.token }}
          BRANCH: ${{ steps.branch.outputs.name }}
        shell: bash
        run: |
          count="$(gh pr list --state open --head "$BRANCH" --json number --jq 'length')"
          if [[ "$count" == "0" ]]; then
            gh pr create \
              --base main \
              --head "$BRANCH" \
              --title "chore: sync upstream PaperlessMCP" \
              --body "Automated weekly merge of barryw/PaperlessMCP:main. Review and merge manually; merging triggers the GHCR publisher."
          else
            echo "Updated existing PR for $BRANCH." >> "$GITHUB_STEP_SUMMARY"
          fi
```

- [ ] Add `pull_request: { branches: [main] }` to
`.github/workflows/docker-publish.yml`. Guard GHCR login with:

```yaml
if: github.event_name != 'pull_request'
```

and change Buildx publication to:

```yaml
push: ${{ github.event_name != 'pull_request' }}
```

- [ ] Run the focused tests and then the complete suite:

```bash
dotnet test PaperlessMCP.sln --no-restore \
  --filter FullyQualifiedName~GhcrPublishingTests
dotnet test PaperlessMCP.sln --no-restore
git diff --check
```

Expected: all tests pass with no whitespace errors.

- [ ] Commit:

```bash
git add .github/workflows/upstream-sync.yml \
  .github/workflows/docker-publish.yml \
  PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs
git commit -m "ci: check PaperlessMCP upstream weekly"
```

---

### Task 3: Publish and exercise the workflows

**Files:**
- No additional source files.

**Interfaces:**
- Consumes: committed workflow changes.
- Produces: a successful GHCR validation run and a manual upstream-check run.

- [ ] Push `main` to `gipasoft/PaperlessMCP`.
- [ ] Watch `Publish Docker image` to completion.
- [ ] Run `Check upstream updates` with `gh workflow run upstream-sync.yml`.
- [ ] Verify either a successful no-update result or an automation PR; verify
  that fork `main` remains at the pushed commit.
