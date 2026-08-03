# Stateless MCP Transport Design

## Goal

Restore PaperlessMCP compatibility with the QNAP `plex-mcp` proxy, whose health
checks and forwarded tool calls are independent Streamable HTTP requests without
an `Mcp-Session-Id` header.

## Evidence

The proxy reaches PaperlessMCP but receives HTTP 400 with: “A new session can
only be created by an initialize request … or enable stateless mode.” The
Paperless URL and API token are both present in the container. The failure is
therefore at the MCP transport boundary, before any Paperless-ngx API call.

## Design

Configure `HttpServerTransportOptions.Stateless = true` for HTTP mode while
preserving the existing infinite idle timeout setting. Put the option mutation
in a small configuration function so a unit test exercises the actual SDK
options object instead of searching source text.

PaperlessMCP exposes request/response tools only. It does not use sampling,
elicitation, roots, unsolicited notifications, or other server-to-client
features disabled by stateless mode. Stdio mode remains unchanged.

Changing the proxy to retain sessions is rejected because it is broader and
would couple proxy health checks to per-server session state. Rolling back
PaperlessMCP is rejected because it would discard the 0.5.0 fixes and features.

## Verification and Delivery

The new unit test must fail before the production configuration exists and pass
after it sets `Stateless`. The complete Linux suite and Docker build must pass.
After merge and GHCR publication, QNAP will pull and recreate only
`paperless-mcp`; the proxy logs must stop reporting missing-session HTTP 400
responses, and the application query must return a document count.
