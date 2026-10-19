//-----------------------------------------------------------------------
// <copyright file="LocalizedDataAnnotationsValidator.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

using System.ComponentModel.DataAnnotations;
using AppControlOrganizer.V2.Web.Services.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Validates edit forms with data annotations and localizes validation messages through app resources.
/// </summary>
public sealed class LocalizedDataAnnotationsValidator : ComponentBase, IDisposable
{
  private EditContext? previousEditContext;
  private ValidationMessageStore? messageStore;

  /// <summary>
  /// Gets or sets the current edit context.
  /// </summary>
  [CascadingParameter]
  private EditContext? CurrentEditContext { get; set; }

  /// <summary>
  /// Gets or sets the localization facade.
  /// </summary>
  [Inject]
  private LocService Loc { get; set; } = null!;

  /// <inheritdoc />
  protected override void OnInitialized()
  {
    if (this.CurrentEditContext == null)
    {
      throw new InvalidOperationException($"{nameof(LocalizedDataAnnotationsValidator)} requires a cascading EditContext.");
    }

    this.previousEditContext = this.CurrentEditContext;
    this.messageStore = new ValidationMessageStore(this.CurrentEditContext);
    this.CurrentEditContext.OnValidationRequested += this.OnValidationRequested;
    this.CurrentEditContext.OnFieldChanged += this.OnFieldChanged;
  }

  /// <inheritdoc />
  protected override void OnParametersSet()
  {
    if (this.CurrentEditContext != this.previousEditContext)
    {
      throw new InvalidOperationException($"{nameof(LocalizedDataAnnotationsValidator)} does not support changing EditContext dynamically.");
    }
  }

  /// <inheritdoc />
  public void Dispose()
  {
    if (this.previousEditContext != null)
    {
      this.previousEditContext.OnValidationRequested -= this.OnValidationRequested;
      this.previousEditContext.OnFieldChanged -= this.OnFieldChanged;
    }
  }

  private void OnValidationRequested(object? sender, ValidationRequestedEventArgs eventArgs)
  {
    if (this.CurrentEditContext == null || this.messageStore == null)
    {
      return;
    }

    this.messageStore.Clear();
    ValidationContext validationContext = new ValidationContext(this.CurrentEditContext.Model);
    List<ValidationResult> validationResults = [];

    Validator.TryValidateObject(
      this.CurrentEditContext.Model,
      validationContext,
      validationResults,
      validateAllProperties: true);

    foreach (ValidationResult validationResult in validationResults)
    {
      string message = this.Loc[validationResult.ErrorMessage ?? string.Empty];
      IEnumerable<string> memberNames = validationResult.MemberNames.Any()
        ? validationResult.MemberNames
        : new[] { string.Empty };

      foreach (string memberName in memberNames)
      {
        this.messageStore.Add(this.CurrentEditContext.Field(memberName), message);
      }
    }

    this.CurrentEditContext.NotifyValidationStateChanged();
  }

  private void OnFieldChanged(object? sender, FieldChangedEventArgs eventArgs)
  {
    if (this.CurrentEditContext == null || this.messageStore == null)
    {
      return;
    }

    this.messageStore.Clear(eventArgs.FieldIdentifier);
    ValidationContext validationContext = new ValidationContext(eventArgs.FieldIdentifier.Model)
    {
      MemberName = eventArgs.FieldIdentifier.FieldName
    };
    List<ValidationResult> validationResults = [];
    object? value = eventArgs.FieldIdentifier.Model
      .GetType()
      .GetProperty(eventArgs.FieldIdentifier.FieldName)
      ?.GetValue(eventArgs.FieldIdentifier.Model);

    Validator.TryValidateProperty(value, validationContext, validationResults);

    foreach (ValidationResult validationResult in validationResults)
    {
      this.messageStore.Add(eventArgs.FieldIdentifier, this.Loc[validationResult.ErrorMessage ?? string.Empty]);
    }

    this.CurrentEditContext.NotifyValidationStateChanged();
  }
}
