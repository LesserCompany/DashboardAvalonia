using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodingSeb.Localization;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Graduate;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Collections
{
    public enum DeleteCollectionChoiceResult
    {
        None,
        FullDeletion,
        PartialDeletion
    }

    public partial class DeleteCollectionChoiceViewModel : ViewModelBase
    {
        private readonly LesserFunctionClient _lfc;
        private readonly string _classCode;
        private Window? _window;

        [ObservableProperty]
        private bool showChoicePanel = true;

        [ObservableProperty]
        private bool showPartialPanel = false;

        [ObservableProperty]
        private bool graduatesLoading = false;

        [ObservableProperty]
        private ObservableCollection<GraduateByCPFWithPhotos> graduatesList = new();

        [ObservableProperty]
        private string? loadGraduatesError;

        /// <summary>Resultado após fechar o diálogo: Full = usuário escolheu exclusão total (caller deve confirmar e chamar RequestDeleteCollection).</summary>
        public DeleteCollectionChoiceResult Result { get; private set; } = DeleteCollectionChoiceResult.None;

        public DeleteCollectionChoiceViewModel(LesserFunctionClient lfc, string classCode, string? collectionDisplayName = null)
        {
            _lfc = lfc ?? throw new ArgumentNullException(nameof(lfc));
            _classCode = classCode ?? throw new ArgumentNullException(nameof(classCode));
            CollectionDisplayName = collectionDisplayName ?? classCode;
        }

        public string CollectionDisplayName { get; }

        public void SetWindow(Window window)
        {
            _window = window;
        }

        [RelayCommand]
        private void ChooseFullDeletion()
        {
            Result = DeleteCollectionChoiceResult.FullDeletion;
            _window?.Close();
        }

        [RelayCommand]
        private async Task ChoosePartialDeletionAsync()
        {
            ShowChoicePanel = false;
            ShowPartialPanel = true;
            LoadGraduatesError = null;
            await LoadGraduatesAsync();
        }

        [RelayCommand]
        private void BackToChoice()
        {
            ShowPartialPanel = false;
            ShowChoicePanel = true;
            GraduatesList.Clear();
            LoadGraduatesError = null;
        }

        private async Task LoadGraduatesAsync()
        {
            GraduatesLoading = true;
            LoadGraduatesError = null;
            try
            {
                var result = await _lfc.GetGraduatesByCPFByClassCode(_classCode);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GraduatesList.Clear();
                    if (result?.Content != null)
                    {
                        foreach (var g in result.Content.Where(x => x != null))
                            GraduatesList.Add(g);
                    }
                    if (GraduatesList.Count == 0 && string.IsNullOrEmpty(result?.message))
                        LoadGraduatesError = Loc.Tr("No graduates found for this collection.", "Nenhum formando encontrado para esta coleção.");
                    else if (result?.loginFailed == true || result?.success == false)
                        LoadGraduatesError = result?.message ?? Loc.Tr("Error loading list.", "Erro ao carregar a lista.");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    LoadGraduatesError = ex.Message);
            }
            finally
            {
                GraduatesLoading = false;
            }
        }

        [RelayCommand]
        private void CloseWindow()
        {
            _window?.Close();
        }
    }
}
