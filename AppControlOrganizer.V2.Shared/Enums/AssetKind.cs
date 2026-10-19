//-----------------------------------------------------------------------
// <copyright file="AssetKind.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Defines the kind of asset for routing to the correct quote sources.
/// </summary>
public enum AssetKind
{
  /// <summary>
  /// Foreign exchange asset.
  /// </summary>
  Fx,

  /// <summary>
  /// Cryptocurrency asset.
  /// </summary>
  Crypto,

  /// <summary>
  /// Stock asset.
  /// </summary>
  Stock,

  /// <summary>
  /// ETF asset.
  /// </summary>
  Etf
}
