//-----------------------------------------------------------------------
// <copyright file="CardType.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Specifies the type of payment card.
/// </summary>
/// <remarks>Use this enumeration to distinguish between credit and debit cards when processing payments or
/// storing card information. The values correspond to common card categories used in financial transactions.</remarks>
public enum CardType
{
  /// <summary>
  /// Credit card type.
  /// </summary>
  Credit = 1,

  /// <summary>
  /// Debit card type.
  /// </summary>
  Debit = 2
}
