using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;

namespace LesserDashboardClient.Models;

/// <summary>
/// Modelo de mensagem do usuário (usado tanto para deserializar a API quanto para a UI)
/// </summary>
public partial class UserMessage : ObservableObject
{
    [ObservableProperty]
    [JsonProperty("_id")]
    private string id = string.Empty;

    [ObservableProperty]
    [JsonProperty("title")]
    private string title = string.Empty;

    [ObservableProperty]
    [JsonProperty("content")]
    private string content = string.Empty;

    [ObservableProperty]
    [JsonProperty("createdAt")]
    private DateTime createdDate;

    [ObservableProperty]
    [JsonProperty("isVisualized")]
    private bool isRead;

    [ObservableProperty]
    private string messageType = "info"; // info, warning, error, success (não vem da API, valor padrão)
}







