//-----------------------------------------------------------------------
// <copyright file="DemoReadOnlyTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Demo;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Tests the anonymous demonstration tenant write guard.
/// </summary>
public sealed class DemoReadOnlyTests
{
  /// <summary>
  /// Anonymous contexts cannot persist core finance rows.
  /// </summary>
  [Fact]
  public async Task AnonymousContextBlocksShadowOwnedFinanceWrites()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Demo());
    await using LifePlannerContext context = await factory.CreateDbContextAsync();
    context.Accounts.Add(new AccountEntity { Name = "Demo account" });

    await Assert.ThrowsAsync<DemoReadOnlyException>(() => context.SaveChangesAsync());
  }

  /// <summary>
  /// Controlled demo maintenance can seed the showcase tenant.
  /// </summary>
  [Fact]
  public async Task DemoWriteScopeAllowsControlledShowcaseSeed()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Demo());
    await using LifePlannerContext context = await factory.CreateDbContextAsync();
    context.Accounts.Add(new AccountEntity { Name = "Seeded demo account" });

    using (LifePlannerContext.AllowDemoWrites())
    {
      await context.SaveChangesAsync();
    }

    Assert.Equal(1, await context.Accounts.CountAsync());
  }

  /// <summary>
  /// Background contexts still reject entities explicitly owned by the demo tenant.
  /// </summary>
  [Fact]
  public async Task BackgroundContextBlocksDeclaredDemoOwnerWrites()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    await using LifePlannerContext context = await factory.CreateDbContextAsync();
    context.AnnualPlans.Add(new AnnualPlanEntity
    {
      OwnerUserId = TestHttpContext.DemoUserId,
      Year = TestClock.Today.Year,
      Theme = "Demo",
    });

    await Assert.ThrowsAsync<DemoReadOnlyException>(() => context.SaveChangesAsync());
  }
}
