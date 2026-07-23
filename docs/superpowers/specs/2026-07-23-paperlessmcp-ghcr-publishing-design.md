# PaperlessMCP GHCR Publishing Design

## Goal

Publish the custom PaperlessMCP fork as a public GHCR image so the QNAP
deployment can be updated with only:

```bash
docker compose pull paperless-mcp
docker compose up -d paperless-mcp
```

The QNAP target platform is `linux/amd64`.

## Scope

Add one GitHub Actions workflow to the `gipasoft/PaperlessMCP` fork and change
the bundled Docker Compose service from a local build to the published image.
Do not change the MCP tools, Paperless authentication, runtime configuration,
or the existing document-content behavior.

## Image and tags

The workflow publishes to:

```text
ghcr.io/gipasoft/paperlessmcp
```

Every successful push to `main` publishes:

- `latest`, for the QNAP Compose deployment;
- `sha-<short-commit>`, for rollback and traceability.

The workflow also supports `workflow_dispatch` and publishes the same tags for
the selected `main` revision. It builds only `linux/amd64`.

## Workflow

Create `.github/workflows/docker-publish.yml` with:

- `push` on `main`;
- `workflow_dispatch`;
- one concurrency group that cancels an obsolete run for the same ref;
- `contents: read` and `packages: write` permissions;
- repository checkout;
- .NET 10 setup and `dotnet test --no-restore` after restore;
- Docker Buildx setup;
- login to `ghcr.io` with `github.actor` and `GITHUB_TOKEN`;
- Docker metadata generation for `latest` and the immutable SHA tag;
- build and push with context `./PaperlessMCP`, Dockerfile
  `./PaperlessMCP/Dockerfile`, platform `linux/amd64`, and GitHub Actions cache.

The image is pushed only when tests and the container build succeed. No
Paperless API token, proxy token, PAT, or other deployment secret is required
by the workflow.

Third-party actions are pinned to immutable commit SHAs rather than mutable
major-version tags.

## Docker Compose

In `PaperlessMCP/docker-compose.yml`:

- remove the `build:` section;
- set `image` to
  `${PAPERLESS_MCP_IMAGE:-ghcr.io/gipasoft/paperlessmcp:latest}`;
- set `pull_policy: always`;
- preserve all environment variables, ports, restart policy, and healthcheck.

`PAPERLESS_MCP_IMAGE` remains available as an override for rollback to an
immutable SHA tag.

## Public package and QNAP operation

The first workflow run creates the GHCR package as private by default. After
that first successful publication, the package owner must change its
visibility to **Public** once in GitHub package settings. Public GHCR container
images can be pulled anonymously, so the QNAP does not require `docker login`.

Normal QNAP updates then use:

```bash
docker compose pull paperless-mcp
docker compose up -d paperless-mcp
docker compose ps
docker compose logs --tail=100 paperless-mcp
```

For rollback, set for example:

```env
PAPERLESS_MCP_IMAGE=ghcr.io/gipasoft/paperlessmcp:sha-abcdef0
```

and repeat `pull` plus `up -d`.

## Verification

Before publishing the implementation:

1. parse the workflow and Compose files as YAML;
2. verify the workflow has the required triggers and minimum permissions;
3. verify its Docker context, Dockerfile path, platform, image name, and tags;
4. run the complete .NET test suite;
5. build the Docker image locally for `linux/amd64` when Docker is available;
6. inspect the Git diff and run `git diff --check`.

After pushing, verify the GitHub Actions run succeeds, the two expected GHCR
tags exist, and an anonymous pull works after the package is made public.
