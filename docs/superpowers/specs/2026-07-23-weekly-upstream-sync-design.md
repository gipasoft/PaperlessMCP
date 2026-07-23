# Weekly Upstream Sync Design

## Goal

Check `barryw/PaperlessMCP` once a week and propose compatible upstream
changes to `gipasoft/PaperlessMCP` without updating `main` automatically or
discarding the fork's authenticated document-content tools.

## Schedule and manual execution

Create `.github/workflows/upstream-sync.yml` with:

- a weekly schedule every Monday;
- `workflow_dispatch` for an on-demand check;
- one concurrency group so overlapping sync runs cannot modify the same
  branch;
- minimal `contents: write` and `pull-requests: write` permissions.

The scheduled workflow always compares the fork's `main` with
`barryw/PaperlessMCP:main`.

## Sync branch and pull request

The workflow checks out the full fork history, adds the official repository as
`upstream`, and fetches `upstream/main`.

If the fork already contains the upstream revision, the job exits successfully
without creating a commit or pull request.

If updates exist, the workflow:

1. looks for an existing open PR whose head starts with
   `automation/sync-upstream-`;
2. checks out that remote head when present, otherwise creates
   `automation/sync-upstream-<short-upstream-sha>` from the fork's current
   `main`;
3. merges the current `origin/main` and then `upstream/main` into that branch
   without rebasing or rewriting the fork's custom commits;
4. runs the complete .NET test suite;
5. pushes only the selected `automation/sync-upstream-*` branch;
6. creates a pull request to `main`, or updates the existing open sync pull
   request.

The pull request identifies the upstream revision and explains that merging it
will trigger the existing GHCR publisher.

## Failure behavior

The workflow never force-pushes or changes `main`.

If Git reports merge conflicts, it aborts the merge, writes a clear GitHub
Actions job summary, and fails. No partially merged branch is pushed.

If restore or tests fail, the branch is not pushed and the job fails with the
normal test output. This prevents an incompatible upstream update from
appearing as ready to merge.

GitHub's scheduled-run failure notification is the alert mechanism; the
workflow does not create recurring issues.

## Pull-request validation

Extend `.github/workflows/docker-publish.yml` to run on pull requests targeting
`main`.

For pull requests it must:

- run the complete .NET tests;
- build the `linux/amd64` image;
- skip GHCR login and skip image publication.

For pushes to `main` and manual publication, existing behavior remains:
successful tests lead to `latest` and immutable SHA tags being pushed to GHCR.

## Repository setting

The fork must allow GitHub Actions to create pull requests. If GitHub blocks the
first PR creation, enable the repository setting:

`Settings → Actions → General → Workflow permissions → Allow GitHub Actions to
create and approve pull requests`.

The workflow itself never approves or merges a pull request.

## Verification

Tests and static checks must prove:

- the schedule is weekly and manual execution is available;
- the official upstream URL and `main` branch are fixed;
- only branches matching `automation/sync-upstream-*` can be pushed;
- no `--force`, reset of remote `main`, or direct push to `main` is present;
- conflicts and failing tests stop before push and PR creation;
- pull requests build without publishing;
- pushes to `main` retain the existing GHCR publication behavior.
