# PaperlessMCP GHCR Publishing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the custom PaperlessMCP fork as a public, pull-only GHCR image for a `linux/amd64` QNAP.

**Architecture:** A GitHub Actions workflow first runs the existing .NET test suite, then builds `PaperlessMCP/Dockerfile` with Buildx and pushes `latest` plus an immutable SHA tag to GHCR. The bundled Compose file references the remote image and retains `PAPERLESS_MCP_IMAGE` as a rollback override.

**Tech Stack:** GitHub Actions, GitHub Container Registry, Docker Buildx, Docker Compose, .NET 10, xUnit.

## Global Constraints

- Publish `ghcr.io/gipasoft/paperlessmcp` for platform `linux/amd64`.
- Push `latest` and `sha-<short-commit>` only after tests and image build succeed.
- Use only `GITHUB_TOKEN`; do not add PATs or Paperless/runtime secrets.
- Grant only `contents: read` and `packages: write`.
- Pin third-party Actions to immutable commit SHAs.
- Preserve MCP runtime behavior and all existing Compose environment variables.
- Make the GHCR package public once after its first successful publication.

---

### Task 1: Deployment contract tests

**Files:**
- Create: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`
- Test: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`

**Interfaces:**
- Consumes: repository files `.github/workflows/docker-publish.yml` and `PaperlessMCP/docker-compose.yml`.
- Produces: executable assertions for the workflow image, tags, platform, permissions, pinned Actions, and pull-only Compose contract.

- [ ] **Step 1: Write the failing deployment tests**

```csharp
using System.Text.RegularExpressions;

namespace PaperlessMCP.Tests.Deployment;

public class GhcrPublishingTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Workflow_PublishesExpectedAmd64ImageWithImmutableActions()
    {
        var workflowPath = Path.Combine(
            RepositoryRoot, ".github", "workflows", "docker-publish.yml");

        Assert.True(File.Exists(workflowPath), $"Missing workflow: {workflowPath}");

        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("branches: [main]", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("contents: read", workflow);
        Assert.Contains("packages: write", workflow);
        Assert.Contains("IMAGE_NAME: gipasoft/paperlessmcp", workflow);
        Assert.Contains("context: ./PaperlessMCP", workflow);
        Assert.Contains("file: ./PaperlessMCP/Dockerfile", workflow);
        Assert.Contains("platforms: linux/amd64", workflow);
        Assert.Contains("type=raw,value=latest", workflow);
        Assert.Contains("type=sha,format=short,prefix=sha-", workflow);

        var usesLines = workflow
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("uses:", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(usesLines);
        Assert.All(usesLines, line => Assert.Matches(
            new Regex(@"^uses:\s+[\w.-]+/[\w.-]+@[0-9a-f]{40}\s+#\s+v\d+"),
            line));
    }

    [Fact]
    public void Compose_UsesPullOnlyGhcrImageAndKeepsRollbackOverride()
    {
        var composePath = Path.Combine(
            RepositoryRoot, "PaperlessMCP", "docker-compose.yml");
        var compose = File.ReadAllText(composePath);

        Assert.Contains(
            "image: ${PAPERLESS_MCP_IMAGE:-ghcr.io/gipasoft/paperlessmcp:latest}",
            compose);
        Assert.Contains("pull_policy: always", compose);
        Assert.DoesNotContain("    build:", compose);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PaperlessMCP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find the PaperlessMCP repository root.");
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```bash
dotnet test PaperlessMCP.sln --no-restore \
  --filter FullyQualifiedName~GhcrPublishingTests
```

Expected: two failures because the workflow is absent and the Compose file
still contains `build:`.

- [ ] **Step 3: Commit the failing tests**

```bash
git add PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs
git commit -m "test: define GHCR publishing contract"
```

---

### Task 2: GHCR workflow and pull-only Compose

**Files:**
- Create: `.github/workflows/docker-publish.yml`
- Modify: `PaperlessMCP/docker-compose.yml`
- Test: `PaperlessMCP.Tests/Deployment/GhcrPublishingTests.cs`

**Interfaces:**
- Consumes: `PaperlessMCP.sln`, `PaperlessMCP/Dockerfile`, and the repository `GITHUB_TOKEN`.
- Produces: `ghcr.io/gipasoft/paperlessmcp:latest` and `ghcr.io/gipasoft/paperlessmcp:sha-<short-commit>`.

- [ ] **Step 1: Add the pinned GHCR workflow**

Create `.github/workflows/docker-publish.yml`:

```yaml
name: Publish Docker image

on:
  push:
    branches: [main]
  workflow_dispatch:

concurrency:
  group: docker-publish-${{ github.ref }}
  cancel-in-progress: true

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: gipasoft/paperlessmcp

