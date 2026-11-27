using System.Collections.Generic;

namespace LesserDashboardClient.Helpers;

public enum UpdateStepAction
{
    None,
    OpenStore,
    OpenInstalledApps,
    DownloadZip
}

public sealed record UpdateStep(
    int Number,
    string Title,
    string Detail,
    UpdateStepAction Action,
    string? ActionLabel,
    bool IsPrimaryAction)
{
    public bool HasAction => Action != UpdateStepAction.None;
    public bool IsSecondaryAction => HasAction && !IsPrimaryAction;
    public string NumberText => Number.ToString();
}

public sealed record UpdateInstructionsContent(
    string Label,
    string Headline,
    string Explanation,
    string StepsTitle,
    IReadOnlyList<UpdateStep> Steps,
    string DismissLabel,
    string? DismissNote);

/// <summary>
/// Textos e passos da tela de instruções de atualização.
/// Regra dos passos: o que precisa do link (baixar o zip, abrir a Store) vem ANTES de desinstalar,
/// porque desinstalar fecha o app e some esta tela.
/// </summary>
public static class UpdateInstructionsContentFactory
{
    public static UpdateInstructionsContent For(UpdateInstructionsDecision decision)
    {
        string dismissLabel = decision.IsBlocking ? "Fechar" : "Mais tarde";

        if (decision.Mode == UpdateInstructionsMode.ReinstallFromZip)
        {
            return new UpdateInstructionsContent(
                Label: "Mudança importante",
                Headline: "Reinstale o LetsPic em 3 passos",
                Explanation: "Esta instalação vai parar de funcionar. A versão nova não instala por cima da antiga, então é preciso desinstalar esta primeiro.",
                StepsTitle: "Como reinstalar",
                Steps: new[]
                {
                    new UpdateStep(1, "Baixe a versão nova", "Guarde o arquivo antes de desinstalar.",
                        UpdateStepAction.DownloadZip, "Baixar letspic.zip", IsPrimaryAction: true),
                    new UpdateStep(2, "Desinstale este LetsPic", "Em Aplicativos instalados, procure LetsPic e clique em Desinstalar.",
                        UpdateStepAction.OpenInstalledApps, "Abrir Aplicativos instalados", IsPrimaryAction: false),
                    new UpdateStep(3, "Instale", "Abra o letspic.zip baixado e siga as instruções dele.",
                        UpdateStepAction.None, null, IsPrimaryAction: false)
                },
                DismissLabel: dismissLabel,
                DismissNote: "Este aviso volta a cada abertura até você reinstalar.");
        }

        var storeReinstallSteps = new[]
        {
            new UpdateStep(1, "Abra o LetsPic na Microsoft Store", "Deixe a página aberta.",
                UpdateStepAction.OpenStore, "Abrir Microsoft Store", IsPrimaryAction: false),
            new UpdateStep(2, "Desinstale o LetsPic", "Em Aplicativos instalados, procure LetsPic e clique em Desinstalar.",
                UpdateStepAction.OpenInstalledApps, "Abrir Aplicativos instalados", IsPrimaryAction: false),
            new UpdateStep(3, "Instale de novo", "Volte à página da Store e clique em Instalar.",
                UpdateStepAction.None, null, IsPrimaryAction: false)
        };

        if (decision.IsBlocking)
        {
            return new UpdateInstructionsContent(
                Label: "Atualização obrigatória",
                Headline: "Esta versão do LetsPic parou de funcionar",
                Explanation: "Atualize pela Microsoft Store para continuar usando o LetsPic.",
                StepsTitle: "Não funcionou? Reinstale",
                Steps: storeReinstallSteps,
                DismissLabel: dismissLabel,
                DismissNote: "Fechar esta tela fecha o aplicativo.");
        }

        return new UpdateInstructionsContent(
            Label: "Atualização disponível",
            Headline: "Há uma versão nova do LetsPic",
            Explanation: "Você pode continuar usando, mas o ideal é atualizar agora.",
            StepsTitle: "Não funcionou? Reinstale",
            Steps: storeReinstallSteps,
            DismissLabel: dismissLabel,
            DismissNote: null);
    }
}
