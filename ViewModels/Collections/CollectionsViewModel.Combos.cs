using Avalonia.Interactivity;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExCSS;
using JavaScriptCore;
using LesserDashboardClient.Models;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using Newtonsoft.Json;
using OfficeOpenXml;
using SharedClientSide;
using SharedClientSide.DataStructure.CoreApp;
using SharedClientSide.Helpers;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Companies.Requests;
using SharedClientSide.ServerInteraction.Users.Graduate;
using SharedClientSide.ServerInteraction.Users.Professionals;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace LesserDashboardClient.ViewModels.Collections;

public partial class CollectionsViewModel : ViewModelBase
{
    public CollectionComboOptions NewCollection_Combo0 { get; } = new CollectionComboOptions
    {
        ComboTitle = "Básico",
        ComboDescription = "Nenhum recurso adicional habilitado. Apenas armazenamento local padrão.",
        ComboColorAccent = "#B0B0B0", // cinza neutro
        ComboPrice = 0.0,
        BackupHd = false,
        AutoTreatment = false,
        EnablePhotoSales = false,
        AllowCPFsToSeeAllPhotos = false,
        Ocr = false
    };

    public CollectionComboOptions NewCollection_Combo1 { get; } = new CollectionComboOptions
    {
        ComboTitle = "Backup HD",
        ComboDescription = "Inclui backup automático em HD, garantindo segurança das fotos contra perda de dados.",
        ComboColorAccent = "#6495ED", // azul claro
        ComboPrice = 0.01,
        BackupHd = true,
        AutoTreatment = false,
        EnablePhotoSales = false,
        AllowCPFsToSeeAllPhotos = false,
        Ocr = false
    };

    public CollectionComboOptions NewCollection_Combo2 { get; } = new CollectionComboOptions
    {
        ComboTitle = "Reconhecimento facial + BackupHD + Tratamento com IA",
        ComboDescription = "Detecta rostos automaticamente, faz backup das fotos em HD e aplica ajustes automáticos de brilho e saturação usando IA.",
        ComboColorAccent = "#FF8C00", // laranja
        ComboPrice = 0.03,
        BackupHd = true,
        AutoTreatment = true,
        EnablePhotoSales = false,
        AllowCPFsToSeeAllPhotos = false,
        Ocr = false
    };

    public CollectionComboOptions NewCollection_Combo3 { get; } = new CollectionComboOptions
    {
        ComboTitle = "Tratamento avançado + Venda de fotos",
        ComboDescription = "Aplica tratamento automático nas fotos e habilita a venda de fotos, permitindo que CPFs específicos vejam todas as imagens.",
        ComboColorAccent = "#32CD32", // verde
        ComboPrice = 0.05,
        BackupHd = true,
        AutoTreatment = true,
        EnablePhotoSales = true,
        AllowCPFsToSeeAllPhotos = true,
        Ocr = false
    };

    public CollectionComboOptions NewCollection_Combo4 { get; } = new CollectionComboOptions
    {
        ComboTitle = "Completo: OCR + IA + Backup + Venda",
        ComboDescription = "Inclui todos os recursos: backup em HD, tratamento automático com IA, reconhecimento de texto (OCR), venda de fotos e acesso completo para CPFs autorizados.",
        ComboColorAccent = "#8A2BE2", // roxo
        ComboPrice = 0.08,
        BackupHd = true,
        AutoTreatment = true,
        EnablePhotoSales = true,
        AllowCPFsToSeeAllPhotos = true,
        Ocr = true
    };

}
    
          



