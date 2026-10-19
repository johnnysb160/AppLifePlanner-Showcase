//-----------------------------------------------------------------------
// <copyright file="IFinancialFactsService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

using AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Provides one canonical interpretation of the financial data already stored by the application.
/// </summary>
public interface IFinancialFactsService
{
  /// <summary>
  /// Invalidates short-lived calculated facts after a financial write for the supplied user.
  /// </summary>
  /// <param name="userId">The owning user identifier.</param>
  void Invalidate(string userId);

  /// <summary>
  /// Calculates current financial facts without persisting a historical snapshot.
  /// </summary>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="referenceDate">The user's local reference date.</param>
  /// <param name="annualPlan">Optional annual planning assumptions.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The canonical financial facts.</returns>
  Task<FinancialFacts> GetAsync(
    string userId,
    DateOnly referenceDate,
    AnnualPlanEntity? annualPlan = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Calculates current financial facts with the stored annual plan for the reference year.
  /// </summary>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="referenceDate">The user's local reference date.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The canonical financial facts with annual plan projections resolved.</returns>
  Task<FinancialFacts> GetWithAnnualPlanAsync(
    string userId,
    DateOnly referenceDate,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Creates or updates the aggregate snapshot for the supplied local date.
  /// </summary>
  /// <param name="userId">The owning user identifier.</param>
  /// <param name="referenceDate">The user's local snapshot date.</param>
  /// <param name="annualPlan">Optional annual planning assumptions.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The persisted snapshot.</returns>
  Task<FinancialSnapshotEntity> CaptureSnapshotAsync(
    string userId,
    DateOnly referenceDate,
    AnnualPlanEntity? annualPlan = null,
    CancellationToken cancellationToken = default);
}
