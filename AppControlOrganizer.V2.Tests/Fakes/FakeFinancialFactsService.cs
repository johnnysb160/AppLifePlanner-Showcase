//-----------------------------------------------------------------------
// <copyright file="FakeFinancialFactsService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Services.Financial;

/// <summary>
/// Test double for canonical financial facts.
/// </summary>
internal sealed class FakeFinancialFactsService : IFinancialFactsService
{
  /// <summary>Gets the users whose facts were invalidated.</summary>
  public List<string> InvalidatedUsers { get; } = new List<string>();

  /// <summary>Gets or sets facts returned by reads.</summary>
  public FinancialFacts Facts { get; set; } = new FinancialFacts();

  /// <inheritdoc />
  public void Invalidate(string userId)
  {
    this.InvalidatedUsers.Add(userId);
  }

  /// <inheritdoc />
  public Task<FinancialFacts> GetAsync(
    string userId,
    DateOnly referenceDate,
    AnnualPlanEntity? annualPlan = null,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.Facts);
  }

  /// <inheritdoc />
  public Task<FinancialFacts> GetWithAnnualPlanAsync(
    string userId,
    DateOnly referenceDate,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.Facts);
  }

  /// <inheritdoc />
  public Task<FinancialSnapshotEntity> CaptureSnapshotAsync(
    string userId,
    DateOnly referenceDate,
    AnnualPlanEntity? annualPlan = null,
    CancellationToken cancellationToken = default)
  {
    return Task.FromResult(new FinancialSnapshotEntity
    {
      OwnerUserId = userId,
      SnapshotDate = referenceDate,
      NetWorth = this.Facts.NetWorth,
      CalculatedAtUtc = DateTime.UtcNow,
    });
  }
}
