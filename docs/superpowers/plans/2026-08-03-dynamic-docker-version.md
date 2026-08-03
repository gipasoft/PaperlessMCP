# Dynamic Docker Version Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make GHCR images inherit the current application version from `version.json` instead of a stale hardcoded build argument.

**Architecture:** The Docker workflow reads and validates `version.json` with `jq`, exposes it as a step output, and passes that output to Docker Buildx. The existing deployment contract test guards this data flow, while README build examples use the same source.

**Tech Stack:** GitHub Actions, Bash, jq, Docker Buildx, .NET 10, xUnit.

## Global Constraints

- Do not hardcode `0.5.0` or any other release in the workflow.
- A missing, malformed, null, or empty `version.json` value must fail the workflow before Docker build.
- Preserve pinned GitHub Action SHAs, GHCR tags, permissions, and `linux/amd64` platform.
- Publish only after the full test suite passes.

---

### Task 1: Protect dynamic version propagation with a failing contract test

**Files:**
- Modify: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`
- Test: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`

**Interfaces:**
- Consumes: `.github/workflows/docker-publish.yml` and root `version.json`.
- Produces: `Workflow_DerivesDockerBuildVersionFromVersionJson`, which fails if the workflow embeds a release string or stops passing the validated step output to Buildx.

- [ ] **Step 1: Add the failing test**

Add this test to `GhcrPublishingTests`:

```csharp
[Fact]
public void Workflow_DerivesDockerBuildVersionFromVersionJson()
{
    var workflowPath = Path.Combine(
        RepositoryRoot, ".github", "workflows", "docker-publish.yml");
    var workflow = File.ReadAllText(workflowPath);

    Assert.Contains("name: Read application version", workflow);
    Assert.Contains("id: app-version", workflow);
    Assert.Contains("jq -er '.version | select(type == \"string\" and length > 0)' version.json", workflow);
    Assert.Contains("build-args: VERSION=${{ steps.app-version.outputs.value }}", workflow);
    Assert.DoesNotContain("build-args: VERSION=0.3.2-content", workflow);
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~Workflow_DerivesDockerBuildVersionFromVersionJson"
```

Expected: FAIL because `Read application version` is absent and the workflow still contains `VERSION=0.3.2-content`.

### Task 2: Read the release version dynamically and update documentation

**Files:**
- Modify: `.github/workflows/docker-publish.yml`
- Modify: `README.md`
- Test: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`

**Interfaces:**
- Consumes: root JSON shaped as `{ "version": "0.5.0" }`.
- Produces: workflow output `${{ steps.app-version.outputs.value }}` and a Docker `VERSION` build argument with the same non-empty string.

- [ ] **Step 1: Add the validated workflow output**

Insert after checkout in `build-and-push`:

```yaml
      - name: Read application version
        id: app-version
        shell: bash
        run: echo "value=$(jq -er '.version | select(type == \"string\" and length > 0)' version.json)" >> "$GITHUB_OUTPUT"
```

Replace the build argument with:

```yaml
          build-args: VERSION=${{ steps.app-version.outputs.value }}
```

- [ ] **Step 2: Make README build examples dynamic**

Use:

```bash
VERSION=$(jq -er '.version' version.json)
docker build \
  --build-arg VERSION="$VERSION" \
  -t "paperlessmcp-content:$VERSION" \
  -f PaperlessMCP/Dockerfile \
  PaperlessMCP
```

Use the same `$VERSION` variable in the following `docker tag` and `docker push` example.

- [ ] **Step 3: Run the focused test and verify GREEN**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~GhcrPublishingTests"
```

Expected: all deployment contract tests pass.

- [ ] **Step 4: Run complete local verification**

Run:

```powershell
dotnet test --filter "FullyQualifiedName!~ExportToOutbox_WhenDestinationIsASymlink_ReplacesTheLinkInsteadOfWritingThroughIt"
git diff --check
```

Expected: 275 tests pass locally, with the Windows-privileged symlink test deferred to Linux CI, and no whitespace errors.

- [ ] **Step 5: Commit and deliver through PR**

Run:

```powershell
git add .github/workflows/docker-publish.yml PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs README.md docs/superpowers/plans/2026-08-03-dynamic-docker-version.md
git commit -m "fix: derive Docker image version from release metadata"
git push -u fork fix-docker-version
gh pr create --repo gipasoft/PaperlessMCP --base main --head fix-docker-version --title "Fix Docker image version metadata" --body "Derive the Docker build version from version.json and protect the workflow with a deployment contract test."
```

Expected: CI passes all 276 Linux tests and the image build, after which the PR can be merged and the resulting `main` publication monitored through the manifest push.
