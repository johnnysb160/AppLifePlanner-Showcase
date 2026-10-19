//-----------------------------------------------------------------------
// <copyright file="TestClock.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Infrastructure;

/// <summary>
/// Provides stable dates used by deterministic tests.
/// </summary>
internal static class TestClock
{
  /// <summary>Gets a stable reference date.</summary>
  public static DateOnly Today
  {
    get
    {
      return new DateOnly(2026, 9, 15);
    }
  }

  /// <summary>Gets a stable local date and time.</summary>
  public static DateTime LocalNow
  {
    get
    {
      return new DateTime(2026, 9, 15, 10, 30, 0, DateTimeKind.Unspecified);
    }
  }
}
