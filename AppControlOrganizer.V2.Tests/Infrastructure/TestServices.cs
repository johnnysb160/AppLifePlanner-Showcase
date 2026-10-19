//-----------------------------------------------------------------------
// <copyright file="TestServices.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Infrastructure;

using AppControlOrganizer.V2.Tests.Fakes;
using AppControlOrganizer.V2.Web.Services;
using AppControlOrganizer.V2.Web.Services.Financial;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Creates service instances used by tests.
/// </summary>
internal static class TestServices
{
  /// <summary>
  /// Creates a no-op financial sync service.
  /// </summary>
  /// <param name="factory">The test context factory.</param>
  /// <param name="factsService">The fake facts service.</param>
  /// <returns>A sync service backed by test dependencies.</returns>
  public static FinancialSyncService CreateFinancialSyncService(
    SqliteLifePlannerFactory factory,
    FakeFinancialFactsService? factsService = null)
  {
    MarketQuoteCache quoteCache = new MarketQuoteCache(
      new MemoryCache(new MemoryCacheOptions()),
      factory,
      NullLogger<MarketQuoteCache>.Instance);
    return new FinancialSyncService(
      factory,
      quoteCache,
      new AllocationQuotePricingService(),
      factsService ?? new FakeFinancialFactsService());
  }
}