jobs:
  test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - name: Check out repository
        uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803 # v6
      - name: Set up .NET
        uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5
        with:
          dotnet-version: 10.0.x
      - name: Restore
        run: dotnet restore PaperlessMCP.sln
      - name: Test
        run: dotnet test PaperlessMCP.sln --configuration Release --no-restore

  build-and-push:
    needs: test
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - name: Check out repository
        uses: actions/checkout@d23441a48e516b6c34aea4fa41551a30e30af803 # v6
      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@8d2750c68a42422c14e847fe6c8ac0403b4cbd6f # v3
      - name: Log in to GHCR
        uses: docker/login-action@c94ce9fb468520275223c153574b00df6fe4bcc9 # v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - name: Generate image metadata
        id: meta
        uses: docker/metadata-action@c299e40c65443455700f0fdfc63efafe5b349051 # v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=raw,value=latest
            type=sha,format=short,prefix=sha-
      - name: Build and push
        uses: docker/build-push-action@10e90e3645eae34f1e60eeb005ba3a3d33f178e8 # v6
        with:
          context: ./PaperlessMCP
          file: ./PaperlessMCP/Dockerfile
          platforms: linux/amd64
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          build-args: VERSION=0.3.2-content
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

- [ ] **Step 2: Convert Compose to the remote image**

Replace the service build block and local image in
`PaperlessMCP/docker-compose.yml`:

```yaml
services:
  paperless-mcp:
    image: ${PAPERLESS_MCP_IMAGE:-ghcr.io/gipasoft/paperlessmcp:latest}
    pull_policy: always
```

Leave ports, environment, restart policy, healthcheck, and comments unchanged.

- [ ] **Step 3: Run the deployment tests and verify GREEN**

Run:

```bash
dotnet test PaperlessMCP.sln --no-restore \
  --filter FullyQualifiedName~GhcrPublishingTests
```

Expected: both deployment tests pass.

- [ ] **Step 4: Run the full test suite**

Run:

```bash
dotnet test PaperlessMCP.sln --no-restore
```

Expected: all tests pass with zero failures.

- [ ] **Step 5: Validate Compose and build the target image**

Run:

```bash
docker compose -f PaperlessMCP/docker-compose.yml config
docker build --platform linux/amd64 \
  --build-arg VERSION=0.3.2-content \
  -t paperlessmcp-content:verification \
  -f PaperlessMCP/Dockerfile \
  PaperlessMCP
```

Expected: Compose renders successfully and the Docker build exits zero. If
Docker is unavailable locally, the GitHub Actions build is the required build
verification before completion.

- [ ] **Step 6: Commit the implementation**

```bash
git add .github/workflows/docker-publish.yml \
  PaperlessMCP/docker-compose.yml
git commit -m "ci: publish PaperlessMCP image to GHCR"
```

---

### Task 3: Publish and verify

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: successful workflow from Task 2.
- Produces: documented public GHCR deployment and QNAP pull-only update commands.

- [ ] **Step 1: Document the fork image and QNAP update**

Add a section to `README.md` containing:

````markdown
### Fork image on GHCR

This fork publishes a `linux/amd64` image after every successful push to
`main`:

```text
ghcr.io/gipasoft/paperlessmcp:latest
```

The package must be made public once after its first publication. QNAP updates
then require no local build and no registry login:

```bash
docker compose pull paperless-mcp
docker compose up -d paperless-mcp
docker compose ps
docker compose logs --tail=100 paperless-mcp
```

Set `PAPERLESS_MCP_IMAGE` to an immutable `sha-<short-commit>` tag to roll back.
````

- [ ] **Step 2: Run final local verification**

Run:

```bash
dotnet test PaperlessMCP.sln --no-restore
git diff --check
git status --short
```

Expected: all tests pass, no whitespace errors, and only the intended README
change is uncommitted.

- [ ] **Step 3: Commit documentation**

```bash
git add README.md
git commit -m "docs: explain pull-only GHCR deployment"
```

- [ ] **Step 4: Push main to the fork**

Run:

```bash
git push https://github.com/gipasoft/PaperlessMCP.git main:main
```

Expected: GitHub accepts the new commits and starts `Publish Docker image`.

- [ ] **Step 5: Verify the Action and package**

Run:

```bash
gh run list --repo gipasoft/PaperlessMCP \
  --workflow docker-publish.yml --limit 1
gh run watch --repo gipasoft/PaperlessMCP --exit-status
```

Expected: both `test` and `build-and-push` succeed.

Change the new `paperlessmcp` package visibility to Public in GitHub package
settings, then run:

```bash
docker pull ghcr.io/gipasoft/paperlessmcp:latest
```

Expected: anonymous pull succeeds without `docker login`.
