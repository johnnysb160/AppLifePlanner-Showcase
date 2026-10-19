//-----------------------------------------------------------------------
// <copyright file="SqliteLifePlannerFactory.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Infrastructure;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides isolated SQLite-backed application contexts for unit tests.
/// </summary>
internal sealed class SqliteLifePlannerFactory : IDbContextFactory<LifePlannerContext>, IDisposable
{
  private readonly IHttpContextAccessor httpContextAccessor;
  private readonly SqliteConnection connection;
  private readonly DbContextOptions<LifePlannerContext> options;

  /// <summary>
  /// Initializes a new instance of the <see cref="SqliteLifePlannerFactory"/> class.
  /// </summary>
  /// <param name="httpContextAccessor">The tenant context accessor.</param>
  public SqliteLifePlannerFactory(IHttpContextAccessor httpContextAccessor)
  {
    this.httpContextAccessor = httpContextAccessor;
    this.connection = new SqliteConnection("Data Source=:memory:");
    this.connection.Open();
    this.options = new DbContextOptionsBuilder<LifePlannerContext>()
      .UseSqlite(this.connection)
      .EnableSensitiveDataLogging()
      .Options;

    using LifePlannerContext context = this.CreateDbContext();
    context.Database.EnsureCreated();
  }

  /// <inheritdoc />
  public LifePlannerContext CreateDbContext()
  {
    return new LifePlannerContext(this.options, this.httpContextAccessor);
  }

  /// <inheritdoc />
  public Task<LifePlannerContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult(this.CreateDbContext());
  }

  /// <summary>
  /// Assigns a shadow owner to a core finance entity.
  /// </summary>
  /// <param name="context">The context tracking the entity.</param>
  /// <param name="entity">The entity receiving the owner.</param>
  /// <param name="ownerUserId">The owner identifier.</param>
  public static void SetShadowOwner(LifePlannerContext context, BaseEntity entity, string ownerUserId)
  {
    context.Entry(entity).Property("OwnerUserId").CurrentValue = ownerUserId;
  }

  /// <inheritdoc />
  public void Dispose()
  {
    this.connection.Dispose();
  }
}
