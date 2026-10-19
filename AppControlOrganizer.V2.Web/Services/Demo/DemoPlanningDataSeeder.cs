//-----------------------------------------------------------------------
// <copyright file="DemoPlanningDataSeeder.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Demo;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Seeds the planning half of the showcase tenant: pillars, OKRs, quarters, reviews, risks,
/// habits, the annual plan, the weekly routine and the weekly focus.
/// </summary>
/// <remarks>
/// Every group is checked on its own and every date is derived from the reference date, so the
/// showcase stays complete and current without a code change. See <see cref="DemoDataSeeder"/>.
/// </remarks>
internal static class DemoPlanningDataSeeder
{
  /// <summary>Owner assigned to every showcase row.</summary>
  private const string Owner = DemoDataSeeder.Owner;

  /// <summary>Days of habit history kept behind the reference date.</summary>
  private const int HabitHistoryDays = 90;

  /// <summary>
  /// Ensures every planning group of the showcase tenant exists and is anchored to the reference date.
  /// </summary>
  /// <param name="db">Database context authorized to write to the showcase tenant.</param>
  /// <param name="logger">Logger used to report each group.</param>
  /// <param name="today">Reference date of the run.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>A task representing the seeding run.</returns>
  internal static async Task SeedAsync(LifePlannerContext db, ILogger logger, DateTime today, CancellationToken ct)
  {
    List<PillarEntity> pillars = await EnsurePillarsAsync(db, logger, ct);
    List<ObjectiveEntity> objectives = await EnsureObjectivesAsync(db, logger, pillars, today, ct);
    await EnsureKeyResultsAsync(db, logger, objectives, ct);
    await EnsureQuarterPlansAsync(db, logger, today, ct);
    await EnsureReviewsAsync(db, logger, today, ct);
    await EnsureRisksAsync(db, logger, pillars, ct);
    List<HabitEntity> habits = await EnsureHabitsAsync(db, logger, pillars, ct);
    await EnsureHabitLogsAsync(db, logger, habits, today, ct);
    await EnsureAnnualPlanAsync(db, logger, today, ct);
    await EnsureRoutineAsync(db, logger, ct);
    await EnsureVehiclesAsync(db, logger, today, ct);
    await EnsureWeeklyFocusesAsync(db, logger, today, ct);
    await EnsurePlanningReviewsAsync(db, logger, today, ct);
  }

  // ── Pillars, objectives and key results ─────────────────────────────────────

  private static async Task<List<PillarEntity>> EnsurePillarsAsync(LifePlannerContext db, ILogger logger, CancellationToken ct)
  {
    List<PillarEntity> existing = await db.Pillars
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == Owner && !item.Deleted)
      .ToListAsync(ct);
    if (existing.Count > 0)
    {
      return existing;
    }

    logger.LogInformation("Seeding showcase pillars...");
    PillarEntity[] pillars =
    {
      new() { OwnerUserId = Owner, Name = "Finances & Career", Description = "Build financial independence and grow professionally", ColorHex = "#0d6efd", IconCss = "bi bi-briefcase-fill", SortOrder = 1 },
      new() { OwnerUserId = Owner, Name = "Health & Wellbeing", Description = "Take care of body and mind for energy and longevity", ColorHex = "#198754", IconCss = "bi bi-heart-fill", SortOrder = 2 },
      new() { OwnerUserId = Owner, Name = "Learning & Growth", Description = "Develop new skills and expand knowledge continuously", ColorHex = "#6f42c1", IconCss = "bi bi-lightbulb-fill", SortOrder = 3 },
    };

