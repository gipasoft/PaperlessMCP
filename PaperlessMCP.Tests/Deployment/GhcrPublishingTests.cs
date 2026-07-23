using System.Text.RegularExpressions;
using Xunit;

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

    [Fact]
    public void UpstreamSync_IsWeeklyManualAndPullRequestOnly()
    {
        var workflowPath = Path.Combine(
            RepositoryRoot, ".github", "workflows", "upstream-sync.yml");

        Assert.True(File.Exists(workflowPath), $"Missing workflow: {workflowPath}");

        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("cron: \"0 5 * * 1\"", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("https://github.com/barryw/PaperlessMCP.git", workflow);
        Assert.Contains("automation/sync-upstream-", workflow);
        Assert.Contains("dotnet test PaperlessMCP.sln --no-restore", workflow);
        Assert.Contains("gh pr create", workflow);
        Assert.DoesNotContain("--force", workflow);
        Assert.DoesNotContain("HEAD:main", workflow);
        Assert.DoesNotContain("push origin main", workflow);
    }

    [Fact]
    public void DockerWorkflow_ValidatesPullRequestsWithoutPublishing()
    {
        var workflowPath = Path.Combine(
            RepositoryRoot, ".github", "workflows", "docker-publish.yml");
        var workflow = File.ReadAllText(workflowPath);

        Assert.Contains("pull_request:", workflow);
        Assert.Contains("if: github.event_name != 'pull_request'", workflow);
        Assert.Contains(
            "push: ${{ github.event_name != 'pull_request' }}",
            workflow);
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
