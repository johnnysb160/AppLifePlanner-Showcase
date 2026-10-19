//-----------------------------------------------------------------------
// <copyright file="OwnershipTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Ownership;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Tests.Infrastructure;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Vehicles;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Tests owner isolation across EF filters and services.
/// </summary>
public sealed class OwnershipTests
{
  /// <summary>
  /// Authenticated contexts see only their own tenant-scoped rows.
  /// </summary>
  [Fact]
  public async Task AuthenticatedContextFiltersFinanceAndPlanningByOwner()
  {
    using SqliteLifePlannerFactory seedFactory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    await SeedOwnerRowsAsync(seedFactory);

    using SqliteLifePlannerFactory ownerFactory = new SqliteLifePlannerFactory(TestHttpContext.User("user-a"));
    await CopyDatabaseAsync(seedFactory, ownerFactory);
    await using LifePlannerContext context = await ownerFactory.CreateDbContextAsync();

    Assert.Single(await context.Accounts.ToListAsync());
    Assert.Single(await context.AnnualPlans.ToListAsync());
    Assert.Equal("A plan", (await context.AnnualPlans.SingleAsync()).Theme);
  }

  /// <summary>
  /// Authenticated contexts exclude soft-deleted planning rows and rows owned by other users.
  /// </summary>
  [Fact]
  public async Task AuthenticatedContextFiltersPlanningTreesByOwnerAndSoftDelete()
  {
    using SqliteLifePlannerFactory seedFactory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    await using (LifePlannerContext seed = await seedFactory.CreateDbContextAsync())
    {
      seed.Pillars.AddRange(
        new PillarEntity { OwnerUserId = "user-a", Name = "Visible pillar", Active = true },
        new PillarEntity { OwnerUserId = "user-a", Name = "Deleted pillar", Active = false, Deleted = true },
        new PillarEntity { OwnerUserId = "user-b", Name = "Other pillar", Active = true });
      await seed.SaveChangesAsync();
    }

    using SqliteLifePlannerFactory ownerFactory = new SqliteLifePlannerFactory(TestHttpContext.User("user-a"));
    await CopyDatabaseAsync(seedFactory, ownerFactory);
    await using LifePlannerContext context = await ownerFactory.CreateDbContextAsync();

    PillarEntity pillar = await context.Pillars.SingleAsync();

    Assert.Equal("Visible pillar", pillar.Name);
  }

  /// <summary>
  /// Services must verify ownership before mutating an entity id.
  /// </summary>
  [Fact]
  public async Task VehicleServiceDoesNotMutateAnotherOwnersVehicle()
  {
    using SqliteLifePlannerFactory factory = new SqliteLifePlannerFactory(TestHttpContext.Background());
    await using (LifePlannerContext context = await factory.CreateDbContextAsync())
    {
      context.Vehicles.Add(new VehicleEntity
      {
        OwnerUserId = "user-b",
        Name = "Other car",
        Active = true,
      });
      await context.SaveChangesAsync();
    }

    VehicleService service = new VehicleService(factory);
    VehicleEntity attemptedUpdate = new VehicleEntity
    {
      Id = 1,
      Name = "Changed by user A",
      VehicleType = VehicleType.Car,
      Active = true,
    };

    VehicleEntity saved = await service.SaveVehicleAsync(attemptedUpdate, "user-a");

    await using LifePlannerContext verify = await factory.CreateDbContextAsync();
    Assert.NotEqual(1, saved.Id);
    Assert.Equal("Other car", (await verify.Vehicles.SingleAsync(item => item.Id == 1)).Name);
    Assert.Equal("user-a", saved.OwnerUserId);
  }

  private static async Task SeedOwnerRowsAsync(SqliteLifePlannerFactory factory)
  {
    await using LifePlannerContext context = await factory.CreateDbContextAsync();
    AccountEntity accountA = new AccountEntity { Name = "A cash" };
    AccountEntity accountB = new AccountEntity { Name = "B cash" };
    context.Accounts.AddRange(accountA, accountB);
    context.AnnualPlans.AddRange(
      new AnnualPlanEntity { OwnerUserId = "user-a", Year = TestClock.Today.Year, Theme = "A plan" },
      new AnnualPlanEntity { OwnerUserId = "user-b", Year = TestClock.Today.Year, Theme = "B plan" });
    SqliteLifePlannerFactory.SetShadowOwner(context, accountA, "user-a");
    SqliteLifePlannerFactory.SetShadowOwner(context, accountB, "user-b");
    await context.SaveChangesAsync();
  }

  private static async Task CopyDatabaseAsync(SqliteLifePlannerFactory sourceFactory, SqliteLifePlannerFactory targetFactory)
  {
    await using LifePlannerContext source = await sourceFactory.CreateDbContextAsync();
    await using LifePlannerContext target = await targetFactory.CreateDbContextAsync();
    List<AccountProjection> accounts = await source.Accounts
      .IgnoreQueryFilters()
      .Select(account => new AccountProjection(
        account.Id,
        account.Name,
        EF.Property<string>(account, "OwnerUserId")))
      .ToListAsync();
    foreach (AccountProjection account in accounts)
    {
      AccountEntity copy = new AccountEntity { Id = account.Id, Name = account.Name };
      target.Accounts.Add(copy);
      SqliteLifePlannerFactory.SetShadowOwner(target, copy, account.OwnerUserId);
    }

    List<AnnualPlanEntity> plans = await source.AnnualPlans
      .IgnoreQueryFilters()
      .AsNoTracking()
      .ToListAsync();
    foreach (AnnualPlanEntity plan in plans)
    {
      target.AnnualPlans.Add(new AnnualPlanEntity
      {
        Id = plan.Id,
        OwnerUserId = plan.OwnerUserId,
        Year = plan.Year,
        Theme = plan.Theme,
      });
    }

    List<PillarEntity> pillars = await source.Pillars
      .IgnoreQueryFilters()
      .AsNoTracking()
      .ToListAsync();
    foreach (PillarEntity pillar in pillars)
    {
      target.Pillars.Add(new PillarEntity
      {
        Id = pillar.Id,
        OwnerUserId = pillar.OwnerUserId,
        Name = pillar.Name,
        Active = pillar.Active,
        Deleted = pillar.Deleted,
      });
    }

    await target.SaveChangesAsync();
  }

  private readonly record struct AccountProjection(int Id, string Name, string OwnerUserId);
}
