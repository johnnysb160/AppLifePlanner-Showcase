//-----------------------------------------------------------------------
// <copyright file="StaticAssetPipelineTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Processes;

using Xunit;

/// <summary>
/// Protects the middleware order required to serve static assets before Blazor catch-all endpoints.
/// </summary>
public sealed class StaticAssetPipelineTests
{
  private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

  /// <summary>
  /// Endpoint routing must run after static files and before authentication and antiforgery middleware.
  /// </summary>
  [Fact]
  public void StaticFilesRunBeforeEndpointRouting()
  {
    string programPath = Path.Combine(RepositoryRoot, "AppControlOrganizer.V2.Web", "Program.cs");
    string program = File.ReadAllText(programPath);

    int staticFilesIndex = program.IndexOf("app.UseStaticFiles();", StringComparison.Ordinal);
    int routingIndex = program.IndexOf("app.UseRouting();", StringComparison.Ordinal);
    int authenticationIndex = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
    int antiforgeryIndex = program.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal);

    Assert.True(staticFilesIndex >= 0, "Program.cs must register static file middleware.");
    Assert.True(routingIndex > staticFilesIndex, "Endpoint routing must run after static files.");
    Assert.True(authenticationIndex > routingIndex, "Authentication must run after endpoint routing.");
    Assert.True(antiforgeryIndex > authenticationIndex, "Antiforgery must run after authentication.");
  }
}
