using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ComplaintEditorView : Page
{
    private Guid? _complaintId;
    private Complaint? _currentComplaint;

    public ComplaintEditorView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            _complaintId = id;
            await LoadComplaint(id);
        }
        else
        {
            _complaintId = null;
            _currentComplaint = new Complaint();
            MainPivot.SelectedIndex = 0; // Start at reception
        }
    }

    private async Task LoadComplaint(Guid id)
    {
        try
        {
            var store = ((App)Application.Current).LocalStore;
            _currentComplaint = await store.GetComplaintByIdAsync(id);

            if (_currentComplaint != null)
            {
                // Phase 1
                SourceBox.Text = _currentComplaint.Source;
                DescriptionBox.Text = _currentComplaint.Description;
                CategoryCombo.SelectedIndex = (int)_currentComplaint.Category;
                
                ClaimantTypeCombo.SelectedIndex = (int)_currentComplaint.ClaimantType;
                SubstantiatedCheck.IsChecked = _currentComplaint.IsSubstantiated;
                ReceiptDatePicker.Date = _currentComplaint.ReceiptDate.HasValue ? new DateTimeOffset(_currentComplaint.ReceiptDate.Value) : null;
                ReceiptMethodBox.Text = _currentComplaint.ReceiptMethod ?? "";

                // Phase 2
                ImpactCombo.SelectedIndex = (int)_currentComplaint.ClinicalImpact;
                InvestigationBox.Text = _currentComplaint.InvestigationResult ?? "";
                
                if (_currentComplaint.RelatedNCId.HasValue)
                {
                    RelatedNCText.Text = $"NC Vinculada: {_currentComplaint.RelatedNCId}";
                    LinkNCButton.IsEnabled = false; // Already linked
                }

                // Phase 3
                ActionsList.ItemsSource = _currentComplaint.Actions;

                // Phase 4
                ResolutionEvidenceBox.Text = _currentComplaint.ResolutionEvidence ?? "";
                EffectivenessDatePicker.Date = _currentComplaint.EffectivenessDate.HasValue ? new DateTimeOffset(_currentComplaint.EffectivenessDate.Value) : null;
                EffectivenessNotesBox.Text = _currentComplaint.EffectivenessNotes ?? "";

                UpdateStatusBadge();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error cargando queja: {ex.Message}");
        }
    }

    private void UpdateStatusBadge()
    {
        if (_currentComplaint != null)
        {
            StatusBadge.Text = $"ESTADO: {_currentComplaint.Status}";
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (_currentComplaint == null) return;

        // Validations
        if (string.IsNullOrWhiteSpace(SourceBox.Text) || string.IsNullOrWhiteSpace(DescriptionBox.Text))
        {
            ShowError("Origen y Descripción son obligatorios.");
            return;
        }

        // Map UI to Model
        _currentComplaint.Source = SourceBox.Text;
        _currentComplaint.Description = DescriptionBox.Text;
        _currentComplaint.Category = (ComplaintCategory)CategoryCombo.SelectedIndex;
        _currentComplaint.ClaimantType = (ClaimantType)ClaimantTypeCombo.SelectedIndex;
        
        _currentComplaint.IsSubstantiated = SubstantiatedCheck.IsChecked ?? false;
        if (ReceiptDatePicker.Date.HasValue) _currentComplaint.ReceiptDate = ReceiptDatePicker.Date?.DateTime;
        _currentComplaint.ReceiptMethod = ReceiptMethodBox.Text;

        _currentComplaint.ClinicalImpact = (ClinicalImpact)ImpactCombo.SelectedIndex;
        _currentComplaint.InvestigationResult = InvestigationBox.Text;

        _currentComplaint.ResolutionEvidence = ResolutionEvidenceBox.Text;
        if (EffectivenessDatePicker.Date.HasValue) _currentComplaint.EffectivenessDate = EffectivenessDatePicker.Date?.DateTime;
        _currentComplaint.EffectivenessNotes = EffectivenessNotesBox.Text;

        // Auto-advance status logic based on completeness (Simple state machine)
        if (_currentComplaint.Status == ComplaintStatus.OPEN)
        {
            if (_currentComplaint.IsSubstantiated && !string.IsNullOrEmpty(_currentComplaint.ReceiptMethod))
            {
                _currentComplaint.Status = ComplaintStatus.VALIDATED;
            }
        }
        else if (_currentComplaint.Status == ComplaintStatus.VALIDATED)
        {
             if (!string.IsNullOrEmpty(_currentComplaint.InvestigationResult))
             {
                 _currentComplaint.Status = ComplaintStatus.INVESTIGATING;
             }
        }
        // ... more logic can be added

        try
        {
            var store = ((App)Application.Current).LocalStore;
            
            if (_complaintId.HasValue)
            {
                await store.UpdateComplaintAsync(_currentComplaint);
            }
            else
            {
                var req = new CreateComplaintRequest(
                    _currentComplaint.Source,
                    _currentComplaint.Description,
                    _currentComplaint.Category,
                    _currentComplaint.InvestigationResult,
                    null, // CorrectiveAction handled via Actions list now
                    _currentComplaint.ResolutionEvidence
                );
                
                var created = await store.CreateComplaintAsync(req);
                _complaintId = created.Id;
                _currentComplaint = created;
                
                // Need to update extended fields that CreateRequest doesn't cover yet
                // Or update CreateComplaintAsync to take full object. 
                // For now, doing a second update to save the extended fields.
                
                // Copy extended fields back to created object
                created.ClaimantType = (ClaimantType)ClaimantTypeCombo.SelectedIndex;
                created.IsSubstantiated = SubstantiatedCheck.IsChecked ?? false;
                created.ReceiptDate = ReceiptDatePicker.Date?.DateTime;
                created.ReceiptMethod = ReceiptMethodBox.Text;
                created.ClinicalImpact = (ClinicalImpact)ImpactCombo.SelectedIndex;
                
                await store.UpdateComplaintAsync(created);
            }

            Frame.GoBack();
        }
        catch (Exception ex)
        {
            ShowError($"Error guardando: {ex.Message}");
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        // Implement delete confirmation
    }
    
    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    private async void AddAction_Click(object sender, RoutedEventArgs e)
    {
        if (!_complaintId.HasValue)
        {
            ShowError("Guarde la queja antes de añadir acciones.");
            return;
        }

        // Simple input dialog for now, or a proper Action Dialog
        // For MVP/Space, I'll use a ContentDialog with basic fields
        
        var stack = new StackPanel { Spacing = 10 };
        var typeCombo = new ComboBox { Header = "Tipo", ItemsSource = Enum.GetNames(typeof(ComplaintActionType)), SelectedIndex = 0 };
        var descBox = new TextBox { Header = "Descripción" };
        stack.Children.Add(typeCombo);
        stack.Children.Add(descBox);

        var dialog = new ContentDialog
        {
            Title = "Nueva Acción",
            Content = stack,
            PrimaryButtonText = "Añadir",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var action = new ComplaintAction
            {
                Id = Guid.NewGuid(),
                ComplaintId = _complaintId.Value,
                ActionType = (ComplaintActionType)typeCombo.SelectedIndex,
                Description = descBox.Text,
                Status = ActionStatus.PENDING,
                DueDate = DateTime.UtcNow.AddDays(7) // Default
            };

            var store = ((App)Application.Current).LocalStore;
            await store.AddComplaintActionAsync(action);
            await LoadComplaint(_complaintId.Value); // Reload to refresh list
        }
    }

    private async void LinkNC_Click(object sender, RoutedEventArgs e)
    {
         if (!_complaintId.HasValue) return;
         
         // Create a new NC with data from this complaint
         var ncParam = new NCEditorParameter
         {
             DefaultTitle = $"Queja: {_currentComplaint?.Source}",
             DefaultDescription = _currentComplaint?.Description,
             DefaultOrigin = "Queja Cliente"
         };
         
         // Navigate to NC Editor
         // Issue: How to get back the NC ID?
         // Solution: We can create the NC here via service first, then navigate to edit it.
         
         var dialog = new ContentDialog
         {
             Title = "Crear No Conformidad",
             Content = "¿Desea generar una nueva NC a partir de esta queja? Se vinculará automáticamente.",
             PrimaryButtonText = "Sí, Crear",
             CloseButtonText = "Cancelar",
             XamlRoot = this.XamlRoot
         };

         if (await dialog.ShowAsync() == ContentDialogResult.Primary)
         {
             try
             {
                 var req = new CreateNCRequest(
                     $"Queja: {_currentComplaint?.Source}",
                     _currentComplaint?.Description ?? "",
                     NCSeverity.HIGH, // Default high if coming from complaint?
                     NCStatus.OPEN,
                     true, // Impact patient likely if it's a complaint
                     "",
                     "Queja Externa",
                     "",
                     null
                 );
                 
                 var service = ((App)Application.Current).QualityService;
                 var nc = await service.CreateNCAsync(req);
                 
                 if (nc != null)
                 {
                     _currentComplaint.RelatedNCId = nc.Id;
                     _currentComplaint.ClinicalImpact = ClinicalImpact.HIGH; // Force High/Crit logic
                     await ((App)Application.Current).LocalStore.UpdateComplaintAsync(_currentComplaint);
                     
                     LinkNCButton.IsEnabled = false;
                     RelatedNCText.Text = $"NC Vinculada: {nc.Id}";
                     
                     // Navigate to NC Editor
                     Frame.Navigate(typeof(NCEditorView), nc.Id);
                 }
             }
             catch (Exception ex)
             {
                 ShowError($"Error creando NC: {ex.Message}");
             }
         }
    }

    private async void CloseComplaint_Click(object sender, RoutedEventArgs e)
    {
        // Validate closure
        if (string.IsNullOrWhiteSpace(ResolutionEvidenceBox.Text))
        {
             ShowError("Debe adjuntar evidencia de resolución (comunicación al cliente).");
             return;
        }
        
        if (!EffectivenessDatePicker.Date.HasValue)
        {
             ShowError("Debe especificar una fecha para verificación de eficacia.");
             return;
        }

        // Logic: If Actions pending, cannot close?
        if (_currentComplaint.Actions.Any(a => a.Status != ActionStatus.VERIFIED))
        {
             var confirm = new ContentDialog
             {
                 Title = "Acciones Pendientes",
                 Content = "Hay acciones sin verificar. ¿Seguro que desea cerrar la queja? ISO 15189 recomienda cerrar solo tras verificar eficacia.",
                 PrimaryButtonText = "Cerrar de todas formas",
                 CloseButtonText = "Cancelar",
                 XamlRoot = this.XamlRoot
             };
             
             if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        _currentComplaint.Status = ComplaintStatus.CLOSED;
        _currentComplaint.ClosedAt = DateTime.UtcNow;
        _currentComplaint.ResolutionEvidence = ResolutionEvidenceBox.Text;
        _currentComplaint.EffectivenessDate = EffectivenessDatePicker.Date?.DateTime;
        _currentComplaint.EffectivenessNotes = EffectivenessNotesBox.Text;
        
        await ((App)Application.Current).LocalStore.UpdateComplaintAsync(_currentComplaint);
        Frame.GoBack();
    }
}
