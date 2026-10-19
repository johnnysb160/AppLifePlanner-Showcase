//-----------------------------------------------------------------------
// <copyright file="InstitutionType.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Specifies the type of financial institution.
/// </summary>
/// <remarks>Use this enumeration to indicate the category of an institution, such as a bank, fintech company,
/// exchange, or broker. The values can be used to control logic or display information based on the institution's
/// type.</remarks>
public enum InstitutionType
{
  /// <summary>
  /// Bank institution type.
  /// </summary>
  Bank = 1,

  /// <summary>
  /// Fintech institution type.
  /// </summary>
  Fintech = 2,

  /// <summary>
  /// Exchange institution type.
  /// </summary>
  Exchange = 3,

  /// <summary>
  /// Broker institution type.
  /// </summary>
  Broker = 4,

  /// <summary>
  /// Stopped institution type.
  /// </summary>
  Stopped = 5,

  /// <summary>
  /// Other institution type.
  /// </summary>
  Others = 6
}
