//-----------------------------------------------------------------------
// <copyright file="Currency.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Specifies supported fiat and cryptocurrency codes for use in financial operations.
/// </summary>
/// <remarks>This enumeration includes both traditional fiat currencies (such as BRL, USD, EUR) and major
/// cryptocurrencies (such as BTC, ETH, LTC). Use these values to represent currency types in APIs, data models, or
/// financial calculations. The set of supported currencies may be expanded in future versions.</remarks>
public enum Currency
{
  /// <summary>
  /// Brazilian Real.
  /// </summary>
  BRL = 1,

  /// <summary>
  /// United States Dollar.
  /// </summary>
  USD = 2,

  /// <summary>
  /// Euro.
  /// </summary>
  EUR = 3,

  /// <summary>
  /// British Pound Sterling.
  /// </summary>
  GBP = 4,

  /// <summary>
  /// Japanese Yen.
  /// </summary>
  JPY = 5,

  /// <summary>
  /// Swiss Franc.
  /// </summary>
  CHF = 6,

  /// <summary>
  /// Australian Dollar.
  /// </summary>
  AUD = 7,

  /// <summary>
  /// Canadian Dollar.
  /// </summary>
  CAD = 8,

  /// <summary>
  /// New Zealand Dollar.
  /// </summary>
  NZD = 9,

  /// <summary>
  /// Argentine Peso.
  /// </summary>
  ARS = 10,

  /// <summary>
  /// Chilean Peso.
  /// </summary>
  CLP = 11,

  /// <summary>
  /// Colombian Peso.
  /// </summary>
  COP = 12,

  /// <summary>
  /// Mexican Peso.
  /// </summary>
  MXN = 13,

  /// <summary>
  /// Uruguayan Peso.
  /// </summary>
  UYU = 14,

  /// <summary>
  /// Bitcoin cryptocurrency.
  /// </summary>
  BTC = 15,

  /// <summary>
  /// Ethereum cryptocurrency.
  /// </summary>
  ETH = 16,

  /// <summary>
  /// Litecoin cryptocurrency.
  /// </summary>
  LTC = 17,

  /// <summary>
  /// Ripple cryptocurrency.
  /// </summary>
  XRP = 18,

  /// <summary>
  /// Monero cryptocurrency.
  /// </summary>
  XMR = 19,

  /// <summary>
  /// Dash cryptocurrency.
  /// </summary>
  DASH = 20,

  /// <summary>
  /// Zcash cryptocurrency.
  /// </summary>
  ZEC = 21,

  /// <summary>
  /// Bitcoin Cash cryptocurrency.
  /// </summary>
  BCH = 22,

  /// <summary>
  /// Cardano cryptocurrency.
  /// </summary>
  ADA = 23,

  /// <summary>
  /// Stellar Lumens cryptocurrency.
  /// </summary>
  XLM = 24,

  /// <summary>
  /// Neo cryptocurrency.
  /// </summary>
  NEO = 25,

  /// <summary>
  /// EOS cryptocurrency.
  /// </summary>
  EOS = 26,

  /// <summary>
  /// IOTA cryptocurrency.
  /// </summary>
  IOTA = 27,

  /// <summary>
  /// Tron cryptocurrency.
  /// </summary>
  TRX = 28,

  /// <summary>
  /// Binance Coin cryptocurrency.
  /// </summary>
  BNB = 29,

  /// <summary>
  /// Tether cryptocurrency.
  /// </summary>
  USDT = 30,

  /// <summary>
  /// VeChain cryptocurrency.
  /// </summary>
  VEN = 31,

  /// <summary>
  /// Ethereum Classic cryptocurrency.
  /// </summary>
  ETC = 32,

  /// <summary>
  /// Generic crypto currency.
  /// </summary>
  CRYPTO = 33
}
