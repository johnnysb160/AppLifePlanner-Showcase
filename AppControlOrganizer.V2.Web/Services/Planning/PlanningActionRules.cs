//-----------------------------------------------------------------------
// <copyright file="PlanningActionRules.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services.Financial;

/// <summary>
/// Builds deterministic, no-paid-AI planning actions from existing application facts.
/// </summary>
public static class PlanningActionRules
{
  private const int ReviewDueDays = 7;

  /// <summary>
  /// Builds the current action candidates for a user.
  /// </summary>
  /// <param name="input">The rule input facts.</param>
  /// <returns>The ordered candidate actions.</returns>
  public static IReadOnlyList<PlanningActionCandidate> BuildCandidates(PlanningActionRuleInput input)
  {
    List<PlanningActionCandidate> candidates = new List<PlanningActionCandidate>();
    AddFinancialSignalActions(candidates, input);
    AddPlanningMaintenanceActions(candidates, input);
    AddDataQualityActions(candidates, input);

    return candidates
      .OrderBy(action => action.Priority)
      .ThenBy(action => action.SourceCode, StringComparer.Ordinal)
      .ToList();
  }

  private static void AddFinancialSignalActions(
    List<PlanningActionCandidate> candidates,
    PlanningActionRuleInput input)
  {
    foreach (FinancialSignal signal in input.Facts.Signals.Where(item => item.RequiresAttention))
    {
      switch (signal.Code)
      {
        case "UNPRICED_POSITIONS":
          candidates.Add(new PlanningActionCandidate
          {
            SourceCode = "FIN_UNPRICED_POSITIONS",
            ActionType = PlanningActionType.Correct,
            Priority = 10,
            IconCss = "bi bi-tags",
            TitleKey = "Correct positions without pricing",
            DetailKey = "Some assets cannot be converted into today's BRL planning value.",
            EvidenceText = input.Facts.UnpricedPositionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DestinationHref = "allocations?panel=quotes",
            PrimaryActionKey = "Open allocations",
            DueDate = input.ReferenceDate,
          });
          break;
        case "RESERVE_GAP":
          candidates.Add(new PlanningActionCandidate
          {
            SourceCode = "FIN_RESERVE_GAP",
            ActionType = PlanningActionType.Apply,
            Priority = 20,
            IconCss = "bi bi-shield-check",
            TitleKey = "Reinforce the emergency reserve",
            DetailKey = "The reserve is below the number of months defined in the annual plan.",
            EvidenceText = $"{Helper.FormatBrl(input.Facts.EmergencyReserve)} / {Helper.FormatBrl(input.Facts.EmergencyReserveTarget)}",
            DestinationHref = "plan#finance",
            PrimaryActionKey = "Review plan",
            DueDate = input.ReferenceDate.AddDays(7),
          });
          break;
        case "IDLE_CASH":
          candidates.Add(new PlanningActionCandidate
          {
            SourceCode = "FIN_IDLE_CASH",
            ActionType = PlanningActionType.Decide,
            Priority = 30,
            IconCss = "bi bi-cash-coin",
            TitleKey = "Decide what to do with idle cash",
            DetailKey = "Cash without a defined purpose is above the planning threshold.",
            EvidenceText = $"{input.Facts.IdleCashPercentage:N1}% · {Helper.FormatBrl(input.Facts.IdleCash)}",
            DestinationHref = "allocations",
            PrimaryActionKey = "Open allocations",
            DueDate = input.ReferenceDate.AddDays(2),
          });
          break;
        case "TARGET_GAP":
          candidates.Add(new PlanningActionCandidate
          {
            SourceCode = "FIN_TARGET_GAP",
            ActionType = PlanningActionType.Review,
            Priority = 40,
            IconCss = "bi bi-bullseye",
            TitleKey = "Adjust the pace toward the annual target",
            DetailKey = "The current projection does not reach the annual net worth target.",
            EvidenceText = $"{Helper.FormatBrl(input.Facts.PlannedYearEndNetWorth)} / {Helper.FormatBrl(input.AnnualPlan?.TargetNetWorth ?? 0m)}",
            DestinationHref = "plan#finance",
            PrimaryActionKey = "Review plan",
            DueDate = input.ReferenceDate.AddDays(7),
          });
          break;
      }
    }
  }

  private static void AddPlanningMaintenanceActions(
    List<PlanningActionCandidate> candidates,
    PlanningActionRuleInput input)
  {
    if (!input.HasWeeklyFocuses)
    {
      candidates.Add(new PlanningActionCandidate
      {
        SourceCode = "PLAN_WEEKLY_FOCUS_MISSING",
        ActionType = PlanningActionType.Decide,
        Priority = 50,
        IconCss = "bi bi-compass",
        TitleKey = "Choose this week's operating focus",
        DetailKey = "Today works better when the week has up to three explicit priorities.",
        EvidenceText = "0 / 3",
        DestinationHref = "plan#week",
        PrimaryActionKey = "Choose focus",
        DueDate = input.ReferenceDate,
      });
    }

    if (!input.LastReviewDate.HasValue
      || input.ReferenceDate.DayNumber - input.LastReviewDate.Value.DayNumber >= ReviewDueDays)
    {
      candidates.Add(new PlanningActionCandidate
      {
        SourceCode = "PLAN_REVIEW_DUE",
        ActionType = PlanningActionType.Review,
        Priority = 60,
        IconCss = "bi bi-check2-circle",
        TitleKey = "Close a light weekly review",
        DetailKey = "Record progress, blockers and one decision without using the advanced review flow.",
        EvidenceText = input.LastReviewDate.HasValue
          ? (input.ReferenceDate.DayNumber - input.LastReviewDate.Value.DayNumber).ToString(System.Globalization.CultureInfo.InvariantCulture)
          : string.Empty,
        DestinationHref = "plan",
        PrimaryActionKey = "Open review",
        DueDate = input.ReferenceDate,
      });
    }
  }

  private static void AddDataQualityActions(
    List<PlanningActionCandidate> candidates,
    PlanningActionRuleInput input)
  {
    if (input.UnclassifiedAllocationCount > 0)
    {
      candidates.Add(new PlanningActionCandidate
      {
        SourceCode = "DATA_ALLOCATION_PURPOSE",
        ActionType = PlanningActionType.Correct,
        Priority = 70,
        IconCss = "bi bi-diagram-3",
        TitleKey = "Classify allocation purposes",
        DetailKey = "Some allocations still rely on automatic purpose inference.",
        EvidenceText = input.UnclassifiedAllocationCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        DestinationHref = "allocations",
        PrimaryActionKey = "Open allocations",
        DueDate = input.ReferenceDate.AddDays(14),
      });
    }

    if (input.UnclassifiedTransactionCount > 0)
    {
      candidates.Add(new PlanningActionCandidate
      {
        SourceCode = "DATA_TRANSACTION_IMPACT",
        ActionType = PlanningActionType.Correct,
        Priority = 80,
        IconCss = "bi bi-receipt",
        TitleKey = "Confirm recent transaction impacts",
        DetailKey = "Recent transactions can distort the plan until their planning impact is confirmed.",
        EvidenceText = input.UnclassifiedTransactionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        DestinationHref = $"transactions?month={input.ReferenceDate.Month}&year={input.ReferenceDate.Year}&planningImpact=Unclassified",
        PrimaryActionKey = "Open transactions",
        DueDate = input.ReferenceDate.AddDays(7),
      });
    }
  }
}
