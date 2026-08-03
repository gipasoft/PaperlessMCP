# Stateless MCP Transport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Accept independent proxy health checks and tool requests without requiring an `Mcp-Session-Id` header.

**Architecture:** Extract HTTP transport option mutation into a small configuration class that operates on the real MCP SDK options type. Program startup delegates to it; a unit test proves stateless mode is enabled while the existing infinite idle timeout is preserved.

**Tech Stack:** .NET 10, ModelContextProtocol.AspNetCore 1.4.0, xUnit, FluentAssertions, GitHub Actions, Docker Buildx.

## Global Constraints

- Change HTTP mode only; stdio mode stays unchanged.
- Do not modify Paperless URL/token handling, tools, proxy configuration, Compose, or ports.
- Preserve `IdleTimeout = Timeout.InfiniteTimeSpan`.
- Enable stateless mode because PaperlessMCP does not use server-to-client MCP capabilities.
- Publish only after the complete Linux test suite and Docker build pass.

---

### Task 1: Specify stateless transport behavior with a failing unit test

**Files:**
- Create: `PaperlessMCP.Tests/Configuration/McpHttpTransportConfigurationTests.cs`
- Test: `PaperlessMCP.Tests/Configuration/McpHttpTransportConfigurationTests.cs`

**Interfaces:**
- Consumes: `ModelContextProtocol.AspNetCore.HttpServerTransportOptions`.
- Produces: a contract for `PaperlessMCP.Configuration.McpHttpTransportConfiguration.Configure(HttpServerTransportOptions)`.

- [ ] **Step 1: Add the failing test**

```csharp
using FluentAssertions;
using ModelContextProtocol.AspNetCore;
using PaperlessMCP.Configuration;
using Xunit;

namespace PaperlessMCP.Tests.Configuration;

public class McpHttpTransportConfigurationTests
{
    [Fact]
    public void Configure_AllowsIndependentRequestsWithoutSessionHeaders()
    {
        var options = new HttpServerTransportOptions();

        McpHttpTransportConfiguration.Configure(options);

        options.Stateless.Should().BeTrue();
        options.IdleTimeout.Should().Be(Timeout.InfiniteTimeSpan);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~McpHttpTransportConfigurationTests"
```

Expected: compilation fails because `McpHttpTransportConfiguration` does not exist.

### Task 2: Configure the real HTTP transport and publish

**Files:**
- Create: `PaperlessMCP/Configuration/McpHttpTransportConfiguration.cs`
- Modify: `PaperlessMCP/Program.cs`
- Test: `PaperlessMCP.Tests/Configuration/McpHttpTransportConfigurationTests.cs`

**Interfaces:**
- Consumes: the real `HttpServerTransportOptions` instance supplied by `.WithHttpTransport(...)`.
- Produces: `Stateless = true` and `IdleTimeout = Timeout.InfiniteTimeSpan` for HTTP mode.

- [ ] **Step 1: Add the minimal configuration function**

```csharp
using ModelContextProtocol.AspNetCore;

namespace PaperlessMCP.Configuration;

public static class McpHttpTransportConfiguration
{
    public static void Configure(HttpServerTransportOptions options)
    {
        options.Stateless = true;
        options.IdleTimeout = Timeout.InfiniteTimeSpan;
    }
}
```

- [ ] **Step 2: Delegate Program HTTP setup to the tested function**

Replace the inline `.WithHttpTransport(options => { ... })` block with:

```csharp
.WithHttpTransport(McpHttpTransportConfiguration.Configure)
```

- [ ] **Step 3: Run focused tests and verify GREEN**

Run:

```powershell
dotnet test --filter "FullyQualifiedName~McpHttpTransportConfigurationTests|FullyQualifiedName~McpAcceptHeaderCompatibilityTests"
```

Expected: all focused transport tests pass.

- [ ] **Step 4: Run complete local verification**

Run:

```powershell
dotnet test --filter "FullyQualifiedName!~ExportToOutbox_WhenDestinationIsASymlink_ReplacesTheLinkInsteadOfWritingThroughIt"
git diff --check
```

Expected: 276 tests pass locally; only the Windows-privileged symlink test is deferred to Linux CI.

- [ ] **Step 5: Commit and deliver through PR**

Run:

```powershell
git add PaperlessMCP/Configuration/McpHttpTransportConfiguration.cs PaperlessMCP/Program.cs PaperlessMCP.Tests/Configuration/McpHttpTransportConfigurationTests.cs
git commit -m "fix: allow stateless MCP proxy requests"
git push -u fork fix-stateless-mcp-transport
gh pr create --repo gipasoft/PaperlessMCP --base main --head fix-stateless-mcp-transport --title "Allow stateless MCP proxy requests" --body "Enable stateless Streamable HTTP transport so the QNAP MCP proxy can health-check and call Paperless tools without session headers."
```

Expected: PR CI passes all 277 Linux tests and the Docker build. After merge, `main` publishes a new `latest` plus immutable SHA tag before QNAP is updated.
