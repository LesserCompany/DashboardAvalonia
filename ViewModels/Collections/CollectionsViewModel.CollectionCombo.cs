using System;
using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LesserDashboardClient.Models;
using LesserDashboardClient.Resources.Converters;
using LesserDashboardClient.Services;

namespace LesserDashboardClient.ViewModels.Collections;

// Combo de uma coleção já criada (lista lateral e "Ver configuração da coleção"). Ver CollectionComboMatcher.
public partial class CollectionsViewModel
{
    // Combos vindos do servidor (GetCompanyComboPrices), usados só para descobrir o combo das coleções. Fica vazio
    // quando só há os combos estáticos de fallback, que não trazem todas as opções e dariam resultado errado.
    // Trocado por uma lista nova a cada carga para disparar o MultiBinding das linhas da lista.
    [ObservableProperty] private IReadOnlyList<CollectionComboOptions> combosForCollectionMatching = Array.Empty<CollectionComboOptions>();

    // Combo da coleção aberta em "Ver configuração da coleção" (null quando não é combo ou fora dessa tela).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewedCollectionComboIsVisible), nameof(ViewedCollectionComboTitle), nameof(ViewedCollectionComboBrush))]
    private CollectionComboOptions? viewedCollectionCombo;

    public bool ViewedCollectionComboIsVisible => IsViewingCollectionConfig && !string.IsNullOrWhiteSpace(ViewedCollectionCombo?.ComboTitle);
    public string? ViewedCollectionComboTitle => ViewedCollectionCombo?.ComboTitle;
    public IBrush ViewedCollectionComboBrush => CollectionComboConverter.ToBrush(ViewedCollectionCombo?.ComboColorAccent);

    partial void OnCombosForCollectionMatchingChanged(IReadOnlyList<CollectionComboOptions> value) => RefreshViewedCollectionCombo();

    private void RefreshViewedCollectionCombo()
    {
        ViewedCollectionCombo = IsViewingCollectionConfig
            ? CollectionComboMatcher.FindCombo(SelectedCollection, CombosForCollectionMatching)
            : null;
        OnPropertyChanged(nameof(ViewedCollectionComboIsVisible));
    }
}
