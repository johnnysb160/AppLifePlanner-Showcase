//-----------------------------------------------------------------------
// <copyright file="AllocationHistoryKind.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Identifies the user action that changed invested allocation capital.
/// </summary>
public enum AllocationHistoryKind
{
  /// <summary>The allocation was created.</summary>
  Created = 1,

  /// <summary>The invested position increased.</summary>
  Increased = 2,

  /// <summary>The invested position decreased.</summary>
  Reduced = 3,

  /// <summary>The allocation was removed.</summary>
  Removed = 4,
}
