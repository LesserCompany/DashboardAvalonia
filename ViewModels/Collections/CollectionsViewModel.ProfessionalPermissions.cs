using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.Helpers;
using LesserDashboardClient.Models;
using LesserDashboardClient.Views;
using LesserDashboardClient.Views.Collections;
using Newtonsoft.Json;
using SharedClientSide.ServerInteraction.Users;
using SharedClientSide.ServerInteraction.Users.Professionals;

namespace LesserDashboardClient.ViewModels.Collections;

// Profissionais: tela "Gerenciar profissionais", prefixo do login no cadastro e permissões (RBAC) no cadastro e na
// edição. A grade de permissões é ProfessionalPermissionsEditorViewModel (componente ProfessionalPermissionsEditor).
public partial class CollectionsViewModel
{
    // ---- Gerenciar profissionais ----

    // Tela de profissionais aberta pelo botão "Gerenciar profissionais" da lista lateral, só para gerenciar.
    // A mesma tela aberta pelo "Separador:" da coleção é um seletor: clicar num profissional troca o separador da
    // coleção selecionada. Neste modo o clique não atribui nada (ver OnSelectedProfessionalChanged).
    [ObservableProperty] private bool isManagingProfessionals;

    [RelayCommand]
    private async Task OpenManageProfessionals()
    {
        IsManagingProfessionals = true;
        ActiveComponent = ActiveViews.SelectProfessional;
        if (!_professionalsLoaded)
        {
            LoadProfessionalsIsRunning = true;
            await Task.Yield();
        }
        await LoadProfessionalsAsync();
    }

    // ---- Cadastro: prefixo do login ----

    // Prefixo do login = nome da company + "@" (Helpers/ProfessionalUsernamePrefix). Vazio até a company carregar.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewProfessionalFinalUsername), nameof(HasNewProfessionalUsernamePrefix))]
    private string newProfessionalUsernamePrefix = string.Empty;

    public bool HasNewProfessionalUsernamePrefix => !string.IsNullOrEmpty(NewProfessionalUsernamePrefix);

    /// <summary>Login que será criado (prefixo + digitado), mostrado embaixo do campo.</summary>
    public string NewProfessionalFinalUsername => ProfessionalUsernamePrefix.Build(NewProfessionalUsernamePrefix, TbNewProfessionalUsername);

    partial void OnTbNewProfessionalUsernameChanged(string value) => OnPropertyChanged(nameof(NewProfessionalFinalUsername));

    private async Task LoadNewProfessionalUsernamePrefixAsync()
    {
        try
        {
            var r = await GlobalAppStateViewModel.lfc.GetCompanyDetails();
            var prefix = r?.success == true ? ProfessionalUsernamePrefix.FromCompany(r.Content?.company) : string.Empty;
            await Dispatcher.UIThread.InvokeAsync(() => NewProfessionalUsernamePrefix = prefix);
        }
        catch
        {
            // Sem prefixo na tela; CreateNewProfessionalCommand tenta de novo com os dados da empresa.
        }
    }

    // ---- Cadastro: permissões iniciais ----

    // Catálogo + caixas do cadastro; o que for marcado vai junto no RegisterProfessional_v2.
    [ObservableProperty] private ProfessionalPermissionsEditorViewModel newProfessionalPermissions = new();

    private void ResetNewProfessionalPermissions()
    {
        NewProfessionalPermissions = new ProfessionalPermissionsEditorViewModel();
        _ = NewProfessionalPermissions.LoadAsync(current: null);
    }

    // null quando o catálogo não carregou: o cadastro segue sem permissões (e funciona também no legado).
    private UserAuthorizationConfig? BuildNewProfessionalAuthorization() => NewProfessionalPermissions.Build();

    // ---- Editar profissional existente (apelido, e-mail, senha, permissões) ----
    // Tudo num gravar só: UpdateCompanyProfessional_v2. O login (username) não muda: as coleções guardam o login
    // do separador.

    private EditProfessionalPermissionsDialog? _editProfessionalPermissionsDialog;
    private UserAuthorizationConfig? _editingProfessionalCurrentAuth;

    [ObservableProperty] private Professional? editingProfessional;
    [ObservableProperty] private ProfessionalPermissionsEditorViewModel editProfessionalPermissions = new();
    [ObservableProperty] private string editProfessionalSurname = string.Empty;
    [ObservableProperty] private string editProfessionalEmail = string.Empty;
    [ObservableProperty] private string editProfessionalNewPassword = string.Empty;
    [ObservableProperty] private string editProfessionalConfirmPassword = string.Empty;
    [ObservableProperty] private bool isSavingProfessionalPermissions;
    [ObservableProperty] private string? editProfessionalPermissionsError;
    [ObservableProperty] private string? editProfessionalPermissionsSuccess;

