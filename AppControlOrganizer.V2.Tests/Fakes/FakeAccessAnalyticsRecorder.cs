//-----------------------------------------------------------------------
// <copyright file="FakeAccessAnalyticsRecorder.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Services.Analytics;

/// <summary>
/// Test double for access analytics recording. Captures calls for assertions instead of enqueuing.
/// </summary>
internal sealed class FakeAccessAnalyticsRecorder : IAccessAnalyticsRecorder
{
  /// <summary>Gets the feature names recorded via <see cref="RecordFeatureUsage"/>.</summary>
  public List<string> RecordedFeatures { get; } = new List<string>();

  /// <inheritdoc />
  public void Record(AccessEventKind kind, string? userId, string? ip, string? userAgent, string? pathOrFeature, string? sessionId)
  {
    if (kind == AccessEventKind.FeatureUsage && pathOrFeature is not null)
    {
      RecordedFeatures.Add(pathOrFeature);
    }
  }

  /// <inheritdoc />
  public void RecordFeatureUsage(string featureName, string? userId)
  {
    RecordedFeatures.Add(featureName);
  }
}
