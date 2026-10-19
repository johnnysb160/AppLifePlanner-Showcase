//-----------------------------------------------------------------------
// <copyright file="VehicleService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Vehicles;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implements the personal vehicle routine domain without external dependencies.
/// </summary>
public sealed class VehicleService : IVehicleService
{
  private const string OilReminderCategory = "Oil";
  private const string OilReminderTitle = "Next oil change";
  private const int SoonDays = 30;
  private const decimal SoonKilometers = 500m;

  private readonly IDbContextFactory<LifePlannerContext> contextFactory;

  /// <summary>
  /// Initializes a new instance of the <see cref="VehicleService"/> class.
  /// </summary>
  /// <param name="contextFactory">Database context factory.</param>
  public VehicleService(IDbContextFactory<LifePlannerContext> contextFactory)
  {
    this.contextFactory = contextFactory;
  }

  /// <inheritdoc />
  public async Task<VehiclePageState> GetPageStateAsync(
    string userId,
    int? selectedVehicleId = null,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    List<VehicleEntity> vehicles = await context.Vehicles
      .AsNoTracking()
      .Include(item => item.Records)
      .Include(item => item.Reminders)
      .Where(item => item.OwnerUserId == userId)
      .OrderByDescending(item => item.Active)
      .ThenBy(item => item.Name)
      .ToListAsync(cancellationToken);

    DateOnly today = DateOnly.FromDateTime(DateTime.Today);
    List<VehicleSummary> summaries = vehicles
      .Select(vehicle => BuildSummary(vehicle, today))
      .ToList();
    int? resolvedSelectedVehicleId = ResolveSelectedVehicleId(summaries, selectedVehicleId);
    VehicleSummary? selectedSummary = summaries.FirstOrDefault(item => item.Vehicle.Id == resolvedSelectedVehicleId);
    List<VehicleRecordEntity> selectedRecords = selectedSummary?.Vehicle.Records
      .Where(item => !item.Deleted)
      .OrderByDescending(item => item.RecordDate)
      .ThenByDescending(item => item.Id)
      .ToList() ?? new List<VehicleRecordEntity>();
    List<VehicleReminderView> selectedReminders = selectedSummary?.Vehicle.Reminders
      .Where(item => !item.Deleted)
      .Select(item => BuildReminderView(item, selectedSummary.CurrentOdometer, today))
      .OrderBy(item => item.Reminder.Status == VehicleReminderStatus.Open ? 0 : 1)
      .ThenByDescending(item => item.DueState == VehicleDueState.Overdue)
      .ThenByDescending(item => item.DueState == VehicleDueState.Soon)
      .ThenBy(item => item.Reminder.DueDate ?? DateOnly.MaxValue)
      .ThenBy(item => item.Reminder.DueOdometer ?? decimal.MaxValue)
      .ToList() ?? new List<VehicleReminderView>();

    return new VehiclePageState
    {
      Vehicles = summaries,
      SelectedVehicleId = resolvedSelectedVehicleId,
      Records = selectedRecords,
      Reminders = selectedReminders,
      SelectedMonthCost = selectedSummary?.MonthCost ?? 0m,
      SelectedYearCost = selectedRecords
        .Where(item => item.RecordDate.Year == today.Year)
        .Sum(item => item.Amount ?? 0m),
    };
  }

  /// <inheritdoc />
  public async Task<VehicleEntity> SaveVehicleAsync(
    VehicleEntity vehicle,
    string userId,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(vehicle.Name))
    {
      throw new InvalidOperationException("Vehicle name is required.");
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    VehicleEntity? target = vehicle.Id == 0
      ? null
      : await context.Vehicles.FirstOrDefaultAsync(
        item => item.Id == vehicle.Id && item.OwnerUserId == userId,
        cancellationToken);
    if (target == null)
    {
      target = new VehicleEntity
      {
        OwnerUserId = userId,
      };
      context.Vehicles.Add(target);
    }

    target.VehicleType = vehicle.VehicleType;
    target.Name = vehicle.Name.Trim();
    target.Brand = TrimOrNull(vehicle.Brand);
    target.Model = TrimOrNull(vehicle.Model);
    target.Year = vehicle.Year;
    target.Plate = TrimOrNull(vehicle.Plate);
    target.CurrentOdometer = Math.Max(0m, vehicle.CurrentOdometer);
    target.OdometerUnit = "Km";
    target.IconCss = string.IsNullOrWhiteSpace(vehicle.IconCss) ? GetDefaultIcon(vehicle.VehicleType) : vehicle.IconCss.Trim();
    target.ColorHex = string.IsNullOrWhiteSpace(vehicle.ColorHex) ? "#2563eb" : vehicle.ColorHex.Trim();
    target.ImageDataUrl = TrimOrNull(vehicle.ImageDataUrl);
    target.Notes = TrimOrNull(vehicle.Notes);
    target.Active = vehicle.Active;
    target.UpdatedAtUtc = DateTime.UtcNow;

    await context.SaveChangesAsync(cancellationToken);
    return target;
  }

