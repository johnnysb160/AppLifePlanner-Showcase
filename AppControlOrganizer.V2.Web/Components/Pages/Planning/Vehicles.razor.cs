//-----------------------------------------------------------------------
// <copyright file="Vehicles.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Pages.Planning;

using System.Security.Claims;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Helpers;
using AppControlOrganizer.V2.Web.Services.Vehicles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Code-behind for the personal vehicle routine page.
/// </summary>
public partial class Vehicles : ComponentBase, IDisposable
{
  private const string TimelineTab = "Timeline";
  private const string MaintenanceTab = "Maintenance";
  private const string FuelTab = "Fuel";
  private const string ExpensesTab = "Expenses";
  private const string RemindersTab = "Reminders";
  private const long MaxVehicleImageBytes = 5 * 1024 * 1024;

  private static readonly string[] Tabs =
  {
    TimelineTab,
    MaintenanceTab,
    FuelTab,
    ExpensesTab,
    RemindersTab,
  };

  /// <summary>Gets or sets the requested vehicle identifier.</summary>
  [Parameter]
  [SupplyParameterFromQuery(Name = "vehicleId")]
  public int? RequestedVehicleId { get; set; }

  /// <summary>Gets or sets the authentication state provider.</summary>
  [Inject]
  public AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

  /// <summary>Gets or sets the vehicle service.</summary>
  [Inject]
  public IVehicleService VehicleService { get; set; } = null!;

  /// <summary>Gets or sets the service backing the header show/hide values toggle.</summary>
  [Inject]
  public AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService ValuesVis { get; set; } = null!;

  private string userId = string.Empty;
  private bool isReadOnly = true;
  private bool isLoading = true;
  private bool isSaving;
  private bool hasError;
  private bool showVehicleModal;
  private bool showRecordModal;
  private bool showReminderModal;
  private int? selectedVehicleId;
  private string activeTab = TimelineTab;
  private string message = string.Empty;
  private string customServiceItem = string.Empty;
  private VehiclePageState state = new();
  private VehicleEntity editingVehicle = new();
  private VehicleRecordEntity editingRecord = new();
  private VehicleReminderEntity editingReminder = new();
  private DateTime recordDateInput = DateTime.Today;
  private DateTime? reminderDueDateInput;
  private HashSet<string> selectedServiceItems = new(StringComparer.OrdinalIgnoreCase);

  private static readonly string[] CommonServiceItems =
  {
    "Oil change",
    "Fuel",
    "Expense",
    "Odometer",
    "Inspection",
    "Tires",
    "Brakes",
    "Battery",
    "Accessory",
    "Insurance",
    "Tax",
    "Parking",
    "Wash",
    "Labor",
  };

  private static readonly string[] CarServiceItems =
  {
    "Alignment",
    "Balancing",
    "Air filter",
    "Cabin filter",
    "Spark plugs",
    "Coolant",
    "Transmission oil",
    "Brake fluid",
    "Suspension",
    "Wipers",
  };

  private static readonly string[] MotorcycleServiceItems =
  {
    "Chain",
    "Sprocket",
    "Rear tire",
    "Front tire",
    "Brake pads",
    "Clutch cable",
    "Throttle cable",
    "Fork oil",
    "Engine adjustment",
    "Valve adjustment",
  };

  private VehicleSummary? SelectedVehicle
  {
    get
    {
      return this.state.Vehicles.FirstOrDefault(item => item.Vehicle.Id == this.selectedVehicleId);
    }
  }

