//-----------------------------------------------------------------------
// <copyright file="AllocationsNew.ViewModels.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Models;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// View models used by Allocations visual components.
/// </summary>
public sealed record CurrencySummaryVm(Currency Currency, decimal CurrentTotal);

/// <summary>
/// Summary view model for allocation types.
/// </summary>
public sealed record TypeSummaryVm(
  AllocationType Type,
  string TypeName,
  Currency Currency,
  decimal CurrentTotal,
  int Count,
  decimal Weight,
  bool HasMixedCurrencies);

/// <summary>
/// Summary view model for accounts.
/// </summary>
public sealed record AccountSummaryVm(
  int AccountId,
  string AccountName,
  string? AccountLogo,
  Currency Currency,
  decimal CurrentTotal,
  int Count,
  decimal Weight,
  bool HasMixedCurrencies);

/// <summary>
/// Grouped view model for allocation types.
/// </summary>
public sealed record GroupedTypeVm(
  AllocationType Type,
  string TypeName,
  Currency Currency,
  decimal CurrentTotal,
  int Count,
  bool HasMixedCurrencies,
  IReadOnlyList<GroupedAccountVm> ByAccount);

/// <summary>
/// Grouped view model for accounts.
/// </summary>
public sealed record GroupedAccountVm(
  int AccountId,
  string AccountName,
  string? AccountLogo,
  Currency Currency,
  decimal CurrentTotal,
  int Count,
  bool HasMixedCurrencies,
  IReadOnlyList<AllocationEntity> Items);

/// <summary>
/// View model for top positions.
/// </summary>
public sealed record TopPositionVm(
  int AllocationId,
  string Name,
  string? Initials,
  int AccountId,
  string AccountName,
  AllocationType Type,
  string TypeName,
  Currency Currency,
  decimal Current);

/// <summary>
/// View model for upcoming expirations.
/// </summary>
public sealed record UpcomingExpirationVm(
  int AllocationId,
  string Name,
  string? Initials,
  int AccountId,
  string AccountName,
  AllocationType Type,
  string TypeName,
  Currency Currency,
  DateTime? Expiration,
  decimal Current);