  /// <inheritdoc />
  public async Task DeleteVehicleAsync(int vehicleId, string userId, CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    VehicleEntity? vehicle = await context.Vehicles
      .FirstOrDefaultAsync(item => item.Id == vehicleId && item.OwnerUserId == userId, cancellationToken);
    if (vehicle == null)
    {
      return;
    }

    vehicle.Active = false;
    vehicle.Deleted = true;
    vehicle.UpdatedAtUtc = DateTime.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<VehicleRecordEntity> SaveRecordAsync(
    VehicleRecordEntity record,
    string userId,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(record.Title))
    {
      throw new InvalidOperationException("Vehicle record title is required.");
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    VehicleEntity vehicle = await context.Vehicles
      .FirstOrDefaultAsync(item => item.Id == record.VehicleId && item.OwnerUserId == userId, cancellationToken)
      ?? throw new InvalidOperationException("Vehicle not found for the current user.");
    VehicleRecordEntity? target = record.Id == 0
      ? null
      : await context.VehicleRecords.FirstOrDefaultAsync(
        item => item.Id == record.Id && item.OwnerUserId == userId,
        cancellationToken);
    if (target == null)
    {
      target = new VehicleRecordEntity
      {
        OwnerUserId = userId,
        VehicleId = vehicle.Id,
      };
      context.VehicleRecords.Add(target);
    }

    target.VehicleId = vehicle.Id;
    target.RecordType = record.RecordType;
    target.RecordDate = record.RecordDate;
    target.Odometer = NormalizeOptionalDecimal(record.Odometer);
    target.Title = record.Title.Trim();
    target.Category = TrimOrNull(record.Category);
    target.Amount = NormalizeOptionalDecimal(record.Amount);
    target.Quantity = NormalizeOptionalDecimal(record.Quantity);
    target.OilChanged = record.OilChanged || record.RecordType == VehicleRecordType.OilChange;
    target.ServiceItems = TrimOrNull(record.ServiceItems);
    target.Location = TrimOrNull(record.Location);
    target.Notes = TrimOrNull(record.Notes);
    target.UpdatedAtUtc = DateTime.UtcNow;

    if (target.Odometer.HasValue && target.Odometer.Value > vehicle.CurrentOdometer)
    {
      vehicle.CurrentOdometer = target.Odometer.Value;
      vehicle.UpdatedAtUtc = DateTime.UtcNow;
    }

    if (target.OilChanged)
    {
      await EnsureOilReminderAsync(context, vehicle, target, userId, cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);
    return target;
  }

  /// <inheritdoc />
  public async Task DeleteRecordAsync(int recordId, string userId, CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    VehicleRecordEntity? record = await context.VehicleRecords
      .FirstOrDefaultAsync(item => item.Id == recordId && item.OwnerUserId == userId, cancellationToken);
    if (record == null)
    {
      return;
    }

    record.Deleted = true;
    record.Active = false;
    record.UpdatedAtUtc = DateTime.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<VehicleReminderEntity> SaveReminderAsync(
    VehicleReminderEntity reminder,
    string userId,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(reminder.Title))
    {
      throw new InvalidOperationException("Vehicle reminder title is required.");
    }

    if (!reminder.DueDate.HasValue && !reminder.DueOdometer.HasValue)
    {
      throw new InvalidOperationException("Vehicle reminder needs a due date or odometer.");
    }

    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    VehicleEntity vehicle = await context.Vehicles
      .FirstOrDefaultAsync(item => item.Id == reminder.VehicleId && item.OwnerUserId == userId, cancellationToken)
      ?? throw new InvalidOperationException("Vehicle not found for the current user.");
    VehicleReminderEntity? target = reminder.Id == 0
      ? null
      : await context.VehicleReminders.FirstOrDefaultAsync(
        item => item.Id == reminder.Id && item.OwnerUserId == userId,
        cancellationToken);
    if (target == null)
    {
      target = new VehicleReminderEntity
      {
        OwnerUserId = userId,
        VehicleId = vehicle.Id,
        Status = VehicleReminderStatus.Open,
      };
      context.VehicleReminders.Add(target);
    }

    target.VehicleId = vehicle.Id;
    target.Title = reminder.Title.Trim();
    target.Category = TrimOrNull(reminder.Category);
    target.DueDate = reminder.DueDate;
    target.DueOdometer = NormalizeOptionalDecimal(reminder.DueOdometer);
    target.Status = reminder.Status;
    target.Notes = TrimOrNull(reminder.Notes);
    target.UpdatedAtUtc = DateTime.UtcNow;
    if (target.Status == VehicleReminderStatus.Open)
    {
      target.ResolvedAtUtc = null;
      target.CompletedByRecordId = null;
    }

    await context.SaveChangesAsync(cancellationToken);
    return target;
  }

  /// <inheritdoc />
  public async Task UpdateReminderStatusAsync(
    int reminderId,
    VehicleReminderStatus status,
    string userId,
    int? completedByRecordId = null,
    CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    VehicleReminderEntity? reminder = await context.VehicleReminders
      .FirstOrDefaultAsync(item => item.Id == reminderId && item.OwnerUserId == userId, cancellationToken);
    if (reminder == null)
    {
      return;
    }

    reminder.Status = status;
    reminder.CompletedByRecordId = status == VehicleReminderStatus.Done ? completedByRecordId : null;
    reminder.ResolvedAtUtc = status == VehicleReminderStatus.Open ? null : DateTime.UtcNow;
    reminder.UpdatedAtUtc = DateTime.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task DeleteReminderAsync(int reminderId, string userId, CancellationToken cancellationToken = default)
  {
    await using LifePlannerContext context = await this.contextFactory.CreateDbContextAsync(cancellationToken);
    VehicleReminderEntity? reminder = await context.VehicleReminders
      .FirstOrDefaultAsync(item => item.Id == reminderId && item.OwnerUserId == userId, cancellationToken);
    if (reminder == null)
    {
      return;
    }

    reminder.Deleted = true;
    reminder.Active = false;
    reminder.UpdatedAtUtc = DateTime.UtcNow;
    await context.SaveChangesAsync(cancellationToken);
  }

  private static VehicleSummary BuildSummary(VehicleEntity vehicle, DateOnly today)
  {
    decimal currentOdometer = Math.Max(
      vehicle.CurrentOdometer,
      vehicle.Records
        .Where(item => !item.Deleted && item.Odometer.HasValue)
        .Select(item => item.Odometer!.Value)
        .DefaultIfEmpty(0m)
        .Max());
    DateOnly monthStart = new DateOnly(today.Year, today.Month, 1);
    VehicleRecordEntity? lastService = vehicle.Records
      .Where(item => !item.Deleted
        && (item.RecordType == VehicleRecordType.Maintenance || item.RecordType == VehicleRecordType.OilChange))
      .OrderByDescending(item => item.RecordDate)
      .ThenByDescending(item => item.Id)
      .FirstOrDefault();
    List<VehicleReminderView> openReminders = vehicle.Reminders
      .Where(item => !item.Deleted && item.Status == VehicleReminderStatus.Open)
      .Select(item => BuildReminderView(item, currentOdometer, today))
      .OrderByDescending(item => item.DueState == VehicleDueState.Overdue)
      .ThenByDescending(item => item.DueState == VehicleDueState.Soon)
      .ThenBy(item => item.Reminder.DueDate ?? DateOnly.MaxValue)
      .ThenBy(item => item.Reminder.DueOdometer ?? decimal.MaxValue)
      .ToList();

    return new VehicleSummary
    {
      Vehicle = vehicle,
      CurrentOdometer = currentOdometer,
      MonthCost = vehicle.Records
        .Where(item => !item.Deleted && item.RecordDate >= monthStart && item.RecordDate <= today)
        .Sum(item => item.Amount ?? 0m),
      LastService = lastService,
      NextReminder = openReminders.FirstOrDefault(),
    };
  }

  private static VehicleReminderView BuildReminderView(
    VehicleReminderEntity reminder,
    decimal currentOdometer,
    DateOnly today)
  {
    int? daysUntilDue = reminder.DueDate.HasValue ? reminder.DueDate.Value.DayNumber - today.DayNumber : null;
    decimal? odometerUntilDue = reminder.DueOdometer.HasValue ? reminder.DueOdometer.Value - currentOdometer : null;
    VehicleDueState dueState = ResolveDueState(reminder, daysUntilDue, odometerUntilDue);
    return new VehicleReminderView
    {
      Reminder = reminder,
      DueState = dueState,
      DaysUntilDue = daysUntilDue,
      OdometerUntilDue = odometerUntilDue,
    };
  }

  private static VehicleDueState ResolveDueState(
    VehicleReminderEntity reminder,
    int? daysUntilDue,
    decimal? odometerUntilDue)
  {
    if (reminder.Status != VehicleReminderStatus.Open)
    {
      return VehicleDueState.Resolved;
    }

    if (daysUntilDue.HasValue && daysUntilDue.Value < 0)
    {
      return VehicleDueState.Overdue;
    }

    if (odometerUntilDue.HasValue && odometerUntilDue.Value <= 0m)
    {
      return VehicleDueState.Overdue;
    }

    if (daysUntilDue.HasValue && daysUntilDue.Value <= SoonDays)
    {
      return VehicleDueState.Soon;
    }

    if (odometerUntilDue.HasValue && odometerUntilDue.Value <= SoonKilometers)
    {
      return VehicleDueState.Soon;
    }

    return daysUntilDue.HasValue || odometerUntilDue.HasValue
      ? VehicleDueState.Current
      : VehicleDueState.Neutral;
  }

  private static int? ResolveSelectedVehicleId(List<VehicleSummary> vehicles, int? selectedVehicleId)
  {
    if (selectedVehicleId.HasValue && vehicles.Any(item => item.Vehicle.Id == selectedVehicleId.Value))
    {
      return selectedVehicleId.Value;
    }

    return vehicles.FirstOrDefault(item => item.Vehicle.Active)?.Vehicle.Id
      ?? vehicles.FirstOrDefault()?.Vehicle.Id;
  }

  private static decimal? NormalizeOptionalDecimal(decimal? value)
  {
    if (!value.HasValue || value.Value <= 0m)
    {
      return null;
    }

    return value.Value;
  }

  private static string? TrimOrNull(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    return value.Trim();
  }

  private static string GetDefaultIcon(VehicleType vehicleType)
  {
    return vehicleType == VehicleType.Motorcycle ? "bi bi-bicycle" : "bi bi-car-front";
  }

  private static async Task EnsureOilReminderAsync(
    LifePlannerContext context,
    VehicleEntity vehicle,
    VehicleRecordEntity record,
    string userId,
    CancellationToken cancellationToken)
  {
    OilReminderRule rule = ResolveOilReminderRule(vehicle);
    VehicleReminderEntity? reminder = await context.VehicleReminders
      .FirstOrDefaultAsync(
        item => item.VehicleId == vehicle.Id
          && item.OwnerUserId == userId
          && item.Status == VehicleReminderStatus.Open
          && item.Category == OilReminderCategory,
        cancellationToken);

    if (reminder == null)
    {
      reminder = new VehicleReminderEntity
      {
        OwnerUserId = userId,
        VehicleId = vehicle.Id,
        Status = VehicleReminderStatus.Open,
        Category = OilReminderCategory,
      };
      context.VehicleReminders.Add(reminder);
    }

    reminder.Title = OilReminderTitle;
    reminder.DueDate = record.RecordDate.AddMonths(rule.Months);
    reminder.DueOdometer = record.Odometer.HasValue ? record.Odometer.Value + rule.Kilometers : null;
    reminder.CompletedByRecordId = record.Id == 0 ? null : record.Id;
    reminder.ResolvedAtUtc = null;
    reminder.UpdatedAtUtc = DateTime.UtcNow;
  }

  private static OilReminderRule ResolveOilReminderRule(VehicleEntity vehicle)
  {
    if (vehicle.VehicleType == VehicleType.Motorcycle)
    {
      bool oldMotorcycle = vehicle.Year.HasValue && vehicle.Year.Value <= 2010;
      return oldMotorcycle ? new OilReminderRule(4, 2_000m) : new OilReminderRule(6, 3_000m);
    }

    return new OilReminderRule(12, 10_000m);
  }

  private readonly record struct OilReminderRule(int Months, decimal Kilometers);
}
