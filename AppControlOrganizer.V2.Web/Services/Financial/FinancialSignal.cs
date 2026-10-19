//-----------------------------------------------------------------------
// <copyright file="FinancialSignal.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Financial;

/// <summary>
/// Represents a concise, evidence-backed financial situation shown in planning experiences.
/// </summary>
public sealed class FinancialSignal
{
  /// <summary>Gets or sets the stable signal code.</summary>
  public string Code { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized title key.</summary>
  public string TitleKey { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized detail key.</summary>
  public string DetailKey { get; set; } = string.Empty;

  /// <summary>Gets or sets a value indicating whether the signal requires attention.</summary>
  public bool RequiresAttention { get; set; }
}
