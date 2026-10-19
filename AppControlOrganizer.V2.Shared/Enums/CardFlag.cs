//-----------------------------------------------------------------------
// <copyright file="CardFlag.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Specifies the supported credit and debit card brands.
/// </summary>
/// <remarks>Use this enumeration to indicate the card brand when processing or validating payment card
/// information. The values include major international and regional card networks. The value 'Unknown' can be used when
/// the card brand cannot be determined.</remarks>
public enum CardFlag
{
  /// <summary>
  /// Visa card brand.
  /// </summary>
  Visa = 1,

  /// <summary>
  /// MasterCard card brand.
  /// </summary>
  MasterCard = 2,

  /// <summary>
  /// American Express card brand.
  /// </summary>
  AmericanExpress = 3,

  /// <summary>
  /// Elo card brand.
  /// </summary>
  Elo = 4,

  /// <summary>
  /// Hipercard card brand.
  /// </summary>
  Hipercard = 5,

  /// <summary>
  /// Hiper card brand.
  /// </summary>
  Hiper = 6,

  /// <summary>
  /// Diners Club card brand.
  /// </summary>
  DinersClub = 7,

  /// <summary>
  /// Discover card brand.
  /// </summary>
  Discover = 8,

  /// <summary>
  /// Aura card brand.
  /// </summary>
  Aura = 9,

  /// <summary>
  /// JCB card brand.
  /// </summary>
  JCB = 10,

  /// <summary>
  /// UnionPay card brand.
  /// </summary>
  UnionPay = 11,

  /// <summary>
  /// Maestro card brand.
  /// </summary>
  Maestro = 12,

  /// <summary>
  /// Dankort card brand.
  /// </summary>
  Dankort = 13,

  /// <summary>
  /// Forbrugsforeningen card brand.
  /// </summary>
  Forbrugsforeningen = 14,

  /// <summary>
  /// Laser card brand.
  /// </summary>
  Laser = 15,

  /// <summary>
  /// Solo card brand.
  /// </summary>
  Solo = 16,

  /// <summary>
  /// Switch card brand.
  /// </summary>
  Switch = 17,

  /// <summary>
  /// UATP card brand.
  /// </summary>
  UATP = 18,

  /// <summary>
  /// Verve card brand.
  /// </summary>
  Verve = 19,

  /// <summary>
  /// Troy card brand.
  /// </summary>
  Troy = 20,

  /// <summary>
  /// Unknown card brand.
  /// </summary>
  Unknown = 21
}