    db.Pillars.AddRange(pillars);
    await db.SaveChangesAsync(ct);
    return pillars.ToList();
  }

  private static async Task<List<ObjectiveEntity>> EnsureObjectivesAsync(
    LifePlannerContext db,
    ILogger logger,
    List<PillarEntity> pillars,
    DateTime today,
    CancellationToken ct)
  {
    List<ObjectiveEntity> existing = await db.Objectives
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == Owner && !item.Deleted)
      .ToListAsync(ct);
    if (existing.Count > 0 || pillars.Count < 3)
    {
      return existing;
    }

    logger.LogInformation("Seeding showcase objectives...");
    int finance = pillars[0].Id;
    int health = pillars[1].Id;
    int growth = pillars[2].Id;
    DateTime yearEnd = new DateTime(today.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    ObjectiveEntity[] objectives =
    {
      new() { OwnerUserId = Owner, PillarId = finance, Title = "Increase income by 30% by year-end", Description = "Grow salary plus freelance income", Deadline = yearEnd, Status = RagStatus.Amber, SortOrder = 1 },
      new() { OwnerUserId = Owner, PillarId = finance, Title = "Build a 6-month emergency fund", Description = "Accumulate a daily-liquidity fixed income reserve", Deadline = today.AddMonths(4), Status = RagStatus.Green, SortOrder = 2 },
      new() { OwnerUserId = Owner, PillarId = health, Title = "Reach 10% body fat", Description = "Combine strength training and cardio with adjusted nutrition", Deadline = today.AddMonths(6), Status = RagStatus.Green, SortOrder = 1 },
      new() { OwnerUserId = Owner, PillarId = health, Title = "Run a half marathon", Description = "Complete 21km in under 2h15", Deadline = today.AddMonths(3), Status = RagStatus.Amber, SortOrder = 2 },
      new() { OwnerUserId = Owner, PillarId = growth, Title = "Master full-stack web development", Description = "Complete a 5-project portfolio and launch freelance services", Deadline = yearEnd, Status = RagStatus.Green, SortOrder = 1 },
    };

    foreach (ObjectiveEntity objective in objectives)
    {
      objective.Deadline = DateTime.SpecifyKind(objective.Deadline.GetValueOrDefault(yearEnd).Date, DateTimeKind.Utc);
    }

    db.Objectives.AddRange(objectives);
    await db.SaveChangesAsync(ct);
    return objectives.ToList();
  }

  private static async Task EnsureKeyResultsAsync(
    LifePlannerContext db,
    ILogger logger,
    List<ObjectiveEntity> objectives,
    CancellationToken ct)
  {
    bool exists = await db.KeyResults
      .IgnoreQueryFilters()
      .AnyAsync(item => item.OwnerUserId == Owner && !item.Deleted, ct);
    if (exists || objectives.Count < 5)
    {
      return;
    }

    logger.LogInformation("Seeding showcase key results...");
    DateTime now = DateTime.UtcNow;
    KeyResultEntity[] keyResults =
    {
      new() { OwnerUserId = Owner, ObjectiveId = objectives[0].Id, Title = "Monthly income of R$ 12,000", MetricUnit = "R$/month", Baseline = 8500m, Current = 9800m, Target = 12000m, Status = RagStatus.Amber, SortOrder = 1, WeeklyNote = "Freelance income is growing steadily", UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[0].Id, Title = "3 active freelance clients", MetricUnit = "clients", Baseline = 0m, Current = 1m, Target = 3m, Status = RagStatus.Amber, SortOrder = 2, UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[0].Id, Title = "Launch a digital product", MetricUnit = "products", Baseline = 0m, Current = 0m, Target = 1m, Status = RagStatus.Red, SortOrder = 3, WeeklyNote = "Still ideating; the niche needs to be defined", UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[1].Id, Title = "Accumulate R$ 30,000 in fixed income", MetricUnit = "R$", Baseline = 5000m, Current = 18000m, Target = 30000m, Status = RagStatus.Green, SortOrder = 1, WeeklyNote = "On pace with the monthly contribution", UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[2].Id, Title = "Reduce body fat to 10%", MetricUnit = "%", Baseline = 18m, Current = 15m, Target = 10m, Status = RagStatus.Green, SortOrder = 1, UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[2].Id, Title = "Train consistently 4x per week", MetricUnit = "days/week", Baseline = 1m, Current = 3m, Target = 4m, Status = RagStatus.Amber, SortOrder = 2, WeeklyNote = "Three workouts last week; one short of target", UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[3].Id, Title = "Run 21km under 2h15", MetricUnit = "current km", Baseline = 0m, Current = 10m, Target = 21m, Status = RagStatus.Amber, SortOrder = 1, WeeklyNote = "10km long run completed", UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[3].Id, Title = "Consecutive training weeks", MetricUnit = "weeks", Baseline = 0m, Current = 8m, Target = 16m, Status = RagStatus.Green, SortOrder = 2, UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[4].Id, Title = "Complete 5 portfolio projects", MetricUnit = "projects", Baseline = 0m, Current = 2m, Target = 5m, Status = RagStatus.Amber, SortOrder = 1, WeeklyNote = "Second project is 80% complete", UpdatedAt = now },
      new() { OwnerUserId = Owner, ObjectiveId = objectives[4].Id, Title = "Accumulated study hours", MetricUnit = "hours", Baseline = 0m, Current = 120m, Target = 300m, Status = RagStatus.Amber, SortOrder = 2, UpdatedAt = now },
    };

    db.KeyResults.AddRange(keyResults);
    await db.SaveChangesAsync(ct);
  }

  // ── Quarters ────────────────────────────────────────────────────────────────

  private static async Task EnsureQuarterPlansAsync(LifePlannerContext db, ILogger logger, DateTime today, CancellationToken ct)
  {
    int currentQuarter = ((today.Month - 1) / 3) + 1;
    List<QuarterPlanEntity> existing = await db.QuarterPlans
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == Owner && !item.Deleted)
      .ToListAsync(ct);

    // A showcase that stops at a past quarter reads as abandoned: keep the current year covered.
    bool covered = existing.Exists(item => item.Year == today.Year && item.Quarter == currentQuarter);
    if (covered)
    {
      return;
    }

    logger.LogInformation("Seeding showcase quarter plans for {Year}.", today.Year);
    string[] focuses = { "Financial and health foundation", "Income growth and endurance", "Consistency and delivery", "Consolidation and next-year planning" };
    string[] deliveries = { "Emergency fund at 50% + measured baseline", "First freelance client + 15km long run", "Portfolio online + steady contribution", "Year closed with reviewed targets" };
    string[] kpis =
    {
      "Savings >= R$ 3,000/month; workouts >= 3x/week",
      "Extra income >= R$ 1,500/month; long run >= 15km",
      "Two hours of study per day; contribution kept",
      "Reserve covering 6 months; plan reviewed",
    };

    for (int quarter = 1; quarter <= 4; quarter++)
    {
      if (existing.Exists(item => item.Year == today.Year && item.Quarter == quarter))
      {
        continue;
      }

      DateTime targetDate = new DateTime(today.Year, quarter * 3, 1, 0, 0, 0, DateTimeKind.Utc);
      targetDate = targetDate.AddDays(DateTime.DaysInMonth(today.Year, quarter * 3) - 1);

      QuarterPlanEntity plan = new QuarterPlanEntity
      {
        OwnerUserId = Owner,
        Year = today.Year,
        Quarter = quarter,
        Focus = focuses[quarter - 1],
        Delivery = deliveries[quarter - 1],
        TargetDate = targetDate,
        WeeklyCadence = "Weekly review every Sunday at 8 PM",
        KpiDescription = kpis[quarter - 1],
        Status = quarter < currentQuarter ? RagStatus.Green : quarter == currentQuarter ? RagStatus.Amber : RagStatus.Green,
      };

      db.QuarterPlans.Add(plan);
      await db.SaveChangesAsync(ct);

      // Past quarters read as delivered; the current one is deliberately half-done so the
      // progress indicators on the quarter screen have something to show.
      bool isPast = quarter < currentQuarter;
      string[] tasks =
      {
        "Open an investment account and transfer the initial reserve",
        "Set up the monthly budget with adjusted categories",
        "Start a periodized training protocol",
        "Publish the service landing page with the initial portfolio",
      };

      for (int index = 0; index < tasks.Length; index++)
      {
        db.QuarterTasks.Add(new QuarterTaskEntity
        {
          OwnerUserId = Owner,
          QuarterPlanId = plan.Id,
          Text = tasks[index],
          SortOrder = index + 1,
          Done = isPast || (quarter == currentQuarter && index < 2),
        });
      }

      await db.SaveChangesAsync(ct);
    }
  }

  // ── Reviews ─────────────────────────────────────────────────────────────────

  private static async Task EnsureReviewsAsync(LifePlannerContext db, ILogger logger, DateTime today, CancellationToken ct)
  {
    List<ReviewTemplateEntity> templates = await db.ReviewTemplates
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == Owner && !item.Deleted)
      .ToListAsync(ct);

    ReviewTemplateItemEntity[] weeklyItems;
    ReviewTemplateItemEntity[] monthlyItems;
    ReviewTemplateEntity? weekly = templates.Find(item => item.ReviewType == ReviewType.Weekly);
    ReviewTemplateEntity? monthly = templates.Find(item => item.ReviewType == ReviewType.Monthly);

    // Templates created by an earlier revision carry Portuguese names. The showcase defaults to
    // English, and the "create only when missing" rule below never rewrites an existing row, so the
    // rename has to happen explicitly.
    await RenameLegacyTemplateAsync(db, logger, weekly, "Weekly Review", ct);
    await RenameLegacyTemplateAsync(db, logger, monthly, "Monthly Review", ct);

    if (weekly == null || monthly == null)
    {
      logger.LogInformation("Seeding showcase review templates...");
      weekly ??= new ReviewTemplateEntity { OwnerUserId = Owner, Name = "Weekly Review", ReviewType = ReviewType.Weekly, IsDefault = true };
      monthly ??= new ReviewTemplateEntity { OwnerUserId = Owner, Name = "Monthly Review", ReviewType = ReviewType.Monthly, IsDefault = true };
      db.ReviewTemplates.Add(weekly);
      db.ReviewTemplates.Add(monthly);
      await db.SaveChangesAsync(ct);
    }

    weeklyItems = await ReadOrCreateItemsAsync(
      db,
      weekly.Id,
      new[]
      {
        "Review your weekly goals: what you planned versus what you delivered.",
        "Which habits were completed this week?",
        "What went well and deserves to be celebrated?",
        "What can improve next week?",
        "Set the 3 priorities for next week",
      },
      ct);

    monthlyItems = await ReadOrCreateItemsAsync(
      db,
      monthly.Id,
      new[]
      {
        "Review your OKRs and update each Key Result progress",
        "Analyze income and expenses and compare them with the planned budget",
        "Which habits did you keep consistently? What was the adherence rate?",
        "What was the biggest learning or insight of the month?",
        "Set 3 strategic priorities for next month",
        "Review and update registered risk statuses",
      },
      ct);

    bool runsExist = await db.ReviewRuns
      .IgnoreQueryFilters()
      .AnyAsync(item => item.OwnerUserId == Owner && !item.Deleted && item.RunDate >= today.AddDays(-45), ct);
    if (runsExist)
    {
      return;
    }

    logger.LogInformation("Seeding showcase review runs...");
    List<ReviewRunItemEntity> runItems = new();

    // Four recent weekly runs plus the latest monthly one give the history screen a real shape.
    for (int week = 4; week >= 1; week--)
    {
      ReviewRunEntity run = new ReviewRunEntity
      {
        OwnerUserId = Owner,
        TemplateId = weekly.Id,
        ReviewType = ReviewType.Weekly,
        RunDate = DateTime.SpecifyKind(today.AddDays(-7 * week), DateTimeKind.Utc),
        Notes = "Consistent week: workouts kept and the income key result moved forward.",
        ScoreEnergy = 6 + (week % 3),
        ScoreFocus = 7,
        ScoreFinances = 8,
        ScoreRelations = 6 + (week % 2),
      };
      db.ReviewRuns.Add(run);
      await db.SaveChangesAsync(ct);
      runItems.AddRange(BuildRunItems(run.Id, weeklyItems, weeklyItems.Length - (week % 2)));
    }

    ReviewRunEntity monthlyRun = new ReviewRunEntity
    {
      OwnerUserId = Owner,
      TemplateId = monthly.Id,
      ReviewType = ReviewType.Monthly,
      RunDate = DateTime.SpecifyKind(today.AddDays(-15), DateTimeKind.Utc),
      Notes = "Solid month. Every quarter task on track; relationships deserve more attention.",
      ScoreEnergy = 8,
      ScoreFocus = 7,
      ScoreFinances = 8,
      ScoreRelations = 7,
    };
    db.ReviewRuns.Add(monthlyRun);
    await db.SaveChangesAsync(ct);
    runItems.AddRange(BuildRunItems(monthlyRun.Id, monthlyItems, monthlyItems.Length - 2));

    db.ReviewRunItems.AddRange(runItems);
    await db.SaveChangesAsync(ct);
  }

  private static async Task<ReviewTemplateItemEntity[]> ReadOrCreateItemsAsync(
    LifePlannerContext db,
    int templateId,
    string[] texts,
    CancellationToken ct)
  {
    List<ReviewTemplateItemEntity> existing = await db.ReviewTemplateItems
      .IgnoreQueryFilters()
      .Where(item => item.TemplateId == templateId && !item.Deleted)
      .OrderBy(item => item.SortOrder)
      .ToListAsync(ct);
    if (existing.Count > 0)
    {
      return existing.ToArray();
    }

    ReviewTemplateItemEntity[] items = texts
      .Select((text, index) => new ReviewTemplateItemEntity { TemplateId = templateId, Text = text, SortOrder = index + 1 })
      .ToArray();
    db.ReviewTemplateItems.AddRange(items);
    await db.SaveChangesAsync(ct);
    return items;
  }

  private static List<ReviewRunItemEntity> BuildRunItems(int runId, ReviewTemplateItemEntity[] items, int doneCount)
  {
    return items
      .Select((item, index) => new ReviewRunItemEntity
      {
        ReviewRunId = runId,
        TemplateItemId = item.Id,
        Text = item.Text,
        SortOrder = item.SortOrder,
        Done = index < doneCount,
      })
      .ToList();
  }

  // ── Risks ───────────────────────────────────────────────────────────────────

  private static async Task EnsureRisksAsync(LifePlannerContext db, ILogger logger, List<PillarEntity> pillars, CancellationToken ct)
  {
    bool exists = await db.Risks
      .IgnoreQueryFilters()
      .AnyAsync(item => item.OwnerUserId == Owner && !item.Deleted, ct);
    if (exists || pillars.Count < 3)
    {
      return;
    }

    logger.LogInformation("Seeding showcase risks...");
    db.Risks.AddRange(
      new RiskEntity
      {
        OwnerUserId = Owner, PillarId = pillars[0].Id,
        Title = "Primary income loss",
        Description = "Job loss or a significant salary cut that compromises the financial plan",
        Probability = RiskProbability.Medium, Impact = RiskImpact.High, Status = RagStatus.Amber,
        EarlyWarning = "Structural company changes, negative reviews, increased conflict with leadership",
        Contingency = "A 6-month emergency fund covers the transition, with an active network for replacement opportunities",
        PlanB = "Intensive freelancing plus an active search for new opportunities for 30 days",
      },
      new RiskEntity
      {
        OwnerUserId = Owner, PillarId = pillars[1].Id,
        Title = "Overtraining injury",
        Description = "Overtraining leading to a muscle or joint injury that interrupts progress",
        Probability = RiskProbability.Medium, Impact = RiskImpact.Medium, Status = RagStatus.Green,
        EarlyWarning = "Persistent joint pain, performance drop for 2+ weeks, impaired sleep",
        Contingency = "Reduce volume immediately and schedule physical therapy in the first week",
        PlanB = "Replace with low-impact training (swimming, yoga) for 2-4 weeks",
      },
      new RiskEntity
      {
        OwnerUserId = Owner, PillarId = pillars[2].Id,
        Title = "Demotivation with technical study",
        Description = "Loss of engagement with learning leading to abandonment of the development plan",
        Probability = RiskProbability.Medium, Impact = RiskImpact.Low, Status = RagStatus.Green,
        EarlyWarning = "More than 5 days without studying, avoiding technical content, chronic procrastination",
        Contingency = "Switch study mode (video to practical project to mentoring) and adjust the environment",
        PlanB = "A structured one-week pause focused on creative projects to recharge",
      },
      new RiskEntity
      {
        OwnerUserId = Owner, PillarId = pillars[0].Id,
        Title = "Lifestyle creep compromising savings",
        Description = "Gradual spending increase as income improves, compromising savings goals",
        Probability = RiskProbability.High, Impact = RiskImpact.Medium, Status = RagStatus.Amber,
        EarlyWarning = "Variable expenses above 15% of budget for 2 consecutive months",
        Contingency = "Immediate budget review with cuts to the 3 largest nonessential expenses",
        PlanB = "Freeze nonessential expenses for 30 days and recalibrate the desired lifestyle level",
      });

    await db.SaveChangesAsync(ct);
  }

  // ── Habits ──────────────────────────────────────────────────────────────────

  private static async Task<List<HabitEntity>> EnsureHabitsAsync(
    LifePlannerContext db,
    ILogger logger,
    List<PillarEntity> pillars,
    CancellationToken ct)
  {
    List<HabitEntity> existing = await db.Habits
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == Owner && !item.Deleted)
      .ToListAsync(ct);
    if (existing.Count > 0 || pillars.Count < 3)
    {
      return existing;
    }

    logger.LogInformation("Seeding showcase habits...");
    HabitEntity[] habits =
    {
      new() { OwnerUserId = Owner, Name = "Drink 2L of water", Description = "Maintain proper hydration throughout the day", ColorHex = "#0ea5e9", IconCss = "bi bi-droplet-fill", FrequencyDays = "0,1,2,3,4,5,6", IsActive = true, PillarId = pillars[1].Id },
      new() { OwnerUserId = Owner, Name = "Physical exercise", Description = "Strength training or running; minimum 45-minute session", ColorHex = "#22c55e", IconCss = "bi bi-activity", FrequencyDays = "1,3,5,6", IsActive = true, PillarId = pillars[1].Id },
      new() { OwnerUserId = Owner, Name = "Read for 30 min", Description = "Read technical or personal development books before bed", ColorHex = "#f59e0b", IconCss = "bi bi-book", FrequencyDays = "0,1,2,3,4,5,6", IsActive = true, PillarId = pillars[2].Id },
      new() { OwnerUserId = Owner, Name = "Meditation 10 min", Description = "Mindfulness practice to reduce anxiety and improve focus", ColorHex = "#8b5cf6", IconCss = "bi bi-cloud", FrequencyDays = "0,1,2,3,4,5,6", IsActive = true },
      new() { OwnerUserId = Owner, Name = "Sleep before 11 PM", Description = "Ensure 7-8h of quality sleep on weekdays", ColorHex = "#6366f1", IconCss = "bi bi-moon-stars-fill", FrequencyDays = "1,2,3,4,5", IsActive = true, PillarId = pillars[1].Id },
    };

    db.Habits.AddRange(habits);
    await db.SaveChangesAsync(ct);
    return habits.ToList();
  }

  private static async Task EnsureHabitLogsAsync(
    LifePlannerContext db,
    ILogger logger,
    List<HabitEntity> habits,
    DateTime today,
    CancellationToken ct)
  {
    if (habits.Count == 0)
    {
      return;
    }

    DateTime windowStart = today.AddDays(-HabitHistoryDays);

    // The habit grid is read day by day, so history that stops weeks ago makes the streaks
    // meaningless. Missing days are filled in one by one — the unique (habit, date) index means a
    // rebuild would collide with itself, and filling gaps also preserves whatever is already there.
    List<HabitLogEntity> existing = await db.HabitLogs
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == Owner && item.LogDate >= windowStart)
      .ToListAsync(ct);
    if (existing.Count > 0 && existing.Max(item => item.LogDate) >= today.AddDays(-1))
    {
      return;
    }

    HashSet<string> alreadyLogged = existing
      .Select(item => FormatLogKey(item.HabitId, item.LogDate))
      .ToHashSet();

    List<HabitLogEntity> logs = new();
    for (int dayOffset = HabitHistoryDays; dayOffset >= 0; dayOffset--)
    {
      DateTime logDate = DateTime.SpecifyKind(windowStart.AddDays(HabitHistoryDays - dayOffset), DateTimeKind.Utc);
      int weekday = (int)logDate.DayOfWeek;

      foreach (HabitEntity habit in habits)
      {
        if (!IsScheduled(habit.FrequencyDays, weekday) || alreadyLogged.Contains(FormatLogKey(habit.Id, logDate)))
        {
          continue;
        }

        logs.Add(new HabitLogEntity
        {
          OwnerUserId = Owner,
          HabitId = habit.Id,
          LogDate = logDate,
          Done = IsDone(habit.Id, logDate),
        });
      }
    }

    if (logs.Count == 0)
    {
      return;
    }

    db.HabitLogs.AddRange(logs);
    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase habit logs filled in: {Count}.", logs.Count);
  }

  /// <summary>Builds the key that identifies one habit log, matching the unique index on the table.</summary>
  /// <param name="habitId">Habit the log belongs to.</param>
  /// <param name="logDate">Date the log covers.</param>
  /// <returns>A stable key for gap detection.</returns>
  private static string FormatLogKey(int habitId, DateTime logDate)
  {
    return string.Create(
      System.Globalization.CultureInfo.InvariantCulture,
      $"{habitId}|{logDate:yyyy-MM-dd}");
  }

  /// <summary>Reads the habit weekday schedule stored as a comma-separated list.</summary>
  /// <param name="frequencyDays">Configured weekdays, where 0 is Sunday.</param>
  /// <param name="weekday">Weekday under evaluation.</param>
  /// <returns><see langword="true"/> when the habit is scheduled for the weekday.</returns>
  private static bool IsScheduled(string? frequencyDays, int weekday)
  {
    if (string.IsNullOrWhiteSpace(frequencyDays))
    {
      return true;
    }

    return frequencyDays
      .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Contains(weekday.ToString(System.Globalization.CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// Decides adherence deterministically so the showcase looks human — around 75% completion,
  /// with misses that fall in the same places on every run rather than flickering between deploys.
  /// </summary>
  /// <param name="habitId">Habit being logged.</param>
  /// <param name="logDate">Date being logged.</param>
  /// <returns><see langword="true"/> when the habit counts as done.</returns>
  private static bool IsDone(int habitId, DateTime logDate)
  {
    int seed = (logDate.DayOfYear * 7) + (habitId * 13);
    return seed % 4 != 0;
  }

  // ── Annual plan, routine and weekly focus ───────────────────────────────────

  private static async Task EnsureAnnualPlanAsync(LifePlannerContext db, ILogger logger, DateTime today, CancellationToken ct)
  {
    AnnualPlanEntity? existing = await db.AnnualPlans
      .IgnoreQueryFilters()
      .FirstOrDefaultAsync(item => item.OwnerUserId == Owner && item.Year == today.Year && !item.Deleted, ct);
    if (existing != null)
    {
      // Older rows stored the bare year as the theme; refresh that one field without touching figures
      // a visitor may have seen change during the session.
      if (string.IsNullOrWhiteSpace(existing.Theme)
          || existing.Theme == today.Year.ToString(System.Globalization.CultureInfo.InvariantCulture))
      {
        existing.Theme = "Build the reserve, then compound it";
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Refreshed showcase annual plan theme.");
      }

      return;
    }

    logger.LogInformation("Seeding showcase annual plan for {Year}.", today.Year);
    AnnualPlanEntity plan = new AnnualPlanEntity { OwnerUserId = Owner, Year = today.Year };
    ApplyShowcaseAnnualPlan(plan);
    db.AnnualPlans.Add(plan);

    await db.SaveChangesAsync(ct);
  }

  /// <summary>
  /// Writes the neutral showcase figures onto an annual plan.
  /// </summary>
  /// <remarks>
  /// The targets are deliberately round and unremarkable: a visitor should read the plan screen as
  /// an example of the mechanics, not as somebody else's finances.
  /// </remarks>
  /// <param name="plan">Plan to fill in.</param>
  internal static void ApplyShowcaseAnnualPlan(AnnualPlanEntity plan)
  {
    plan.Theme = "Build the reserve, then compound it";
    plan.TargetNetWorth = 250000m;
    plan.PlannedMonthlyIncome = 10700m;
    plan.MonthlySpendingCeiling = 5500m;
    plan.PlannedAnnualExpenses = 12000m;
    plan.PlannedMonthlyContribution = null;
    plan.PlannedOneOffNetFlows = 0m;
    plan.EmergencyReserveMonths = 6;
    plan.UpdatedAtUtc = DateTime.UtcNow;
  }

  private static async Task EnsureRoutineAsync(LifePlannerContext db, ILogger logger, CancellationToken ct)
  {
    RoutineTemplateEntity? template = await db.RoutineTemplates
      .IgnoreQueryFilters()
      .Include(item => item.Blocks)
      .FirstOrDefaultAsync(item => item.OwnerUserId == Owner && item.IsActiveTemplate && !item.Deleted, ct);

    // A template still carrying the legacy marker holds the routine that used to be written from
    // code; its blocks are replaced by the neutral showcase week.
    bool isLegacy = template != null && template.InitialDataVersion > 0;
    if (template != null && !isLegacy && template.Blocks.Any(block => !block.Deleted))
    {
      return;
    }

    logger.LogInformation("Seeding showcase weekly routine...");
    if (template != null && isLegacy)
    {
      db.RoutineBlocks.RemoveRange(template.Blocks);
      template.InitialDataVersion = 0;
      await db.SaveChangesAsync(ct);
    }

    if (template == null)
    {
      template = new RoutineTemplateEntity { OwnerUserId = Owner, Name = "Weekly routine", IsActiveTemplate = true };
      db.RoutineTemplates.Add(template);
      await db.SaveChangesAsync(ct);
    }

    int sortOrder = 1;
    List<RoutineBlockEntity> blocks = new();

    void Add(DayOfWeek day, int startHour, int endHour, string title, string category, string iconCss, string colorHex)
    {
      blocks.Add(new RoutineBlockEntity
      {
        OwnerUserId = Owner,
        RoutineTemplateId = template!.Id,
        DayOfWeek = day,
        StartTime = new TimeOnly(startHour, 0),
        EndTime = new TimeOnly(endHour, 0),
        Title = title,
        Category = category,
        IconCss = iconCss,
        ColorHex = colorHex,
        SortOrder = sortOrder++,
      });
    }

    // A generic week a visitor can recognize: focused work, a training slot, study and a close-out.
    DayOfWeek[] weekdays = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
    foreach (DayOfWeek day in weekdays)
    {
      Add(day, 7, 8, "Morning routine", "Personal", "bi bi-sunrise", "#f59e0b");
      Add(day, 9, 12, "Focused work", "Work", "bi bi-briefcase", "#3b82f6");
      Add(day, 12, 13, "Lunch", "Personal", "bi bi-cup-hot", "#94a3b8");
      Add(day, 13, 18, "Work", "Work", "bi bi-briefcase", "#3b82f6");
      Add(day, 18, 19, "Training", "Health", "bi bi-heart-pulse", "#22c55e");
      Add(day, 20, 21, "Study", "Development", "bi bi-book", "#0ea5e9");
      Add(day, 22, 23, "Wind down", "Personal", "bi bi-moon-stars", "#6366f1");
    }

    Add(DayOfWeek.Saturday, 9, 11, "Long run", "Health", "bi bi-activity", "#22c55e");
    Add(DayOfWeek.Saturday, 15, 17, "Personal project", "Development", "bi bi-code-slash", "#0ea5e9");
    Add(DayOfWeek.Sunday, 10, 11, "Weekly planning", "Personal", "bi bi-calendar-check", "#8b5cf6");
    Add(DayOfWeek.Sunday, 20, 21, "Weekly review", "Personal", "bi bi-clock-history", "#8b5cf6");

    db.RoutineBlocks.AddRange(blocks);
    await db.SaveChangesAsync(ct);
    logger.LogInformation("Showcase routine seeded: {Count} blocks.", blocks.Count);
  }

  private static async Task EnsureWeeklyFocusesAsync(LifePlannerContext db, ILogger logger, DateTime today, CancellationToken ct)
  {
    DateOnly weekStart = DateOnly.FromDateTime(today.AddDays(-(int)today.DayOfWeek));
    bool exists = await db.WeeklyFocuses
      .IgnoreQueryFilters()
      .AnyAsync(item => item.OwnerUserId == Owner && item.WeekStartDate == weekStart && !item.Deleted, ct);
    if (exists)
    {
      return;
    }

    logger.LogInformation("Seeding showcase weekly focus for the week starting {Week:yyyy-MM-dd}.", weekStart);
    string[] texts =
    {
      "Close the second freelance proposal",
      "Keep four training sessions",
      "Finish the portfolio landing page",
    };

    for (int index = 0; index < texts.Length; index++)
    {
      db.WeeklyFocuses.Add(new WeeklyFocusEntity
      {
        OwnerUserId = Owner,
        WeekStartDate = weekStart,
        Text = texts[index],
        SortOrder = index + 1,
      });
    }

    await db.SaveChangesAsync(ct);
  }

  private static async Task EnsurePlanningReviewsAsync(LifePlannerContext db, ILogger logger, DateTime today, CancellationToken ct)
  {
    bool exists = await db.PlanningReviews
      .IgnoreQueryFilters()
      .AnyAsync(item => item.OwnerUserId == Owner && !item.Deleted && item.ReviewDate >= DateOnly.FromDateTime(today.AddDays(-45)), ct);
    if (exists)
    {
      return;
    }

    logger.LogInformation("Seeding showcase light reviews...");
    for (int week = 3; week >= 1; week--)
    {
      db.PlanningReviews.Add(new PlanningReviewEntity
      {
        OwnerUserId = Owner,
        ReviewType = ReviewType.Weekly,
        Status = PlanningReviewStatus.Completed,
        ReviewDate = DateOnly.FromDateTime(today.AddDays(-7 * week)),
        ProgressNotes = "Contribution kept and the emergency reserve moved closer to target.",
        BlockerNotes = "Two evenings lost to unplanned commitments.",
        DecisionNotes = "Protect the study block and move the review to Sunday morning.",
        CompletedAtUtc = DateTime.SpecifyKind(today.AddDays(-7 * week), DateTimeKind.Utc),
      });
    }

    await db.SaveChangesAsync(ct);
  }

  // ── Reviews: legacy naming ──────────────────────────────────────────────────

  /// <summary>Renames a showcase review template left with a non-default name by an older revision.</summary>
  /// <param name="db">Database context scoped to the caller.</param>
  /// <param name="logger">Logger used to report the rename.</param>
  /// <param name="template">Template to inspect; ignored when null.</param>
  /// <param name="expectedName">Name the showcase template must carry.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>A task representing the rename.</returns>
  private static async Task RenameLegacyTemplateAsync(
    LifePlannerContext db,
    ILogger logger,
    ReviewTemplateEntity? template,
    string expectedName,
    CancellationToken ct)
  {
    if (template == null || string.Equals(template.Name, expectedName, StringComparison.Ordinal))
    {
      return;
    }

    logger.LogInformation(
      "Renaming showcase review template {OldName} to {NewName}.", template.Name, expectedName);
    template.Name = expectedName;
    await db.SaveChangesAsync(ct);
  }

  // ── Vehicles ────────────────────────────────────────────────────────────────

  /// <summary>
  /// Ensures the showcase tenant owns vehicles with maintenance history and open reminders.
  /// </summary>
  /// <remarks>
  /// Without this group the vehicles screen is the only page a visitor reaches empty, which reads as
  /// a broken feature rather than an unused one. Dates are derived from the reference date so the
  /// history stays plausible as time passes.
  /// </remarks>
  /// <param name="db">Database context scoped to the caller.</param>
  /// <param name="logger">Logger used to report the group.</param>
  /// <param name="today">Reference date for the run.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>A task representing the seeding of the group.</returns>
  private static async Task EnsureVehiclesAsync(
    LifePlannerContext db,
    ILogger logger,
    DateTime today,
    CancellationToken ct)
  {
    List<VehicleEntity> existing = await db.Vehicles
      .IgnoreQueryFilters()
      .Where(item => item.OwnerUserId == Owner && !item.Deleted)
      .ToListAsync(ct);
    if (existing.Count > 0)
    {
      return;
    }

    logger.LogInformation("Seeding showcase vehicles...");

    VehicleEntity car = new VehicleEntity
    {
      OwnerUserId = Owner,
      VehicleType = VehicleType.Car,
      Name = "Family car",
      Brand = "Honda",
      Model = "Civic",
      Year = 2019,
      Plate = "ABC-1D23",
      CurrentOdometer = 68500m,
      OdometerUnit = "Km",
      IconCss = "bi bi-car-front",
      ColorHex = "#2563eb",
      Notes = "Weekday commute and weekend trips.",
      UpdatedAtUtc = DateTime.UtcNow,
    };

    VehicleEntity motorcycle = new VehicleEntity
    {
      OwnerUserId = Owner,
      VehicleType = VehicleType.Motorcycle,
      Name = "City bike",
      Brand = "Yamaha",
      Model = "MT-03",
      Year = 2022,
      Plate = "XYZ-4E56",
      CurrentOdometer = 14200m,
      OdometerUnit = "Km",
      IconCss = "bi bi-bicycle",
      ColorHex = "#f59e0b",
      Notes = "Short trips and traffic days.",
      UpdatedAtUtc = DateTime.UtcNow,
    };

    db.Vehicles.Add(car);
    db.Vehicles.Add(motorcycle);
    await db.SaveChangesAsync(ct);

    DateOnly reference = DateOnly.FromDateTime(today);

    db.VehicleRecords.AddRange(
      BuildVehicleRecord(car.Id, VehicleRecordType.OilChange, reference.AddDays(-40), 64200m, "Oil and filter change", "Maintenance", 320m, true, "City garage"),
      BuildVehicleRecord(car.Id, VehicleRecordType.Fuel, reference.AddDays(-21), 66100m, "Fuel fill-up", "Fuel", 280m, false, "Highway station", 44m),
      BuildVehicleRecord(car.Id, VehicleRecordType.Maintenance, reference.AddDays(-12), 67400m, "Front brake pads", "Maintenance", 540m, false, "City garage"),
      BuildVehicleRecord(car.Id, VehicleRecordType.Fuel, reference.AddDays(-4), 68300m, "Fuel fill-up", "Fuel", 265m, false, "Neighbourhood station", 41m),
      BuildVehicleRecord(car.Id, VehicleRecordType.Expense, reference.AddDays(-2), 68450m, "Annual insurance instalment", "Insurance", 410m, false, null),
      BuildVehicleRecord(motorcycle.Id, VehicleRecordType.OilChange, reference.AddDays(-30), 12800m, "Oil change", "Maintenance", 180m, true, "Bike shop"),
      BuildVehicleRecord(motorcycle.Id, VehicleRecordType.Fuel, reference.AddDays(-6), 14050m, "Fuel fill-up", "Fuel", 95m, false, "Neighbourhood station", 14m));

    db.VehicleReminders.AddRange(
      BuildVehicleReminder(car.Id, "Next oil change", "Maintenance", reference.AddDays(45), 72000m),
      BuildVehicleReminder(car.Id, "Tyre rotation", "Maintenance", reference.AddDays(20), 70000m),
      BuildVehicleReminder(car.Id, "Licensing renewal", "Documentation", reference.AddDays(70), null),
      BuildVehicleReminder(motorcycle.Id, "Chain inspection", "Maintenance", reference.AddDays(15), 15000m));

    await db.SaveChangesAsync(ct);
  }

  /// <summary>Builds one showcase vehicle history record.</summary>
  /// <param name="vehicleId">Owning vehicle identifier.</param>
  /// <param name="recordType">Kind of record.</param>
  /// <param name="recordDate">Date the record refers to.</param>
  /// <param name="odometer">Odometer reading at the time.</param>
  /// <param name="title">Record title.</param>
  /// <param name="category">Record category.</param>
  /// <param name="amount">Amount spent.</param>
  /// <param name="oilChanged">Whether the service included an oil change.</param>
  /// <param name="location">Where the service happened.</param>
  /// <param name="quantity">Fuelled quantity, when applicable.</param>
  /// <returns>The record to persist.</returns>
  private static VehicleRecordEntity BuildVehicleRecord(
    int vehicleId,
    VehicleRecordType recordType,
    DateOnly recordDate,
    decimal odometer,
    string title,
    string category,
    decimal amount,
    bool oilChanged,
    string? location,
    decimal? quantity = null)
  {
    return new VehicleRecordEntity
    {
      OwnerUserId = Owner,
      VehicleId = vehicleId,
      RecordType = recordType,
      RecordDate = recordDate,
      Odometer = odometer,
      Title = title,
      Category = category,
      Amount = amount,
      Quantity = quantity,
      OilChanged = oilChanged,
      Location = location,
      UpdatedAtUtc = DateTime.UtcNow,
    };
  }

  /// <summary>Builds one open showcase vehicle reminder.</summary>
  /// <param name="vehicleId">Owning vehicle identifier.</param>
  /// <param name="title">Reminder title.</param>
  /// <param name="category">Reminder category.</param>
  /// <param name="dueDate">Date the reminder falls due.</param>
  /// <param name="dueOdometer">Odometer reading the reminder falls due at, when applicable.</param>
  /// <returns>The reminder to persist.</returns>
  private static VehicleReminderEntity BuildVehicleReminder(
    int vehicleId,
    string title,
    string category,
    DateOnly dueDate,
    decimal? dueOdometer)
  {
    return new VehicleReminderEntity
    {
      OwnerUserId = Owner,
      VehicleId = vehicleId,
      Title = title,
      Category = category,
      DueDate = dueDate,
      DueOdometer = dueOdometer,
      Status = VehicleReminderStatus.Open,
      UpdatedAtUtc = DateTime.UtcNow,
    };
  }
}
