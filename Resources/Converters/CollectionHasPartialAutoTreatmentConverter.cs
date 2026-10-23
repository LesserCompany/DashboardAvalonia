using Avalonia.Data.Converters;
using LesserDashboardClient.Helpers;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LesserDashboardClient.Resources.Converters;

/// <summary>
/// Visibilidade do ícone de tratamento parcial na lista de coleções: values[0] = a coleção da linha
/// (ProfessionalTask), values[1] = CollectionsViewModel.PartialAutoTreatmentClassCodes (classCodes com ao
/// menos um lote, vindos de GetCollectionsWithPartialAutoTreatment). Mesmo esquema de MultiBinding do
/// BillingCanceledListItemBrushConverter — o ProfessionalTask não carrega essa informação.
/// </summary>
public sealed class CollectionHasPartialAutoTreatmentConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!PartialAutoTreatmentFeature.ShowInList(hasBatch: true))
            return false;
        if (values == null || values.Count < 2)
            return false;
        if (values[0] is not ProfessionalTask item || string.IsNullOrEmpty(item.classCode))
            return false;
        return values[1] is ISet<string> classCodes && classCodes.Contains(item.classCode);
    }
}
