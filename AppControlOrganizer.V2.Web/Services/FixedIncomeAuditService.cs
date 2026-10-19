//-----------------------------------------------------------------------
// <copyright file="FixedIncomeAuditService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Audits fixed-income allocations by comparing saved values with BACEN-indexed calculations.
/// </summary>
public sealed class FixedIncomeAuditService
{
  private const decimal MinimumDifferenceAmount = 2m;
  private const decimal DifferenceTolerancePercent = 0.20m;
  private const decimal SameAsAppliedToleranceAmount = 0.01m;

  private readonly IHttpClientFactory httpClientFactory;
  private readonly ILogger<FixedIncomeAuditService> logger;

  /// <summary>
  /// Initializes a new instance of the <see cref="FixedIncomeAuditService"/> class.
  /// </summary>
  /// <param name="httpClientFactory">The HTTP client factory used by the BACEN calculator.</param>
  /// <param name="logger">The logger used for diagnostics.</param>
  public FixedIncomeAuditService(
    IHttpClientFactory httpClientFactory,
    ILogger<FixedIncomeAuditService> logger)
  {
    this.httpClientFactory = httpClientFactory;
    this.logger = logger;
  }

  /// <summary>
  /// Audits all fixed-income allocations in the provided collection.
  /// </summary>
  /// <param name="allocations">The allocations to audit.</param>
  /// <param name="cancellationToken">A cancellation token.</param>
  /// <returns>The audit result for each fixed-income allocation.</returns>
  public async Task<IReadOnlyList<FixedIncomeAuditItem>> AuditAsync(
    IEnumerable<AllocationEntity> allocations,
    CancellationToken cancellationToken)
  {
    List<FixedIncomeAuditItem> items = new List<FixedIncomeAuditItem>();
    IEnumerable<AllocationEntity> fixedIncomeAllocations = allocations.Where(ShouldAudit);

    foreach (AllocationEntity allocation in fixedIncomeAllocations)
    {
      items.Add(await AuditOneAsync(allocation, cancellationToken));
    }

    return items
      .OrderByDescending(item => item.HasDivergence)
      .ThenByDescending(item => item.CanCalculate)
      .ThenBy(item => item.Allocation.Name)
      .ToList();
  }

  private async Task<FixedIncomeAuditItem> AuditOneAsync(
    AllocationEntity allocation,
    CancellationToken cancellationToken)
  {
    decimal appliedTotal = allocation.BuyUnitValue * allocation.Quantity;
    decimal savedCurrentTotal = (allocation.CurrentUnitValue ?? allocation.BuyUnitValue) * allocation.Quantity;

    FixedIncomeAuditItem item = new FixedIncomeAuditItem
    {
      Allocation = allocation,
      AppliedTotal = appliedTotal,
      SavedCurrentTotal = savedCurrentTotal
    };

    if (allocation.ProfitDate is null ||
        allocation.ProfitPercentage is null ||
        string.IsNullOrWhiteSpace(allocation.ProfitType))
    {
      item.DiagnosticKey = "Missing fixed-income yield metadata.";
      return item;
    }

    FixedIncomeCalculationResult? calculation = await FixedIncomeYieldCalculator.CalculateAsync(
      allocation,
      this.httpClientFactory,
      this.logger,
      cancellationToken);

    if (calculation == null)
    {
      item.DiagnosticKey = "Could not calculate with BACEN data.";
      return item;
    }

    decimal expectedTotal = Math.Round(calculation.CurrentUnitValue * allocation.Quantity, 2);
    decimal difference = savedCurrentTotal - expectedTotal;
    decimal absoluteDifference = Math.Abs(difference);
    decimal differencePercentage = expectedTotal == 0m ? 0m : (difference / expectedTotal) * 100m;

    item.CanCalculate = true;
    item.ExpectedCurrentTotal = expectedTotal;
    item.Difference = difference;
    item.DifferencePercentage = differencePercentage;
    item.Source = calculation.Source;
    item.LastRateDate = calculation.LastRateDate;
    item.RateCount = calculation.RateCount;
    item.HasDivergence = absoluteDifference >= MinimumDifferenceAmount
      && Math.Abs(differencePercentage) >= DifferenceTolerancePercent;
    item.DiagnosticKey = ResolveDiagnosticKey(item, allocation);

    return item;
  }

  private static bool ShouldAudit(AllocationEntity allocation)
  {
    return FixedIncomeYieldCalculator.IsSupportedYieldType(allocation) &&
      HasAnyYieldMetadata(allocation);
  }

  private static string ResolveDiagnosticKey(FixedIncomeAuditItem item, AllocationEntity allocation)
  {
    if (!item.HasDivergence)
    {
      return "Saved value is close to BACEN calculation.";
    }

    if (allocation.Quantity != 1m)
    {
      return "Review quantity and unit value. Bank quotas require per-quota buy value.";
    }

    if (IsSavedValueStillAppliedValue(item, allocation))
    {
      return "Current value was not updated yet. Run the automatic yield update or save the allocation again.";
    }

    return "Saved value diverges from BACEN. Review application date, percentage, and gross versus net value.";
  }

  private static bool IsSavedValueStillAppliedValue(FixedIncomeAuditItem item, AllocationEntity allocation)
  {
    if (allocation.CurrentUnitValue is not null)
    {
      return false;
    }

    return Math.Abs(item.SavedCurrentTotal - item.AppliedTotal) <= SameAsAppliedToleranceAmount;
  }

  private static bool HasAnyYieldMetadata(AllocationEntity allocation)
  {
    return allocation.ProfitDate is not null ||
      allocation.ProfitPercentage is not null ||
      !string.IsNullOrWhiteSpace(allocation.ProfitType);
  }
}
