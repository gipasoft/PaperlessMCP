# Dynamic Docker Version Design

## Goal

Ensure every GHCR image built from `main` carries the application version from
the repository instead of the stale hardcoded `0.3.2-content` value.

## Design

`version.json` remains the release-version source already updated by upstream.
The Docker publishing job reads its `version` property into a step output and
passes that output to the Dockerfile as `VERSION`. The workflow fails before the
build if the file is missing, malformed, or lacks a version, so it cannot
silently publish `0.0.0-dev`.

A deployment contract test protects the data flow from `version.json` through
the workflow's version step into `docker/build-push-action`. README local-build
examples derive the same value instead of embedding a release number.

Hardcoding `0.5.0` is rejected because it would recreate the defect at the next
upstream release. Removing the build argument is rejected because the Dockerfile
default would stamp the assembly as `0.0.0-dev`.

## Verification and Delivery

The deployment test must fail against the current hardcoded workflow, pass after
the workflow change, and the complete Linux CI suite plus GHCR build must pass.
After merge, the published `latest` and immutable SHA tags must show the dynamic
version build argument in the GitHub Actions log before QNAP is updated.
