using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaWebView;
using LesserDashboardClient.ViewModels.SearchGraduate;
using LesserDashboardClient.ViewModels.Shared;
using LesserDashboardClient.Views.Shared;

namespace LesserDashboardClient.Views.SearchGraduate;

public partial class SearchGraduateControl : UserControl
{
    private static SearchGraduateControl? _lastInstance;

    private WebView? _webView;
    private OpenInWebButton? _openInWebButton;

    private IReadOnlyList<string>? _pendingCpfsToInject;

    /// <summary>Evita <see cref="InjectCpfsWhenReadyAsync"/> após o controlo sair da árvore visual.</summary>
    private volatile bool _detachedFromVisualTree;

    public SearchGraduateControl()
    {
        InitializeComponent();

        if (SearchGraduateNavigationState.HasPendingCpfs)
        {
            _pendingCpfsToInject = SearchGraduateNavigationState.PendingCpfs;
            SearchGraduateNavigationState.Clear();
        }

        this.Loaded += SearchGraduateControl_Loaded;
        this.DetachedFromVisualTree += (_, _) => _detachedFromVisualTree = true;
        this.AttachedToVisualTree += (_, _) => _detachedFromVisualTree = false;
        this.Unloaded += (_, _) =>
        {
            _detachedFromVisualTree = true;
            if (ReferenceEquals(_lastInstance, this))
                _lastInstance = null;
        };
    }

    /// <summary>
    /// Chamado externamente quando a aba já está visível e novos CPFs precisam ser injetados.
    /// </summary>
    public static void InjectCpfsIfVisible(IReadOnlyList<string> cpfs)
    {
        if (_lastInstance == null) return;
        _lastInstance._pendingCpfsToInject = cpfs;

        if (_lastInstance.DataContext is SearchGraduateViewModel vm)
        {
            if (vm.SelectedSectionIndex == 1)
            {
                vm.NotifyActiveUrlChanged();
                _ = _lastInstance.InjectCpfsWhenReadyAsync();
            }
            else
            {
                vm.SelectedSectionIndex = 1;
            }
        }
    }

    private void SearchGraduateControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _lastInstance = this;
        _webView = this.FindControl<WebView>("PART_WebView");
        _openInWebButton = this.FindControl<OpenInWebButton>("OpenInWebButtonControl");

        if (DataContext is SearchGraduateViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SearchGraduateViewModel.SelectedSectionIndex))
                {
                    SyncOpenInWebButton(vm);
                    if (vm.SelectedSectionIndex == 1 && _pendingCpfsToInject is { Count: > 0 })
                        _ = InjectCpfsWhenReadyAsync();
                }
                else if (args.PropertyName == nameof(SearchGraduateViewModel.ActiveUrlWeb))
                {
                    SyncOpenInWebButton(vm);
                }
            };

            SyncOpenInWebButton(vm);

            if (_pendingCpfsToInject is { Count: > 0 } && vm.SelectedSectionIndex == 1)
                _ = InjectCpfsWhenReadyAsync();
        }
    }

    private static string AppendFilterQuery(string url, IReadOnlyList<string> cpfs)
    {
        var text = string.Join(" ", cpfs);
        return url + "&filter=" + Uri.EscapeDataString(text);
    }

    private static Uri? ToNavigateUri(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri;
        return null;
    }

    /// <summary>
    /// 1) Tenta injetar via <c>ExecuteScriptAsync</c> → <c>window.receiveCpfs</c> (sem CPF na URL).
    /// 2) Fallback: recarrega a mesma rota com <c>&amp;filter=...</c> para o Svelte ler no <c>onMount</c>.
    /// </summary>
    private async Task InjectCpfsWhenReadyAsync()
    {
        if (_webView == null || _pendingCpfsToInject == null || _pendingCpfsToInject.Count == 0)
            return;
        if (DataContext is not SearchGraduateViewModel vm)
            return;

        var cpfs = _pendingCpfsToInject;
        _pendingCpfsToInject = null;

        var jsonArray = JsonSerializer.Serialize(cpfs);

        var checkAndInjectScript =
            $"(function(){{" +
            $"if(typeof window.receiveCpfs==='function'){{window.receiveCpfs({jsonArray});return 'ok';}}" +
            $"return 'pending';" +
            $"}})()";

        const int maxRetries = 15;
        const int baseDelayMs = 300;

        for (int i = 0; i < maxRetries; i++)
        {
            if (_detachedFromVisualTree)
                return;

            await Task.Delay(baseDelayMs + (i * 200)).ConfigureAwait(true);

            if (_detachedFromVisualTree)
                return;

            try
            {
                var result = await _webView.ExecuteScriptAsync(checkAndInjectScript);
                if (result != null && result.Contains("ok", StringComparison.Ordinal))
                {
                    Debug.WriteLine($"[SearchGraduateControl] CPFs injected after {i + 1} attempt(s)");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchGraduateControl] ExecuteScriptAsync attempt {i + 1} failed: {ex.Message}");
            }
        }

        Debug.WriteLine("[SearchGraduateControl] Injection failed — fallback to URL query filter");

        if (_detachedFromVisualTree)
            return;

        var urlWithFilter = AppendFilterQuery(vm.UrlProtectedCpf, cpfs);
        var webWithFilter = AppendFilterQuery(vm.UrlProtectedCpfWeb, cpfs);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_detachedFromVisualTree)
                return;
            if (ToNavigateUri(urlWithFilter) is { } uri)
                _webView.Url = uri;

            if (_openInWebButton?.DataContext is OpenInWebButtonViewModel btnVm)
                btnVm.WebUrl = webWithFilter;
        });
    }

    private void SyncOpenInWebButton(SearchGraduateViewModel vm)
    {
        if (_openInWebButton?.DataContext is OpenInWebButtonViewModel btnVm)
            btnVm.WebUrl = vm.ActiveUrlWeb;
    }
}
