//-----------------------------------------------------------------------
// <copyright file="LogSafeTests.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Observability;

using AppControlOrganizer.V2.Web.Services.Logging;
using Xunit;

/// <summary>
/// Tests safe structured logging helpers.
/// </summary>
public sealed class LogSafeTests
{
  /// <summary>
  /// Hashes must be stable and avoid writing the source value.
  /// </summary>
  [Fact]
  public void HashReturnsStableRedactedValue()
  {
    string first = LogSafe.Hash("person@example.com");
    string second = LogSafe.Hash("person@example.com");

    Assert.Equal(first, second);
    Assert.Equal(16, first.Length);
    Assert.DoesNotContain("person", first, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("@", first);
  }

  /// <summary>
  /// Blank values should produce a safe sentinel.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void HashReturnsNoneForBlankValues(string? value)
  {
    Assert.Equal("none", LogSafe.Hash(value));
  }

  /// <summary>
  /// Correlation ids should be compact and unique.
  /// </summary>
  [Fact]
  public void NewCorrelationIdReturnsCompactUniqueValue()
  {
    string first = LogSafe.NewCorrelationId();
    string second = LogSafe.NewCorrelationId();

    Assert.Equal(32, first.Length);
    Assert.NotEqual(first, second);
  }
}