  private IEnumerable<VehicleRecordEntity> FilteredRecords
  {
    get
    {
      return this.activeTab switch
      {
        MaintenanceTab => this.state.Records.Where(item =>
          item.RecordType == VehicleRecordType.Maintenance || item.RecordType == VehicleRecordType.OilChange),
        FuelTab => this.state.Records.Where(item => item.RecordType == VehicleRecordType.Fuel),
        ExpensesTab => this.state.Records.Where(item => item.RecordType == VehicleRecordType.Expense),
        _ => this.state.Records,
      };
    }
  }

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    this.ValuesVis.OnChange += this.OnValuesVisibilityChanged;
    AuthenticationState authState = await this.AuthStateProvider.GetAuthenticationStateAsync();
    this.userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? LifePlannerContext.DemoUserId;
    this.isReadOnly = this.userId == LifePlannerContext.DemoUserId;
    this.selectedVehicleId = this.RequestedVehicleId;
    await this.ReloadAsync();
  }

  private async Task ReloadAsync()
  {
    this.state = await this.VehicleService.GetPageStateAsync(this.userId, this.selectedVehicleId);
    this.selectedVehicleId = this.state.SelectedVehicleId;
    this.isLoading = false;
  }

  private async Task SelectVehicleAsync(ChangeEventArgs args)
  {
    if (int.TryParse(args.Value?.ToString(), out int vehicleId))
    {
      await this.SelectVehicleCardAsync(vehicleId);
    }
  }

  private async Task SelectVehicleCardAsync(int vehicleId)
  {
    this.selectedVehicleId = vehicleId;
    await this.ReloadAsync();
  }

  private void NewVehicle()
  {
    if (this.isReadOnly)
    {
      return;
    }

    this.editingVehicle = new VehicleEntity
    {
      VehicleType = VehicleType.Car,
      Active = true,
      IconCss = "bi bi-car-front",
      ColorHex = "#2563eb",
      OdometerUnit = "Km",
    };
    this.showVehicleModal = true;
  }

  private void EditSelectedVehicle()
  {
    VehicleEntity? vehicle = this.SelectedVehicle?.Vehicle;
    if (vehicle == null || this.isReadOnly)
    {
      return;
    }

    this.editingVehicle = CloneVehicle(vehicle);
    this.showVehicleModal = true;
  }

  private async Task SaveVehicleAsync()
  {
    if (this.isReadOnly)
    {
      return;
    }

    await this.RunSavingAsync(async () =>
    {
      VehicleEntity saved = await this.VehicleService.SaveVehicleAsync(this.editingVehicle, this.userId);
      this.selectedVehicleId = saved.Id;
      this.showVehicleModal = false;
      await this.ReloadAsync();
      this.ShowMessage(this.Loc["Vehicle saved."], false);
    });
  }

  private async Task DeleteSelectedVehicleAsync()
  {
    if (this.isReadOnly || !this.selectedVehicleId.HasValue)
    {
      return;
    }

    await this.RunSavingAsync(async () =>
    {
      await this.VehicleService.DeleteVehicleAsync(this.selectedVehicleId.Value, this.userId);
      this.selectedVehicleId = null;
      await this.ReloadAsync();
      this.ShowMessage(this.Loc["Vehicle deleted."], false);
    });
  }

  private void CloseVehicleModal()
  {
    this.showVehicleModal = false;
  }

  private async Task OnVehicleImageSelectedAsync(InputFileChangeEventArgs args)
  {
    IBrowserFile file = args.File;
    if (file.Size > MaxVehicleImageBytes)
    {
      this.ShowMessage(this.Loc["Vehicle image is too large. Max 5 MB."], true);
      return;
    }

    string contentType = ResolveImageContentType(file);
    if (string.IsNullOrWhiteSpace(contentType))
    {
      this.ShowMessage(this.Loc["Vehicle image must be an image file."], true);
      return;
    }

    try
    {
      await using Stream stream = file.OpenReadStream(MaxVehicleImageBytes);
      using MemoryStream memory = new();
      await stream.CopyToAsync(memory);
      this.editingVehicle.ImageDataUrl = $"data:{contentType};base64,{Convert.ToBase64String(memory.ToArray())}";
      this.ShowMessage(this.Loc["Vehicle image ready."], false);
    }
    catch
    {
      this.ShowMessage(this.Loc["Vehicle image could not be read."], true);
    }
  }

  private void RemoveVehicleImage()
  {
    this.editingVehicle.ImageDataUrl = null;
  }

  private void NewRecord()
  {
    if (this.isReadOnly || !this.selectedVehicleId.HasValue)
    {
      return;
    }

    this.recordDateInput = DateTime.Today;
    this.editingRecord = new VehicleRecordEntity
    {
      VehicleId = this.selectedVehicleId.Value,
      RecordType = VehicleRecordType.Maintenance,
      RecordDate = DateOnly.FromDateTime(this.recordDateInput),
      Title = this.Loc["Maintenance"],
      Odometer = this.SelectedVehicle?.CurrentOdometer,
    };
    this.selectedServiceItems.Clear();
    this.customServiceItem = string.Empty;
    this.showRecordModal = true;
  }

  private void ToggleServiceItem(string item)
  {
    if (!this.selectedServiceItems.Add(item))
    {
      this.selectedServiceItems.Remove(item);
    }

    this.SyncRecordFromServiceItems();
  }

  private void SyncRecordFromServiceItems()
  {
    List<string> items = this.BuildSelectedServiceItems();
    this.editingRecord.ServiceItems = string.Join("; ", items);
    this.editingRecord.OilChanged = items.Any(item => item.Equals("Oil change", StringComparison.OrdinalIgnoreCase));
    this.editingRecord.RecordType = this.ResolveRecordType(items);
    if (items.Count > 0)
    {
      this.editingRecord.Title = string.Join(" + ", items.Take(3));
    }
  }

  private void EditRecord(VehicleRecordEntity record)
  {
    if (this.isReadOnly)
    {
      return;
    }

    this.recordDateInput = record.RecordDate.ToDateTime(TimeOnly.MinValue);
    this.editingRecord = CloneRecord(record);
    this.selectedServiceItems = ParseServiceItems(record.ServiceItems).ToHashSet(StringComparer.OrdinalIgnoreCase);
    this.customServiceItem = string.Empty;
    this.showRecordModal = true;
  }

  private async Task SaveRecordAsync()
  {
    if (this.isReadOnly || !this.selectedVehicleId.HasValue)
    {
      return;
    }

    await this.RunSavingAsync(async () =>
    {
      this.editingRecord.VehicleId = this.selectedVehicleId.Value;
      this.editingRecord.RecordDate = DateOnly.FromDateTime(this.recordDateInput);
      this.SyncRecordFromServiceItems();
      await this.VehicleService.SaveRecordAsync(this.editingRecord, this.userId);
      this.showRecordModal = false;
      await this.ReloadAsync();
      this.ShowMessage(this.Loc["Record saved."], false);
    });
  }

  private async Task DeleteRecordAsync(int recordId)
  {
    if (this.isReadOnly)
    {
      return;
    }

    await this.RunSavingAsync(async () =>
    {
      await this.VehicleService.DeleteRecordAsync(recordId, this.userId);
      await this.ReloadAsync();
      this.ShowMessage(this.Loc["Record deleted."], false);
    });
  }

  private void CloseRecordModal()
  {
    this.showRecordModal = false;
  }

  private void NewReminder()
  {
    if (this.isReadOnly || !this.selectedVehicleId.HasValue)
    {
      return;
    }

    this.reminderDueDateInput = DateTime.Today.AddMonths(1);
    this.editingReminder = new VehicleReminderEntity
    {
      VehicleId = this.selectedVehicleId.Value,
      Status = VehicleReminderStatus.Open,
      Title = this.Loc["Maintenance reminder"],
      DueDate = DateOnly.FromDateTime(this.reminderDueDateInput.Value),
    };
    this.showReminderModal = true;
  }

  private void EditReminder(VehicleReminderEntity reminder)
  {
    if (this.isReadOnly)
    {
      return;
    }

    this.reminderDueDateInput = reminder.DueDate?.ToDateTime(TimeOnly.MinValue);
    this.editingReminder = CloneReminder(reminder);
    this.showReminderModal = true;
  }

  private async Task SaveReminderAsync()
  {
    if (this.isReadOnly || !this.selectedVehicleId.HasValue)
    {
      return;
    }

    await this.RunSavingAsync(async () =>
    {
      this.editingReminder.VehicleId = this.selectedVehicleId.Value;
      this.editingReminder.DueDate = this.reminderDueDateInput.HasValue
        ? DateOnly.FromDateTime(this.reminderDueDateInput.Value)
        : null;
      await this.VehicleService.SaveReminderAsync(this.editingReminder, this.userId);
      this.showReminderModal = false;
      await this.ReloadAsync();
      this.ShowMessage(this.Loc["Reminder saved."], false);
    });
  }

  private async Task CompleteReminderAsync(int reminderId)
  {
    await this.UpdateReminderStatusAsync(reminderId, VehicleReminderStatus.Done);
  }

  private async Task DismissReminderAsync(int reminderId)
  {
    await this.UpdateReminderStatusAsync(reminderId, VehicleReminderStatus.Dismissed);
  }

  private async Task UpdateReminderStatusAsync(int reminderId, VehicleReminderStatus status)
  {
    if (this.isReadOnly)
    {
      return;
    }

    await this.RunSavingAsync(async () =>
    {
      await this.VehicleService.UpdateReminderStatusAsync(reminderId, status, this.userId);
      await this.ReloadAsync();
      this.ShowMessage(this.Loc["Reminder updated."], false);
    });
  }

  private async Task DeleteReminderAsync(int reminderId)
  {
    if (this.isReadOnly)
    {
      return;
    }

    await this.RunSavingAsync(async () =>
    {
      await this.VehicleService.DeleteReminderAsync(reminderId, this.userId);
      await this.ReloadAsync();
      this.ShowMessage(this.Loc["Reminder deleted."], false);
    });
  }

  private void CloseReminderModal()
  {
    this.showReminderModal = false;
  }

  private async Task RunSavingAsync(Func<Task> action)
  {
    this.isSaving = true;
    this.message = string.Empty;
    try
    {
      await action();
    }
    catch (Exception exception)
    {
      this.ShowMessage(this.Loc[exception.Message], true);
    }
    finally
    {
      this.isSaving = false;
    }
  }

  private void ShowMessage(string text, bool error)
  {
    this.message = text;
    this.hasError = error;
  }

  private string BuildVehicleSubtitle(VehicleEntity vehicle)
  {
    string details = string.Join(" ", new[] { vehicle.Brand, vehicle.Model }.Where(item => !string.IsNullOrWhiteSpace(item)));
    if (vehicle.Year.HasValue)
    {
      details = string.IsNullOrWhiteSpace(details) ? vehicle.Year.Value.ToString(this.Loc.Culture) : $"{details} · {vehicle.Year.Value}";
    }

    return string.IsNullOrWhiteSpace(details) ? this.Loc[vehicle.VehicleType.ToString()] : details;
  }

  private string BuildRecordMeta(VehicleRecordEntity record)
  {
    List<string> parts = new();
    if (record.Odometer.HasValue)
    {
      parts.Add(this.FormatOdometer(record.Odometer.Value));
    }

    if (record.Amount.HasValue)
    {
      parts.Add(this.FormatMoney(record.Amount.Value));
    }

    if (record.Quantity.HasValue)
    {
      parts.Add(this.Loc.Format("Vehicle quantity format", record.Quantity.Value));
    }

    if (!string.IsNullOrWhiteSpace(record.Category))
    {
      parts.Add(record.Category);
    }

    if (!string.IsNullOrWhiteSpace(record.ServiceItems))
    {
      parts.Add(record.ServiceItems);
    }

    if (!string.IsNullOrWhiteSpace(record.Location))
    {
      parts.Add(record.Location);
    }

    return parts.Count == 0 ? this.Loc["No extra details"] : string.Join(" · ", parts);
  }

  private string BuildReminderDueText(VehicleReminderView reminder)
  {
    List<string> parts = new();
    if (reminder.Reminder.DueDate.HasValue)
    {
      parts.Add(reminder.Reminder.DueDate.Value.ToString("d", this.Loc.Culture));
    }

    if (reminder.Reminder.DueOdometer.HasValue)
    {
      parts.Add(this.FormatOdometer(reminder.Reminder.DueOdometer.Value));
    }

    string dueText = parts.Count == 0 ? this.Loc["No due target"] : string.Join(" · ", parts);
    return $"{this.Loc[reminder.DueState.ToString()]} · {dueText}";
  }

  private string FormatOdometer(decimal value)
  {
    return this.Loc.Format("Vehicle km format", value);
  }

  private string FormatMoney(decimal value)
  {
    // Vehicle costs are as sensitive as any other amount, so they follow the same toggle.
    return this.ValuesVis.Mask(Helper.FormatBrl(value));
  }

  /// <inheritdoc />
  public void Dispose()
  {
    this.ValuesVis.OnChange -= this.OnValuesVisibilityChanged;
  }

  private void OnValuesVisibilityChanged() => this.InvokeAsync(this.StateHasChanged);

  private static string GetDueClass(VehicleDueState? dueState)
  {
    return dueState switch
    {
      VehicleDueState.Overdue => "overdue",
      VehicleDueState.Soon => "soon",
      VehicleDueState.Resolved => "resolved",
      _ => "current",
    };
  }

  private static string GetRecordClass(VehicleRecordType recordType)
  {
    return recordType switch
    {
      VehicleRecordType.OilChange => "oil",
      VehicleRecordType.Fuel => "fuel",
      VehicleRecordType.Expense => "expense",
      VehicleRecordType.Odometer => "odometer",
      _ => "maintenance",
    };
  }

  private static string GetRecordIcon(VehicleRecordType recordType)
  {
    return recordType switch
    {
      VehicleRecordType.OilChange => "bi bi-droplet-half",
      VehicleRecordType.Fuel => "bi bi-fuel-pump",
      VehicleRecordType.Expense => "bi bi-receipt",
      VehicleRecordType.Odometer => "bi bi-speedometer2",
      _ => "bi bi-tools",
    };
  }

  private static string GetServiceIcon(string item)
  {
    return item switch
    {
      "Oil change" => "bi bi-droplet-half",
      "Fuel" => "bi bi-fuel-pump",
      "Expense" => "bi bi-receipt",
      "Odometer" => "bi bi-speedometer2",
      "Tires" or "Rear tire" or "Front tire" or "Balancing" or "Alignment" => "bi bi-circle",
      "Brakes" or "Brake pads" or "Brake fluid" => "bi bi-record-circle",
      "Battery" => "bi bi-battery-half",
      "Chain" or "Sprocket" => "bi bi-link-45deg",
      "Insurance" => "bi bi-shield-check",
      "Tax" => "bi bi-receipt",
      "Parking" => "bi bi-p-square",
      "Wash" => "bi bi-stars",
      _ => "bi bi-tools",
    };
  }

  private static string ResolveImageContentType(IBrowserFile file)
  {
    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
      || file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
      || file.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
    {
      return file.ContentType;
    }

    return Path.GetExtension(file.Name).ToLowerInvariant() switch
    {
      ".jpg" or ".jpeg" => "image/jpeg",
      ".png" => "image/png",
      ".webp" => "image/webp",
      _ => string.Empty,
    };
  }

  private VehicleRecordType ResolveRecordType(List<string> items)
  {
    if (items.Count == 0)
    {
      return this.editingRecord.RecordType;
    }

    if (items.Any(item => item.Equals("Fuel", StringComparison.OrdinalIgnoreCase)))
    {
      return VehicleRecordType.Fuel;
    }

    if (items.Any(item => item.Equals("Expense", StringComparison.OrdinalIgnoreCase)))
    {
      return VehicleRecordType.Expense;
    }

    if (items.Any(item => item.Equals("Odometer", StringComparison.OrdinalIgnoreCase)))
    {
      return VehicleRecordType.Odometer;
    }

    return this.editingRecord.OilChanged || items.Any(item => item.Equals("Oil change", StringComparison.OrdinalIgnoreCase))
      ? VehicleRecordType.OilChange
      : VehicleRecordType.Maintenance;
  }

  private List<string> BuildSelectedServiceItems()
  {
    List<string> items = this.selectedServiceItems.ToList();
    if (!string.IsNullOrWhiteSpace(this.customServiceItem))
    {
      items.Add(this.customServiceItem.Trim());
    }

    return items.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  }

  private IEnumerable<string> VehicleSpecificServiceItems
  {
    get
    {
      return this.SelectedVehicle?.Vehicle.VehicleType == VehicleType.Motorcycle
        ? MotorcycleServiceItems
        : CarServiceItems;
    }
  }

  private static IEnumerable<string> ParseServiceItems(string? value)
  {
    return string.IsNullOrWhiteSpace(value)
      ? Enumerable.Empty<string>()
      : value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
  }

  private static VehicleEntity CloneVehicle(VehicleEntity vehicle)
  {
    return new VehicleEntity
    {
      Id = vehicle.Id,
      VehicleType = vehicle.VehicleType,
      Name = vehicle.Name,
      Brand = vehicle.Brand,
      Model = vehicle.Model,
      Year = vehicle.Year,
      Plate = vehicle.Plate,
      CurrentOdometer = vehicle.CurrentOdometer,
      OdometerUnit = vehicle.OdometerUnit,
      IconCss = vehicle.IconCss,
      ColorHex = vehicle.ColorHex,
      ImageDataUrl = vehicle.ImageDataUrl,
      Notes = vehicle.Notes,
      Active = vehicle.Active,
    };
  }

  private static VehicleRecordEntity CloneRecord(VehicleRecordEntity record)
  {
    return new VehicleRecordEntity
    {
      Id = record.Id,
      VehicleId = record.VehicleId,
      RecordType = record.RecordType,
      RecordDate = record.RecordDate,
      Odometer = record.Odometer,
      Title = record.Title,
      Category = record.Category,
      Amount = record.Amount,
      Quantity = record.Quantity,
      OilChanged = record.OilChanged,
      ServiceItems = record.ServiceItems,
      Location = record.Location,
      Notes = record.Notes,
    };
  }

  private static VehicleReminderEntity CloneReminder(VehicleReminderEntity reminder)
  {
    return new VehicleReminderEntity
    {
      Id = reminder.Id,
      VehicleId = reminder.VehicleId,
      Title = reminder.Title,
      Category = reminder.Category,
      DueDate = reminder.DueDate,
      DueOdometer = reminder.DueOdometer,
      Status = reminder.Status,
      Notes = reminder.Notes,
      CompletedByRecordId = reminder.CompletedByRecordId,
    };
  }
}
