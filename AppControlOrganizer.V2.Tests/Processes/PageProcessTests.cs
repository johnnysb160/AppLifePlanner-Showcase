//-----------------------------------------------------------------------
// <copyright file="PageProcessTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Processes;

using AppControlOrganizer.V2.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Tests page-level process coverage inventory.
/// </summary>
public sealed class PageProcessTests
{
  private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

  /// <summary>
  /// Mature process pages keep code-behind files where testable workflow logic lives.
  /// </summary>
  [Fact]
  public void CoreProcessPagesHaveCodeBehindCoverageTargets()
  {
    string[] expectedCodeBehindFiles =
    {
      "AppControlOrganizer.V2.Web/Components/Pages/Accounts.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Transactions.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Allocations/Allocations.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/FinancialYearReview.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Home.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/OKRs.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/QuarterPlans.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Reviews.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Habits.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Risks.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Vehicles.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Auth/Profile.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Auth/SystemStatus.razor.cs",
    };

    foreach (string relativePath in expectedCodeBehindFiles)
    {
      Assert.True(File.Exists(Path.Combine(RepositoryRoot, relativePath)), relativePath);
    }
  }

  /// <summary>
  /// Write process pages expose the demo read-only guard in markup or code-behind.
  /// </summary>
  [Fact]
  public void WriteProcessPagesExposeDemoReadOnlyGuard()
  {
    string[] expectedGuardedPages =
    {
      "AppControlOrganizer.V2.Web/Components/Pages/Accounts.razor",
      "AppControlOrganizer.V2.Web/Components/Pages/Transactions.razor",
      "AppControlOrganizer.V2.Web/Components/Pages/Allocations/Allocations.razor",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/OKRs.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/QuarterPlans.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Reviews.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Habits.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Risks.razor.cs",
      "AppControlOrganizer.V2.Web/Components/Pages/Planning/Vehicles.razor",
    };

    foreach (string relativePath in expectedGuardedPages)
    {
      string content = File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
      Assert.True(
        content.Contains("WriteGuard", StringComparison.Ordinal)
          || content.Contains("EnsureWriteAllowedAsync", StringComparison.Ordinal),
        relativePath);
    }
  }

  /// <summary>
  /// Process tests use a fixed date so page and service assertions stay deterministic.
  /// </summary>
  [Fact]
  public void ProcessTestClockIsStable()
  {
    Assert.Equal(new DateOnly(2026, 9, 15), TestClock.Today);
  }
}
