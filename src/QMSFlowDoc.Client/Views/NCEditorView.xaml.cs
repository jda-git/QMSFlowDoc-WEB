using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System.Collections.Generic;
using System;
using System.Linq;

namespace QMSFlowDoc.Client.Views;

public sealed partial class NCEditorView : Page
{
    private Guid? _ncId;

    public NCEditorView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            _ncId = id;
            await LoadNC(id);
        }
        else if (e.Parameter is NCEditorParameter param)
        {
            if (param.IncidentId.HasValue)
            {
                _ncId = param.IncidentId.Value;
                await LoadNC(_ncId.Value);
            }
            else
            {
                TitleBox.Text = param.DefaultTitle ?? "";
                DescriptionBox.Text = param.DefaultDescription ?? "";
                
                // Try to set Origin
                if (!string.IsNullOrEmpty(param.DefaultOrigin))
                {
                    OriginCombo.Text = param.DefaultOrigin;
                }
            }
        }
    }
    

    private System.Collections.ObjectModel.ObservableCollection<CapaAction> Actions { get; set; } = new();

    private async System.Threading.Tasks.Task LoadNC(Guid id)
    {
        try
        {
            var service = ((App)Application.Current).QualityService;
            var nc = await service.GetNCByIdAsync(id);
            if (nc != null)
            {
                TitleBox.Text = nc.Title;
                DescriptionBox.Text = nc.Description;
                ContainmentBox.Text = nc.Containment;
                RcaBox.Text = nc.RootCauseAnalysis;
                OriginCombo.Text = nc.Origin; // Simple text binding for editable combo
                ImpactPatientCheck.IsChecked = nc.ImpactPatient;

                // Load Actions
                Actions.Clear();
                if (nc.Actions != null)
                {
                    foreach (var action in nc.Actions) Actions.Add(action);
                }
                ActionsList.ItemsSource = Actions; // Bind manually or via property

                // Select Severity
                foreach (ComboBoxItem item in SeverityCombo.Items)
                {
                    if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tagVal) && tagVal == (int)nc.Severity)
                    {
                        SeverityCombo.SelectedItem = item;
                        break;
                    }
                }

                // Select Status
                foreach (ComboBoxItem item in StatusCombo.Items)
                {
                    if (item.Content.ToString() == nc.Status.ToString())
                    {
                        StatusCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
             ErrorText.Text = $"Error loading NC: {ex.Message}";
             ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            ErrorText.Text = "El título es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var severity = (NCSeverity)(int.TryParse((SeverityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out int sev) ? sev : 1);
            var statusStr = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            NCStatus? status = null;
            if (Enum.TryParse<NCStatus>(statusStr, out var sEnum)) status = sEnum;

            var request = new CreateNCRequest(
                TitleBox.Text,
                DescriptionBox.Text,
                severity,
                status,
                ImpactPatientCheck.IsChecked ?? false,
                ContainmentBox.Text,
                null,
                OriginCombo.Text, // ISO 15189
                RcaBox.Text,  // ISO 15189
                ((App)Application.Current).AuthService.CurrentUserId // DetectedByUserId
            );

            var service = ((App)Application.Current).QualityService;

            if (_ncId.HasValue)
            {
                // Update
                var success = await service.UpdateNCAsync(_ncId.Value, request);
                
                 // Update Status separate patch if needed, or included in PUT. 
                 // Assuming PUT handles all fields now based on CreateNCRequest usage in controller.
                 // Double check controller UpdateNC logic - it updates specific fields.
                 
                 // Update Status specifically via PATCH if key logic depends on it, but UpdateNC also sets it potentially?
                 // Let's stick to existing pattern: Update fields, then Update Status status.
                if (Enum.TryParse<NCStatus>(statusStr, out var statusEnum))
                {
                    await service.UpdateNCStatusAsync(_ncId.Value, (int)statusEnum);
                }

                if (success) Frame.GoBack();
                else { ErrorText.Text = "Error al actualizar."; ErrorText.Visibility = Visibility.Visible; }
            }
            else
            {
                // Create
                var result = await service.CreateNCAsync(request);
                if (result != null)
                {
                    Frame.GoBack();
                }
                else
                {
                    ErrorText.Text = "Error al enviar el reporte.";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void AddAction_Click(object sender, RoutedEventArgs e)
    {
        if (!_ncId.HasValue)
        {
            await new ContentDialog 
            { 
                Title = "Guardar Primero", 
                Content = "Por favor, guarde la No Conformidad antes de añadir acciones.", 
                CloseButtonText = "OK", 
                XamlRoot = this.XamlRoot 
            }.ShowAsync();
            return;
        }

        var staffService = ((App)Application.Current).StaffService;
        var users = (await staffService.GetStaffAsync()) // Fixed method name
                        .Select(s => new User { Id = s.UserId ?? Guid.Empty, FullName = s.FullName })
                        .ToList();

        var dialog = new Dialogs.AddCapaDialog(users ?? new System.Collections.Generic.List<User>()); // Explicit or add using
        dialog.XamlRoot = this.XamlRoot;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try 
            {
                var req = new CreateCAPARequest(
                    _ncId.Value,
                    dialog.ActionType,
                    dialog.ActionDescription,
                    dialog.OwnerUserId,
                    dialog.DueDate
                );

                var service = ((App)Application.Current).QualityService;
                var action = await service.CreateCAPAAsync(req);

                if (action != null)
                {
                    Actions.Add(action);
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Error creating CAPA: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}

public class NCEditorParameter
{
    public Guid? IncidentId { get; set; }
    public string? DefaultTitle { get; set; }
    public string? DefaultDescription { get; set; }
    public string? DefaultOrigin { get; set; }
}
