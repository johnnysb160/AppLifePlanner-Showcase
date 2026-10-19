//-----------------------------------------------------------------------
// <copyright file="AllocationHistoryEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Records user-driven changes to an allocation's invested capital.
/// </summary>
public sealed class AllocationHistoryEntity : BaseEntity
{
  /// <summary>Gets or sets the owning user identifier.</summary>
  public string OwnerUserId { get; set; } = string.Empty;

  /// <summary>Gets or sets the changed allocation identifier.</summary>
  public int AllocationId { get; set; }

  /// <summary>Gets or sets the account that owned the allocation when the change happened.</summary>
  public int AccountId { get; set; }

  /// <summary>Gets or sets the allocation change kind.</summary>
  public AllocationHistoryKind Kind { get; set; }

  /// <summary>Gets or sets the planning purpose used by contribution calculations.</summary>
  public FinancialPurpose PlanningPurpose { get; set; }

  /// <summary>Gets or sets when the allocation change happened.</summary>
  public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

  /// <summary>Gets or sets the previous invested amount in allocation currency.</summary>
  public decimal PreviousNativeAmount { get; set; }

  /// <summary>Gets or sets the new invested amount in allocation currency.</summary>
  public decimal NewNativeAmount { get; set; }

  /// <summary>Gets or sets the signed native invested amount delta.</summary>
  public decimal DeltaNativeAmount { get; set; }

  /// <summary>Gets or sets the signed invested amount delta converted to BRL.</summary>
  public decimal AmountBrl { get; set; }

  /// <summary>Gets or sets the allocation currency used for the native amount.</summary>
  public Currency Currency { get; set; }

  /// <summary>Gets or sets the optional allocation name snapshot.</summary>
  public string? AllocationName { get; set; }

  /// <summary>Gets or sets the allocation navigation property.</summary>
  public AllocationEntity? Allocation { get; set; }
}