    // Permissões salvas do professional (Users.Authorization no Postgres, JSON de UserAuthorizationConfig, vem em
    // GetCompanyProfessionals_v2). Vazio/ilegível = nenhuma.
    public static UserAuthorizationConfig ParseAuthorization(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new UserAuthorizationConfig();
        try
        {
            var auth = JsonConvert.DeserializeObject<UserAuthorizationConfig>(json) ?? new UserAuthorizationConfig();
            auth.Roles ??= new();
            auth.Policies ??= new();
            return auth;
        }
        catch
        {
            return new UserAuthorizationConfig();
        }
    }

    [RelayCommand]
    private async Task OpenEditProfessionalPermissions(Professional? professional)
    {
        if (professional == null || string.IsNullOrWhiteSpace(professional.username))
            return;

        try
        {
            EditingProfessional = professional;
            EditProfessionalSurname = professional.surname ?? string.Empty;
            EditProfessionalEmail = professional.contactMail ?? string.Empty;
            EditProfessionalNewPassword = string.Empty;
            EditProfessionalConfirmPassword = string.Empty;
            _editingProfessionalCurrentAuth = ParseAuthorization(professional.Authorization);
            EditProfessionalPermissionsError = null;
            EditProfessionalPermissionsSuccess = null;
            EditProfessionalPermissions = new ProfessionalPermissionsEditorViewModel();
            _ = EditProfessionalPermissions.LoadAsync(_editingProfessionalCurrentAuth);

            _editProfessionalPermissionsDialog = new EditProfessionalPermissionsDialog { DataContext = this };
            if (MainWindow.instance != null)
                await _editProfessionalPermissionsDialog.ShowDialog(MainWindow.instance);
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Could not open the professional.") + " " + ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveProfessionalPermissions()
    {
        var professional = EditingProfessional;
        if (professional == null || IsSavingProfessionalPermissions)
            return;

        var fields = ProfessionalEditChanges.Compute(professional.surname, professional.contactMail,
            EditProfessionalSurname, EditProfessionalEmail, EditProfessionalNewPassword, EditProfessionalConfirmPassword);
        if (fields.Error != null)
        {
            EditProfessionalPermissionsError = fields.Error;
            return;
        }

        // Sem catálogo (ambiente sem o endpoint) as permissões ficam como estão; os outros campos ainda podem mudar.
        var chosen = EditProfessionalPermissions.Build();
        var diff = chosen == null
            ? new ProfessionalPermissionsDiff()
            : ProfessionalPermissionsDiff.Compute(_editingProfessionalCurrentAuth, chosen,
                EditProfessionalPermissions.CatalogRoles, EditProfessionalPermissions.CatalogPolicies);

        if (diff.IsEmpty && !fields.HasFieldChanges)
        {
            CancelEditProfessionalPermissions();
            return;
        }

        IsSavingProfessionalPermissions = true;
        EditProfessionalPermissions.IsEnabled = false;
        EditProfessionalPermissionsError = null;
        try
        {
            var r = await GlobalAppStateViewModel.lfc.UpdateCompanyProfessional(professional.username,
                fields.SurName, fields.ContactMail, fields.NewPassword,
                diff.RolesToAdd, diff.RolesToRemove, diff.PoliciesToAdd, diff.PoliciesToRemove);

            if (r?.success == true)
            {
                // Mesma instância da lista de profissionais: o card e a próxima abertura já mostram o que foi salvo.
                if (fields.SurName != null)
                    professional.surname = fields.SurName.Length == 0 ? null : fields.SurName;
                if (fields.ContactMail != null)
                    professional.contactMail = fields.ContactMail;
                if (r.Content != null)
                {
                    professional.Authorization = JsonConvert.SerializeObject(r.Content);
                    _editingProfessionalCurrentAuth = r.Content;
                }
                EditProfessionalNewPassword = string.Empty;
                EditProfessionalConfirmPassword = string.Empty;
                EditProfessionalPermissionsSuccess = fields.NewPassword != null
                    ? Loc.Tr("Saved. The professional was signed out and must log in with the new password.")
                    : Loc.Tr("Saved.");
                await ApplyProfessionalFilterAsync(System.Threading.CancellationToken.None);
                await Task.Delay(fields.NewPassword != null ? 2500 : 1000);
                CancelEditProfessionalPermissions();
            }
            else
            {
                EditProfessionalPermissionsError = string.IsNullOrWhiteSpace(r?.message) ? Loc.Tr("Could not save the professional.") : r.message;
            }
        }
        catch (Exception ex)
        {
            EditProfessionalPermissionsError = Loc.Tr("Could not save the professional.") + " " + ex.Message;
        }
        finally
        {
            IsSavingProfessionalPermissions = false;
            EditProfessionalPermissions.IsEnabled = true;
        }
    }

    [RelayCommand]
    private void CancelEditProfessionalPermissions()
    {
        _editProfessionalPermissionsDialog?.Close();
        _editProfessionalPermissionsDialog = null;
        EditProfessionalNewPassword = string.Empty;
        EditProfessionalConfirmPassword = string.Empty;
    }
}

