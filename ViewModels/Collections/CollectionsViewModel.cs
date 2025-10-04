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
using SharedClientSide.ServerInteraction.Users.Requests;
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
using LesserDashboardClient.Services;


namespace LesserDashboardClient.ViewModels.Collections;

public partial class CollectionsViewModel : ViewModelBase
{
    private static Queue<string> CollectionCreationQueue = new Queue<string>();

    public class GraduateReportInfo
    {
        public string Name { get; set; } = "";
        public int BlueTags { get; set; } = 0;
        public int RedTags { get; set; } = 0;
        public int TotalTags
        {
            get
            {
                return BlueTags + RedTags;
            }
        }
    }

    #region VIEWS
    // ======================== VIEWS ======================== 

    private CancellationTokenSource _previewDebounceCtsViewCollection;
    [ObservableProperty] public ProfessionalTask selectedCollection;
    partial void OnSelectedCollectionChanged(ProfessionalTask value)
    {

        _previewDebounceCtsViewCollection?.Cancel();
        _previewDebounceCtsViewCollection = new();
        var token = _previewDebounceCtsViewCollection.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested)
                    return;
                // dispara carregamentos com base no ActiveCollection
                UpdateCollectionViewSelected();
            }
            catch (OperationCanceledException) { }
        }, token);
    }
    public enum ActiveViews
    {
        QuickAccess,
        Banners,
        NewCollection,
        NewCollectionPreConfigured,
        CollectionView,
        SelectProfessional,
        CancelBilling
    }
    [ObservableProperty]
    private ActiveViews activeComponent = ActiveViews.QuickAccess;
    private ActiveViews lastActiveComponent;
    partial void OnActiveComponentChanged(ActiveViews oldValue, ActiveViews newValue)
    {
        lastActiveComponent = oldValue;

        OnPropertyChanged(nameof(ComponentBannersIsVisible));
        OnPropertyChanged(nameof(ComponentNewCollectionIsVisible));
        OnPropertyChanged(nameof(ComponentCollectionViewIsVisible));
        OnPropertyChanged(nameof(ComponentSelectProfessionalIsIsVisible));
        OnPropertyChanged(nameof(ComponentQuickAccessIsVisible));
        OnPropertyChanged(nameof(ComponentCancelBillingIsVisible));
        OnPropertyChanged(nameof(ComponentNewCollectionPreConfiguredIsVisible));
    }
    public bool ComponentQuickAccessIsVisible => ActiveComponent == ActiveViews.QuickAccess;
    public bool ComponentBannersIsVisible => ActiveComponent == ActiveViews.Banners;
    public bool ComponentSelectProfessionalIsIsVisible => ActiveComponent == ActiveViews.SelectProfessional;
    public bool ComponentNewCollectionIsVisible => ActiveComponent == ActiveViews.NewCollection;
    public bool ComponentCollectionViewIsVisible => ActiveComponent == ActiveViews.CollectionView;
    public bool ComponentCancelBillingIsVisible => ActiveComponent == ActiveViews.CancelBilling;
    public bool ComponentNewCollectionPreConfiguredIsVisible => ActiveComponent == ActiveViews.NewCollectionPreConfigured;

    #endregion
    [ObservableProperty] private ObservableCollection<ProfessionalTask> collectionsList = new();
    [ObservableProperty] private ObservableCollection<ProfessionalTask> collectionsListFiltered = new();
    [ObservableProperty] private ObservableCollection<GraduateByCPF> graduatesData = new();
    [ObservableProperty] private bool updatingGraduatesData;
    [ObservableProperty] public bool collectionsListIsLoading = true;
    [ObservableProperty] public bool isEnabledFilters = false;
    [ObservableProperty] public bool isUpdateProgressBars = false;
    [ObservableProperty] public int? totalPhotosForRecognition;
    [ObservableProperty] public int? totalPhotosForRecognitionDone;
    [ObservableProperty] public int? totalPhotosForAutoTreatment;
    [ObservableProperty] public int? totalPhotosForAutoTreatmentDone;
    [ObservableProperty] public bool uploadComplete = true;
    [ObservableProperty] public int? totalPhotosForOCR;
    [ObservableProperty] public int? totalPhotosForOCRDone;
    [ObservableProperty] public bool? cbUploadOnTestSystem;
    [ObservableProperty] public bool? cbAllowDeletedProductionToBeFoundAnyone;

    //ProfessionalTask Props
    [ObservableProperty] public string tbCollectionName;
    [ObservableProperty] public string tbEventFolder;
    partial void OnTbEventFolderChanged(string value)
    {
        CheckPathEventFolder();
    }
    [ObservableProperty] public string tbRecFolder;
    partial void OnTbRecFolderChanged(string value)
    {
        if (Directory.Exists(value))
        {
            TbRecFolderError = false;
            InsertDataBasedOnRecFolderCommand();
        }
        else
            TbRecFolderError = true;
    }
    [ObservableProperty] public bool tbEventFolderError = false;
    [ObservableProperty] public bool tbRecFolderError = false;
    [ObservableProperty] public bool? cbHDBackup;

    //Buttons
    [ObservableProperty] public bool actionsButtonsIsVisible = false;
    [ObservableProperty] public bool showButtonsCollectionView = true;
    [ObservableProperty] public bool btTagSortIsEnabled = false;
    [ObservableProperty] public bool btTagSortIsRunning = false;
    [ObservableProperty] public bool btExportIsEnabled = false;
    [ObservableProperty] public bool btExportIsRunning = false;
    [ObservableProperty] public bool btReportsIsEnabled = false;
    [ObservableProperty] public bool btDownloadHdIsEnabled = false;
    [ObservableProperty] public bool btDownloadHdIsRunning = false;
    [ObservableProperty] public bool btReEnqueueIsEnabled = true;
    [ObservableProperty] public bool btReenqueueIsRunning = false;
    [ObservableProperty] public bool btRequestManualTreatmentAllCollectionIsEnabled = true;
    [ObservableProperty] public bool btRequestManualTreatmentAllCollectionIsRunning = false;
    [ObservableProperty] public bool selectedCollectionIsCanceled;
    [ObservableProperty] public bool expanderAdvancedOptionsIsEnabled;
    partial void OnCbHDBackupChanged(bool? oldValue, bool? newValue)
    {
        if (newValue == false)
        {
            CbEnableAutoTreatment = false;
        }
    }
    [ObservableProperty] public bool? cbEnableAutoTreatment;
    partial void OnCbEnableAutoTreatmentChanged(bool? oldValue, bool? newValue)
    {
        if (newValue == true)
        {
            CbHDBackup = true;
        }
    }
    [ObservableProperty] public bool? cbEnableAutoExclusion = true;
    [ObservableProperty] public bool? cbEnablePhotoSales;
    [ObservableProperty] public double? tbPricePerPhotoForSellingOnline;
    [ObservableProperty] public bool? cbAllowCPFsToSeeAllPhotos;
    [ObservableProperty] public bool? cbUploadedPhotosAreAlreadySorted;
    [ObservableProperty] public string tbProfessionalTaskDescription;
    [ObservableProperty] public string? autoTreatmentVersion;
    [ObservableProperty] public bool isReupload = false;
    [ObservableProperty] public string? tbProfessioanlTaskDescription;
    [ObservableProperty] public bool? cbOcr;

    [ObservableProperty] public bool expanderAdvancedOptions;
    [ObservableProperty] public int scrollComponentNewCollection = 0;
    [ObservableProperty] public ObservableCollection<ClassSeparationFile> classSeparationFiles = new();
    [ObservableProperty] public ClassSeparationFile selectedSeparationFile;
    partial void OnSelectedSeparationFileChanged(ClassSeparationFile value)
    {
        UpdateSeparationProgress();
    }

    [ObservableProperty] public bool classSeparationFilesIsVisible;
    [ObservableProperty] public bool isCreatingCollection = false;

    [ObservableProperty] public bool componentNewCollectionIsEnabled = true;
    [ObservableProperty] public bool loadProfessionalsIsRunning = false;



    [ObservableProperty] public List<Professional> professionals = new();
    [ObservableProperty] public Professional selectedProfessional;
    partial void OnSelectedProfessionalChanged(Professional value)
    {
        BackLasViewCommand();
        if(SelectedCollection != null)
            SelectedCollection.professionalLogin = SelectedProfessional.username;
        CurrentProfessionalName = SelectedProfessional.username;
    }
    [ObservableProperty] public string currentProfessionalName;

    //FREE TRIAL PROPERTIES
    //[ObservableProperty] public bool accountInPeriodFreeTrial = false;
    //[ObservableProperty] public string? freePhotosRemainingInTrialPeriod;
    //[ObservableProperty] public double currentPercentagePhotosFreeTrialRemaining;
    //[ObservableProperty] public string freeTrialExpiryDate = "N/A";
    //[ObservableProperty] public bool isFirstUse = false;
    [ObservableProperty] public RemainingFreeTrialPhotosResult remainingFreeTrialPhotosResult = new RemainingFreeTrialPhotosResult { IsFreeTrialActive = false };


    //PPP
    [ObservableProperty] public bool resumeSeparationFileComponentIsVisible;
    [ObservableProperty] public SeparationProgress? separationProgressValue;
    [ObservableProperty] public string labelPercentNotSeparated;
    [ObservableProperty] public string labelPercentSeparated;
    [ObservableProperty] public double separatedRectHeight;
    [ObservableProperty] public double notSeparatedRectHeight;
    [ObservableProperty] public ServerProgress? serverProgressValues;

    public ObservableCollection<string> BlockTypeOptions { get; } =
    new ObservableCollection<string> { "WATERMARK", "ACCESS_DENIED" };

    [ObservableProperty] public ProfessionalTask selectedCollectionForCancelBilling;
    [ObservableProperty] public bool cancelBillingIsRunning;

    [ObservableProperty] public bool reportsContainerIsVisible = false;

    [ObservableProperty] public bool generatingGeneralReport = false;
    [ObservableProperty] public bool generatingReportPerGraduate = false;




    public CollectionsViewModel()
    {
        LoadProfessionalTasks();
        Task.Run(()=>LoadProfessionals());
        GetInfosAboutFreeTrialPeriod();
        LoadDynamicPrices();
        System.Timers.Timer timerUpdateView = new System.Timers.Timer();
        timerUpdateView.Interval = 60000;
        timerUpdateView.Elapsed += (e, a) =>
        {
            if(ActiveComponent == ActiveViews.CollectionView)
            {
                UpdateCollectionViewSelected();
            }
        };
        timerUpdateView.Start();
    }


    public async void LoadProfessionalTasks()
    {
        try
        {
            CollectionsListIsLoading = true;
            var r = await GlobalAppStateViewModel.lfc.getCompanyProfessionalTasks();

            if (r != null)
            {
                CollectionsList = new ObservableCollection<ProfessionalTask>(r
                    //r.OrderBy(x => x.CreationDate).Reverse()
                );
                CollectionsListFiltered = new ObservableCollection<ProfessionalTask>(CollectionsList);
                //CollectionsListFiltered = new ObservableCollection<ProfessionalTask>(CollectionsList.OrderBy(x => x.CreationDate).Reverse());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }
        finally
        {
            CollectionsListIsLoading = false;
            IsEnabledFilters = true;
        }
    }
    public async Task UpdateProfessionalTasksList(ProfessionalTask pt = null)
    {
        try
        {
            CollectionsListIsLoading = true;
            IsEnabledFilters = false;
            {
                if(pt != null)
                {
                    CollectionsList.Insert(0, pt);
                    CollectionsListFiltered.Clear();
                    CollectionsListFiltered = new ObservableCollection<ProfessionalTask>(CollectionsList);
                }
                else
                {
                    var r = await GlobalAppStateViewModel.lfc.getCompanyProfessionalTasks();
                    if (r != null)
                    {
                        CollectionsList = new ObservableCollection<ProfessionalTask>(r);
                        CollectionsListFiltered.Clear();
                        CollectionsListFiltered = new ObservableCollection<ProfessionalTask>(CollectionsList);
                    }
                }
            }

        }
        catch
        {

        }
        finally
        {
            CollectionsListIsLoading = false;
            IsEnabledFilters = true;
        }
    }

    public void FilterProfessionalTasks(string? classCode, string? professional)
    {
        try
        {
            CollectionsListIsLoading = true;
            if (CollectionsList == null)
            {
                CollectionsListFiltered = new ObservableCollection<ProfessionalTask>();
                return;
            }

            classCode ??= string.Empty;
            professional ??= string.Empty;

            var filtered = CollectionsList.Where(task =>
            {
                var login = task.professionalLogin ?? string.Empty;
                var taskClass = task.classCode ?? string.Empty;

                bool matchClass = string.IsNullOrEmpty(classCode) ||
                                  taskClass.Contains(classCode, StringComparison.OrdinalIgnoreCase);

                bool matchProfessional = string.IsNullOrEmpty(professional) ||
                                         login.Contains(professional, StringComparison.OrdinalIgnoreCase);

                return matchClass && matchProfessional;
            });

            CollectionsListFiltered = new ObservableCollection<ProfessionalTask>(filtered);

        }
        finally
        {
            CollectionsListIsLoading = false;
        }
    }
    public async Task UpdateProgressBars()
    {
            try
            {
                //var stopwatch = Stopwatch.StartNew();

                if (SelectedCollection == null || SelectedCollection.companyUsername == null || SelectedCollection.classCode == null || SelectedCollection.UploadComplete == null)
                    return;

                var selected = SelectedCollection;
                ServerProgressValues = await GlobalAppStateViewModel.lfc.UpdateAndGetServerProgress(selected.companyUsername, selected.classCode);


                if (ServerProgressValues == null)
                        return;
                TotalPhotosForRecognition = ServerProgressValues.total;
                TotalPhotosForRecognitionDone = ServerProgressValues.done;
                TotalPhotosForAutoTreatment = ServerProgressValues.total;
                TotalPhotosForAutoTreatmentDone = ServerProgressValues.autoTreated;
                TotalPhotosForOCR = ServerProgressValues.total;
                TotalPhotosForOCRDone = ServerProgressValues.ocr;

                //if (ServerProgressValues.uploaded < ServerProgressValues.total || ServerProgressValues.total == null)
                //{
                //    //UploadComplete = false;
                //}
                //else
                //{
                //    //UploadComplete = true;
                //}
                selected.UploadComplete = (ServerProgressValues.uploaded < ServerProgressValues.total || ServerProgressValues.total == null) ? false : true;
                //stopwatch.Stop();
                //Console.WriteLine($"UpdateProgressBarAndButtons levou {stopwatch.ElapsedMilliseconds} ms");
            }
            catch
            {

            }
            finally
            {

            }
    }
    private async void LoadGraduatesData(ProfessionalTask professionalTask)
    {
        GraduatesData.Clear();
        var result = await GlobalAppStateViewModel.lfc.GetGraduatesByCPFByClassCode(professionalTask.classCode);
        var graduates = result.Content;
        foreach (var g in graduates)
        {
            GraduatesData.Add(g);
        }
    }
    public async Task UpdateClassSeparationFile(string classCode)
    {
        List<ClassSeparationFile> sepFiles = new List<ClassSeparationFile>();

        var saveFolderPath = SharedClientSide.Helpers.Constants.SeparationFolder + "/" + classCode + "/Save";
        var redirectFilePath = SharedClientSide.Helpers.Constants.SeparationFolder + "/" + classCode + "/redirect.txt";
        if (File.Exists(redirectFilePath))
            saveFolderPath = File.ReadAllText(redirectFilePath) + "/Save";

        var localFilePath = SharedClientSide.Helpers.Constants.GetSaveFolder(classCode) + "/separacao.hermes";
        if (File.Exists(localFilePath))
            sepFiles.Add(new ClassSeparationFile()
            {
                Creator = "Este computador.",
                FileLocationType = ClassSeparationFile.FileLocationTypes.LOCAL,
                ModificationDate = File.GetLastWriteTime(localFilePath),
                FilePathInLocalDisk = localFilePath,
                SeparationProgressFilePath = localFilePath.Replace("separacao.hermes", "separationProgress.txt")
            });

        var localFilePathInLegacySaveFolder = SharedClientSide.Helpers.Constants.GetLegacySaveFolder(classCode) + "/separacao.hermes";
        if (File.Exists(localFilePathInLegacySaveFolder))
            sepFiles.Add(new ClassSeparationFile()
            {
                Creator = "Este computador",
                FileLocationType = ClassSeparationFile.FileLocationTypes.LOCAL,
                ModificationDate = File.GetLastWriteTime(localFilePath),
                FilePathInLocalDisk = localFilePathInLegacySaveFolder,
                SeparationProgressFilePath = localFilePathInLegacySaveFolder.Replace("separacao.hermes", "separationProgress.txt")
            });

        ClassSeparationFiles.Clear();
        foreach (var f in sepFiles) { ClassSeparationFiles.Add(f); }

        var userFiles = await LesserFunctionClient.DefaultClient.GetUserFilesInClass(classCode);
        if (userFiles.success)
        {
            foreach (var f in userFiles.Content)
            {
                if (f.blobName == "")
                    continue;
                if (f.blobName.EndsWith("separacao.hermes") == false)
                    continue;

                var user = f.blobName.Split('/')[f.blobName.Split('/').Length - 2];
                var company = f.blobName.Split('/')[0];
                var cloudFilePathInCompanyFolder = f.blobName.Substring(company.Length + 1);
                ClassSeparationFiles.Add(
                    new ClassSeparationFile()
                    {
                        SeparationProgressPathCloudCompanyFolder = cloudFilePathInCompanyFolder.Replace("separacao.hermes", "separationProgress.txt"),
                        FilePathInCloudCompanyFolder = cloudFilePathInCompanyFolder,
                        Creator = user,
                        ModificationDate = f.modificationDate.Value.Date,
                        FileLocationType = ClassSeparationFile.FileLocationTypes.CLOUD,
                        StorageLocation = f.storageLocation
                    }
                );
            }
        }
        if (ClassSeparationFiles.Count == 0)
        {
            ClassSeparationFilesIsVisible = false;
            SelectedSeparationFile = null;
        }
        else
        {
            ClassSeparationFilesIsVisible = true;
            SelectedSeparationFile = ClassSeparationFiles[0];
        }
    }

    public async void UpdateCollectionViewSelected()
    {

        try
        {
            ActionsButtonsIsVisible = false;
            IsUpdateProgressBars = true;

            ActiveComponent = ActiveViews.CollectionView;
            ExpanderAdvancedOptions = false;

            await UpdateProgressBars();

            if(SelectedCollection != null)
                await UpdateClassSeparationFile(SelectedCollection.classCode);

            SelectedCollectionIsCanceled = SelectedCollection?.BillingCancelled == true ? true : false;

            UpdateSeparationProgress();

            if(ServerProgressValues?.done >= ServerProgressValues?.total)
                ActionsButtonsIsVisible = true;

            UploadComplete = SelectedCollection?.UploadComplete == true ? true : false;

            if (ServerProgressValues != null)
                BtTagSortIsEnabled = (ServerProgressValues.done >= ServerProgressValues.total);

            BtExportIsEnabled = SelectedSeparationFile != null ? true : false;
            BtDownloadHdIsEnabled = SelectedCollection?.UploadHD == true ? true : false;

            if(SelectedCollection != null)
                if (SelectedCollection.BillingCancelled == true)
                {
                    BtTagSortIsEnabled = false;
                    BtExportIsEnabled = false;
                    BtReportsIsEnabled = false;
                    BtDownloadHdIsEnabled = false;
                }
        }
        finally
        {
            IsUpdateProgressBars = false;
        }
    }

    public double ConvertCentsToDecimal(int? cents)
    {
        if (cents is null)
            return 0;
        return cents.Value / 100;
    }
    public int ConvertDecimalToCents(double? decimalValue)
    {
        if (decimalValue is null)
            return 0;
        return (int)(decimalValue.Value * 100);
    }
    public bool IsTextAllowed(string text)
    {
        return RegexHelper.RegexToClassCode.IsMatch(text);
    }
    private bool CheckIfClassAlreadyExists(string classCode)
    {
        foreach (ProfessionalTask pt in CollectionsList)
        {
            if (pt.classCode == classCode)
                return true;
        }
        return false;
    }
    public (bool response, string message) CheckIfClassCanBeCreated(ProfessionalTask pt, List<FileInfo> EventFiles, List<FileInfo> RecFiles)
    {
        DirectoryInfo di = new DirectoryInfo(pt.originalEventsFolder);

        List<FileInfo> files = EventFiles.ToArray().Concat(RecFiles).ToList();

        (bool isThereFilepathTooLong, string firstFilepathTooLong) = FileHelper.FileListHasFilepathsLargerThan(files, 200);

        if (isThereFilepathTooLong)
            return (false, "O caminho do arquivo " + firstFilepathTooLong + " é muito longo, por isso o Windows não permite ao programa acessá-lo. O máximo permitido são 200 caracteres.");

        (bool isThereFilepathWithProhibitedCharacter, string firstFilepathWithProhibitedCharacter, string prohibitedCharacterFound)
            = FileHelper.FileListHasFilepathWithProhibitedCharacter(files, new string[] { "&", ";" });

        if (isThereFilepathWithProhibitedCharacter)
            return (false, "O caminho " + firstFilepathWithProhibitedCharacter + " possui o caracter " + prohibitedCharacterFound + ", que não é permitido.");

        foreach (FileInfo c in files)
        {
            if (!SharedClientSide.Helpers.RegexHelper.RegexToFileName.IsMatch(c.Name))
            {
                string chBlocked = "";
                foreach (char ch in c.Name)
                {
                    if (!SharedClientSide.Helpers.RegexHelper.RegexToFileName.IsMatch(ch.ToString()))
                    {
                        chBlocked = ch.ToString();
                        break;
                    }
                }
                return (false, $"Não é permitido utilizar o caractere '{chBlocked}' no nome do arquivo \"{c.FullName}\".\nRenomeie o arquivo e tente novamente.");
            }
        }

        return (true, "");
    }
    private string GenerateDynamicClassCode()
    {
        return DateTime.Now.ToString("dd_MM_yyyy__HH_mm_");
    }
    private void WaitForProcess(System.Diagnostics.Process p)
    {
        if (p == null)
        {
            Console.WriteLine("O processo é nulo");
            return;
        }
        try
        {
            p.WaitForInputIdle();
            p.WaitForExit();
        }
        catch (InvalidOperationException ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk($"Erro ao aguardar o processo: {ex.Message}");
        }
    }
    private async Task LoadProfessionals()
    {
        try
        {
            LoadProfessionalsIsRunning = true;
            if (GlobalAppStateViewModel.lfc != null)
            {
                Professionals = await GlobalAppStateViewModel.lfc.getCompanyProfessionals();

                if(Professionals != null && Professionals.Count > 0)
                    SelectedProfessional = Professionals[0];
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            LoadProfessionalsIsRunning = false;
        }
    }
    private void CheckPathEventFolder()
    {
        try
        {
            if (!Directory.Exists(TbEventFolder))
            {
                TbEventFolderError = true;
                return;
            }
            else
            {
                TbEventFolderError = false;
            }
            DirectoryInfo inputDir = new DirectoryInfo(TbEventFolder);
            DirectoryInfo eventFolder;

            string dirName = inputDir.Name.ToLower();
            var subDirectories = inputDir.GetDirectories().ToList();

            if (dirName == "1.eventos" || dirName.Contains("1.eventos_grande") || dirName.Contains("event"))
            {
                eventFolder = inputDir;
            }
            else
            {
                eventFolder = subDirectories.Find(x => x.Name.ToLower() == "1.eventos");
                    if (eventFolder == null)
                        eventFolder = subDirectories.Find(x => x.Name.ToLower().Contains("1.eventos_grande"));
                    if (eventFolder == null)
                        eventFolder = subDirectories.Find(x => x.Name.ToLower().Contains("event"));
            }

            DirectoryInfo classFolder;
            if (eventFolder != null)
            {
                classFolder = eventFolder;
                TbEventFolder = eventFolder.FullName;
            }
            else
            {
                classFolder = inputDir;
            }
            DirectoryInfo parentFolder = classFolder.Parent;

            if (parentFolder == null)
                return;
            string parentPath = parentFolder.FullName;
            if (!Directory.Exists(parentPath))
                return;
            classFolder = new DirectoryInfo(parentPath);

            subDirectories = classFolder.GetDirectories().ToList();

            DirectoryInfo recFolder;
            recFolder = subDirectories.Find(x => x.Name.ToLower() == "2.reconhecimentos");
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower() == "2.reconhecimentos_grande");
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower().Contains("reco"));
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower().Contains("id"));
            if (recFolder == null)
                recFolder = subDirectories.Find(x => x.Name.ToLower().Contains("2.id"));
            if (recFolder != null)
            {
                TbRecFolder = recFolder.FullName;
            }
            else
            {
                TbRecFolder = "";
            }
        }
        finally
        {
        }
    }
    private async void UpdateSeparationProgress()
    {

        if (SelectedSeparationFile == null)
        {
            ResumeSeparationFileComponentIsVisible = false;
            return;
        }


        //BtExportIsRunning = false;

        LabelPercentNotSeparated = "loading...";
        LabelPercentSeparated = "loading...";
        NotSeparatedRectHeight = 0;
        SeparatedRectHeight = 0;

        //if (CompanyWindow == null) return;
        //if (CompanyWindow.LesserFunctionClient == null) return;
        //if(ProfessionalTask == null) return;


        //gridHours.Visibility = Visibility.Collapsed;
        //SeparationProgressGrid.Visibility = Visibility.Collapsed;
        //BtReport.IsEnabled = false;
        //BtExport.IsEnabled = false;

        if (SelectedSeparationFile != null && SelectedSeparationFile.Creator != "")
        {
            if (SelectedSeparationFile.FileLocationType == ClassSeparationFile.FileLocationTypes.LOCAL)
            {
                if (File.Exists(SelectedSeparationFile.SeparationProgressFilePath))
                {
                    // checking if the file starts with '<' can be waived in future versions. This check was introduced because a version
                    // release in July 2022 caused the app to download metadata inside the SeparationProgressFilePath instead of the Json.
                    var str = File.ReadAllText(SelectedSeparationFile.SeparationProgressFilePath);
                    if (str.StartsWith("<"))
                        File.Delete(SelectedSeparationFile.SeparationProgressFilePath);
                    else
                        SeparationProgressValue = JsonConvert.DeserializeObject<SeparationProgress>(str);
                }
            }
            else
            {
                //Redundancia  necessária pois ha casos em que alternar rapidamente entre turmas faz com que haja um erro dentro do LesserFunctionClient.General.cs, arquivo que não é recomendável alterar por enquanto.
                //Acredito que o erro seja ocasionado porque a response chega depois que o objeto já foi destruído, ou seja, o objeto que chama o método já não existe mais.
                //Colocar qualquer tipo de trava para aguardar uma chamada acontecer prejudica a experiência do usuário para esse caso.
                //Para conservar a boa experiência do usuário, o melhor é tratar o erro internamente e deixar o programa seguir sem problemas ou avisos.
                try
                {
                    if (GlobalAppStateViewModel.lfc == null || SelectedCollection == null || SelectedSeparationFile == null)
                        return;

                    if (string.IsNullOrWhiteSpace(SelectedCollection.classCode) ||
                        string.IsNullOrWhiteSpace(SelectedCollection.companyUsername) ||
                        string.IsNullOrWhiteSpace(SelectedSeparationFile.Creator))
                        return;
                    SeparationProgressValue = await GlobalAppStateViewModel.lfc.getSeparationProgress(SelectedCollection.classCode, SelectedSeparationFile.Creator, SelectedCollection.companyUsername);
                }
                catch (Exception ex)
                {
                    SeparationProgressValue = null;
                    Console.WriteLine("Erro ao obter progresso de separação: " + ex.Message);
                }
            }
        }
        if (SeparationProgressValue == null)
            return;
        if (SelectedCollection.classCode != SeparationProgressValue.code)
            return;

        ResumeSeparationFileComponentIsVisible = SeparationProgressValue.totalPhotos == 0 ? false : true;


        //labelHours.Content = TranslationHelper.Default.TOTAL_HOURS_WORKING + SeparationProgress.totalHours.ToString("F1");
        //gridHours.Visibility = Visibility.Visible;

        var percentNotSeparated = (double)SeparationProgressValue.emptyPhotos / (double)SeparationProgressValue.totalPhotos;

        LabelPercentNotSeparated = SeparationProgressValue.emptyPhotos + " fotos (" + percentNotSeparated.ToString("P1") + ")";
        LabelPercentSeparated = (SeparationProgressValue.totalPhotos - SeparationProgressValue.emptyPhotos) + " fotos (" + (1 - percentNotSeparated).ToString("P1") + ")";


        const double totalGraphRectHeight = 100;
        NotSeparatedRectHeight = totalGraphRectHeight * percentNotSeparated;
        SeparatedRectHeight = totalGraphRectHeight * (1 - percentNotSeparated);

    }
    public void UpdateGraduateDataFromFile(FileInfo f)
    {
        GraduatesData.Clear();
        ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        var package = new ExcelPackage(f);

        ExcelWorksheet workSheet = package.Workbook.Worksheets[0];

        for (int i = 1;
                 i <= 1000000;
                 i++)
        {
            GraduateByCPF gradByCPF = new GraduateByCPF();

            var photoPathCell = workSheet.Cells[i, 1].Value;
            if (photoPathCell == null)
                break;

            if (photoPathCell.ToString() == TranslationHelper.Default.PHOTO_NAME)
                continue;

            var recFolder = TbRecFolder.Replace("\\", "/");
            var photoPath = photoPathCell.ToString().Replace("\\", "/");
            if (photoPath.StartsWith(recFolder))
                photoPath.Replace(recFolder, "");
            if (!File.Exists(recFolder + "/" + photoPath))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("O arquivo " + recFolder + "/" + photoPath + " não existe.");
                break;
            }
            gradByCPF.ShortPath = photoPath;

            var CPFCell = workSheet.Cells[i, 2].Value;
            if (CPFCell != null)
                gradByCPF.CPF = CPFCell.ToString();

            var emailCell = workSheet.Cells[i, 3].Value;
            if (emailCell != null)
                gradByCPF.Email = emailCell.ToString();

            var maxPhotosCell = workSheet.Cells[i, 4].Value;
            if (maxPhotosCell != null && (maxPhotosCell as string) != "")
                gradByCPF.MaxPhotos = int.Parse(StringHelper.RemoveAllCharactersButNumbers(maxPhotosCell.ToString()));

            var maxPhotosForTreatmentCell = workSheet.Cells[i, 5].Value;
            if (maxPhotosForTreatmentCell != null && (maxPhotosForTreatmentCell as string) != "")
                gradByCPF.MaxPhotosForTreatmentRequest = int.Parse(StringHelper.RemoveAllCharactersButNumbers(maxPhotosForTreatmentCell.ToString()));

            var blockedCellValue = workSheet.Cells[i, 6].Value ?? false;
            if (blockedCellValue == null || string.IsNullOrWhiteSpace(blockedCellValue.ToString()))
            {
                gradByCPF.Blocked = false;
            }
            else
            {
                var blockedCell = blockedCellValue.ToString().ToLower();
                if (blockedCell != null && (blockedCell as string) != "")
                {
                    var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "sim", "s", "v", "true", "t","verdadeiro","yes","y", "si","1"
                        };
                    var falseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "não","nao", "no", "not", "n", "false","f","ñ","falso","0"
                        };
                    if (trueValues.Contains(blockedCell))
                    {
                        gradByCPF.Blocked = true;
                    }
                    else if (falseValues.Contains(blockedCell))
                    {
                        gradByCPF.Blocked = false;
                    }
                }
            }
            var blockTypeCellValue = workSheet.Cells[i, 7].Value;
            if (blockTypeCellValue == null || string.IsNullOrWhiteSpace(blockTypeCellValue.ToString()))
            {
                gradByCPF.BlockType = "watermark";
            }
            else
            {
                var blockTypeCell = blockTypeCellValue.ToString().ToLower();
                if (blockTypeCell != null && (blockTypeCell as string) != "")
                {
                    var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "watermark","marca dagua","marca d'agua","marca d agua","sim", "s", "v", "true", "t","verdadeiro","yes","y", "si","1"
                        };
                    var falseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "acesso negado","acesso_negado","negado","access denied","access_denied","não", "no", "not", "n","nao", "false","f","ñ","falso","0"
                        };
                    if (trueValues.Contains(blockTypeCell))
                    {
                        gradByCPF.BlockType = "WATERMARK";
                    }
                    else if (falseValues.Contains(blockTypeCell))
                    {
                        gradByCPF.BlockType = "ACCESS_DENIED";
                    }
                }
            }
            GraduatesData.Add(gradByCPF);
        }
    }
    public async void GetInfosAboutFreeTrialPeriod()
    {
        try
        {
            if (GlobalAppStateViewModel.lfc != null)
            {
                RemainingFreeTrialPhotosResult = await GlobalAppStateViewModel.lfc.GetRemainingFreeTrialPhotos2();
            }
            return;
        }
        catch (Exception e)
        {
            return;
        }
    }
    #region RELAY COMMANDS
    [RelayCommand]
    public void BackLasViewCommand()
    {
        ActiveComponent = lastActiveComponent;
    }
    [RelayCommand]
    public void OpenQuickAccessViewCommand()
    {
        ActiveComponent = ActiveViews.QuickAccess;
    }

    [RelayCommand]
    public void OpenNewCollectionViewCommand()
    {
        IsReupload = false;
        CurrentProfessionalName = SelectedProfessional.username ?? GlobalAppStateViewModel.lfc.loginResult.User.company;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.NewCollection;

        TbCollectionName = GenerateDynamicClassCode();
        TbEventFolder = string.Empty;
        TbRecFolder = string.Empty;
        CbUploadedPhotosAreAlreadySorted = false;
        CbAllowCPFsToSeeAllPhotos = false;
        CbHDBackup = false;
        CbEnableAutoExclusion = true;
        CbEnablePhotoSales = false;
        TbPricePerPhotoForSellingOnline = 0;
        TbProfessioanlTaskDescription = string.Empty;
        CbEnableAutoTreatment = false;
        CbOcr = false;


        ExpanderAdvancedOptionsIsEnabled = true;
    }
    [RelayCommand]
    public void OpenNewCollectionPreConfiguredOnlySeparationCommand()
    {
        IsReupload = false;
        CurrentProfessionalName = SelectedProfessional.username ?? GlobalAppStateViewModel.lfc.loginResult.User.company;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.NewCollectionPreConfigured;

        TbCollectionName = GenerateDynamicClassCode();
        TbEventFolder = string.Empty;
        TbRecFolder = string.Empty;
        CbUploadedPhotosAreAlreadySorted = false;
        CbAllowCPFsToSeeAllPhotos = false;
        CbHDBackup = false;
        CbEnableAutoExclusion = true;
        CbEnablePhotoSales = false;
        TbPricePerPhotoForSellingOnline = 0;
        TbProfessioanlTaskDescription = string.Empty;
        CbEnableAutoTreatment = false;
        CbOcr = false;

        ExpanderAdvancedOptions = false;
        ExpanderAdvancedOptionsIsEnabled = false;
    }

    [RelayCommand]
    public void OpenNewCollectionPreConfigured(CollectionComboOptions options)
    {
        IsReupload = false;
        CurrentProfessionalName = SelectedProfessional.username ?? GlobalAppStateViewModel.lfc.loginResult.User.company;
        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.NewCollectionPreConfigured;
        TbCollectionName = GenerateDynamicClassCode();
        TbEventFolder = string.Empty;
        TbRecFolder = string.Empty;
        CbUploadedPhotosAreAlreadySorted = options.UploadedPhotosAreAlreadySorted;
        CbAllowCPFsToSeeAllPhotos = options.AllowCPFsToSeeAllPhotos;
        CbHDBackup = options.BackupHd;
        CbEnableAutoExclusion = true;
        CbEnablePhotoSales = options.EnablePhotoSales;
        TbPricePerPhotoForSellingOnline = 0;
        TbProfessioanlTaskDescription = string.Empty;
        CbEnableAutoTreatment = options.AutoTreatment;
        CbOcr = options.Ocr;
        CbAllowDeletedProductionToBeFoundAnyone = options.AllowDeletedProductionToBeFoundAnyone;

        ExpanderAdvancedOptions = true;
        ExpanderAdvancedOptionsIsEnabled = false;
    }
    [RelayCommand]
    public void OpenReuploadViewCommand()
    {
        if (SelectedCollection == null)
            return;


        CurrentProfessionalName = SelectedCollection.professionalLogin;

        ScrollComponentNewCollection = 0;
        ActiveComponent = ActiveViews.NewCollection;

        ExpanderAdvancedOptions = true;

        IsReupload = true;
        TbCollectionName = SelectedCollection.classCode;
        TbEventFolder = SelectedCollection.originalEventsFolder;
        TbRecFolder = SelectedCollection.originalRecFolder;
        CbUploadedPhotosAreAlreadySorted = SelectedCollection.UploadPhotosAreAlreadySorted;
        CbAllowCPFsToSeeAllPhotos = SelectedCollection.AllowCPFsToSeeAllPhotos;
        CbHDBackup = SelectedCollection.UploadHD ?? false;
        CbEnableAutoExclusion = SelectedCollection.EnableFaceRelevanceDetection;
        CbEnablePhotoSales = SelectedCollection.EnablePhotosSales ?? false;
        TbPricePerPhotoForSellingOnline = ConvertCentsToDecimal(SelectedCollection.PricePerPhotoForSellingOnlineInCents);
        TbProfessioanlTaskDescription = SelectedCollection.Description;
        CbEnableAutoTreatment = SelectedCollection.AutoTreatment ?? false;
        AutoTreatmentVersion = SelectedCollection.AutoTreatmentVersion;
        CbOcr = SelectedCollection.OCR ?? false;
        CbAllowDeletedProductionToBeFoundAnyone = SelectedCollection.AllowDeletedProductionToBeFoundAnyone ?? false;
        LoadGraduatesData(SelectedCollection);
    }
    [RelayCommand]
    public async Task OpenSelectProfessionalViewCommand()
    {
        ActiveComponent = ActiveViews.SelectProfessional;
        if(Professionals == null || Professionals.Count == 0)
            await LoadProfessionals();
        if (SelectedProfessional == null && Professionals != null && Professionals.Count > 0)
            SelectedProfessional = Professionals[0];
    }
    [RelayCommand]
    public void OpenCancelBillingViewCommand()
    {
        ActiveComponent = ActiveViews.CancelBilling;
    }
    [RelayCommand]
    public async Task CreateCollectionCommand()
    {
        string attempClassCode = TbCollectionName;
        try
        {



            IsCreatingCollection = true;
            if (!IsTextAllowed(TbCollectionName))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("The class code can only contain letters and numbers"));
                return;
            }
            if (CollectionCreationQueue.Contains(TbCollectionName))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("This class is already being created in the system."));
                return;

            }
            if (!Directory.Exists(TbRecFolder))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Acknowledgments folder not found"));
                return;
            }
            if (!Directory.Exists(TbEventFolder))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Events folder not found"));
                return;
            }
            if (CheckIfClassAlreadyExists(TbCollectionName))
            {
                var result = await GlobalAppStateViewModel.Instance.ShowDialogYesNo(Loc.Tr("This contract name already exists...", "This contract name already exists, do you want to update it?"));
                if(result != true)
                    return;
            }

            CollectionCreationQueue.Enqueue(TbCollectionName);
            ProfessionalTask pt = new ProfessionalTask()
            {
                professionalLogin = CurrentProfessionalName,
                originalClassFolder = GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles,
                classCode = TbCollectionName,
                UploadOnTestSystem = CbUploadOnTestSystem ?? false,
                companyUsername = GlobalAppStateViewModel.lfc.loginResult.User.company,
                originalEventsFolder = TbEventFolder,
                originalRecFolder = TbRecFolder,
                EnableFaceRelevanceDetection = CbEnableAutoExclusion,
                AutoTreatment = CbEnableAutoTreatment,
                UploadPhotosAreAlreadySorted = CbUploadedPhotosAreAlreadySorted,
                AllowCPFsToSeeAllPhotos = CbAllowCPFsToSeeAllPhotos,
                UploadHD = CbHDBackup,
                UploadComplete = false,
                Description = TbProfessionalTaskDescription,
                EnablePhotosSales = CbEnablePhotoSales,
                PricePerPhotoForSellingOnlineInCents = ConvertDecimalToCents(TbPricePerPhotoForSellingOnline),
                OCR = CbOcr,
                AllowDeletedProductionToBeFoundAnyone = CbAllowDeletedProductionToBeFoundAnyone,
                
            };
            if (CbEnableAutoTreatment == true)
            {
                pt.AutoTreatmentVersion = "2.0";
            }

            var eventFiles = FileHelper.GetFilesWithExtensionsAndFilters(pt.originalEventsFolder);
            var recFiles = FileHelper.GetFilesWithExtensionsAndFilters(pt.originalRecFolder);
            var checkIfClassCanBeCreated = CheckIfClassCanBeCreated(pt, eventFiles, recFiles);
            if (checkIfClassCanBeCreated.response == false)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(checkIfClassCanBeCreated.message);
            }

            var graduatesDataToUpload = GraduatesData.ToList();
            graduatesDataToUpload.RemoveAll(x => x.CPF == "" || x.CPF == null);

            if (graduatesDataToUpload.Count > 0)
            {
                pt.PhotosDistribution = true;
                // this is a double check. The UI already prevents this case from happening by setting the checkbox to true and disabling it when the GraduatesData collection changes.
                // Check GraduatesData_CollectionChanged
                pt.UploadHD = true; // in order to distribute photos with decent quality, UploadHD is set to true whenever there are CPFs.
            }
            else
            {
                pt.PhotosDistribution = false;
                //pt.UploadHD = false; -> this line was removed when implementing the change to let users make backups without CPFs.
            }


            var eventFilesShortPaths = eventFiles.Select(x => x.FullName.Substring(pt.originalEventsFolder.Length)).ToList();
            var recFilesShortPaths = recFiles.Select(x => x.FullName.Substring(pt.originalRecFolder.Length)).ToList();

            bool shouldNotifyPipedriveAboutFirstUse = false;
            bool shouldNotifyPipedriveAboutFreeTrial50PercentReached = false;
            bool shouldNotifyPipedriveAboutFreeTrialLimitReached = false;
            if (RemainingFreeTrialPhotosResult.IsFreeTrialActive)
            {
                //VERIFICA SE É O PRIMEIRO USO DO SISTEMA
                if(RemainingFreeTrialPhotosResult.IsFirstUse)
                    shouldNotifyPipedriveAboutFirstUse = true;

                int totalCollectionPhotos = recFilesShortPaths.Count + eventFilesShortPaths.Count;
                //VERIFICA SE A QUOTA DE TESTE ESTÁ PASSANDO DE 50%
                if(RemainingFreeTrialPhotosResult.HalfQuotaRemainingPhotos > 0 && totalCollectionPhotos > RemainingFreeTrialPhotosResult.HalfQuotaRemainingPhotos)
                    shouldNotifyPipedriveAboutFreeTrial50PercentReached = true;

                //VERIFICA SE A QUOTA DE TESTE ESTÁ SENDO EXCEDIDA
                if (RemainingFreeTrialPhotosResult.RemainingFreeTrialPhotos > 0 && totalCollectionPhotos > RemainingFreeTrialPhotosResult.RemainingFreeTrialPhotos)
                {
                    shouldNotifyPipedriveAboutFreeTrialLimitReached = true;
                    var dialog = await GlobalAppStateViewModel.Instance.ShowDialogYesNo("Você esgotou sua cota gratuita de fotos para teste. A partir de agora, todas as fotos adicionais desta turma e de turmas futuras estarão sujeitas a cobrança.", "Limite máximo de fotos gratuitas atingido.");
                    if (dialog != true)
                        return;
                }
            }

            var r = await GlobalAppStateViewModel.lfc.UpdateOrCreateProfessionalTaskAsync(pt, recFilesShortPaths, eventFilesShortPaths);
            if (r.success)
            {
                foreach (var g in graduatesDataToUpload)
                    g.ClassCode = pt.classCode;
                if (graduatesDataToUpload.Count > 0)
                    await GlobalAppStateViewModel.lfc.RegisterGraduatesCPFsAndEmails(graduatesDataToUpload);




                Action<int> callback = MainWindowViewModel.Instance != null
                    ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                    : _ => { };
                await App.StartUploadConcurrentApp(pt, callback);

                await UpdateProfessionalTasksList();
                var currentPt = CollectionsListFiltered.FirstOrDefault(x => x.classCode == pt.classCode);
                if (currentPt != null)
                    SelectedCollection = currentPt;
                ActiveComponent = ActiveViews.CollectionView;

                if(RemainingFreeTrialPhotosResult.IsFreeTrialActive == true)
                    GetInfosAboutFreeTrialPeriod();

            }
            else
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(r.message);
            }

            try
            {
                if (shouldNotifyPipedriveAboutFirstUse == true)
                    await GlobalAppStateViewModel.lfc.PipeDrive_NotifySystemFirstUse();
                if(shouldNotifyPipedriveAboutFreeTrial50PercentReached == true)
                    await GlobalAppStateViewModel.lfc.PipeDrive_HalfQuotaFreeTrialReached();
                if(shouldNotifyPipedriveAboutFreeTrialLimitReached == true)
                    await GlobalAppStateViewModel.lfc.PipeDrive_FullQuotaFreeTrialReached();
            }
            catch
            {
                return;
            }

        }
        catch(Exception ex)
        {
            //var bbox = MessageBoxManager
            //    .GetMessageBoxStandard("", $"{ex.Message} | {ex.StackTrace}");
            //var result = bbox.ShowWindowDialogAsync(MainWindow.instance);

            GlobalAppStateViewModel.Instance.ShowDialogOk($"{ex.Message}");
        }
        finally
        {
            CollectionCreationQueue.TryDequeue(out attempClassCode!); // ! instruction for silence compilator becase warning not null here
            IsCreatingCollection = false;
        }
    }
    [RelayCommand]
    public async Task TagSortCommand() 
    {
        try
        {
            BtTagSortIsRunning = true;
            if (SelectedSeparationFile != null)
            {
                BtTagSortIsEnabled = false;
                var sepFile = new FileInfo(SharedClientSide.Helpers.Constants.GetSaveFolder(SelectedCollection.classCode) + "/separacao.hermes");

                if (SelectedSeparationFile.FileLocationType == SharedClientSide.ClassSeparationFile.FileLocationTypes.CLOUD)
                {
                    if (sepFile.Exists)
                    {
                        var msgBoxResult = await GlobalAppStateViewModel.Instance.ShowDialogYesNo(Loc.Tr("You're about to open a file from the cloud...", Loc.Tr("Confirmation")));
                        if (msgBoxResult != true)
                            return;
                    }
                    var classSeparationFile = SelectedSeparationFile;

                    var ur = await LesserFunctionClient.DefaultClient.GetBlobBytesFromUserFolderInClassesContainer(classSeparationFile.FilePathInCloudCompanyFolder, classSeparationFile.StorageLocation);
                    if (ur.success)
                    {
                        var bytes = ur.Content;
                        if(sepFile.Directory == null)
                            return;
                        if (sepFile.Directory.Exists == false)
                            sepFile.Directory.Create();
                        SeparationFileSharedManagement.performBackup(SeparationFileSharedManagement.GetSeparationDocumentPath(SelectedCollection.classCode), SeparationFileSharedManagement.GetBackupsFolderPath(SelectedCollection.classCode));
                        File.WriteAllBytes(sepFile.FullName, bytes);
                    }

                    var ur2 = await LesserFunctionClient.DefaultClient.GetBlobBytesFromUserFolderInClassesContainer(classSeparationFile.SeparationProgressPathCloudCompanyFolder, classSeparationFile.StorageLocation);
                    if (ur2.success)
                    {
                        var localProgressFile = new FileInfo(SharedClientSide.Helpers.Constants.GetSaveFolder(SelectedCollection.classCode) + "/separationProgress.txt");
                        File.WriteAllBytes(localProgressFile.FullName, ur2.Content);
                    }
                }
            }
            Action<int> callback = MainWindowViewModel.Instance != null
                ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                : _ => { };
            await App.StartDownloadApp(SelectedCollection, callback);
        }
        catch(Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message + ex.StackTrace);
        }
        finally
        {
            await Task.Delay(500);
            BtTagSortIsEnabled = true;
            BtTagSortIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task ExportCommand()
    {
        try
        {
            BtExportIsRunning = true;
            BtExportIsEnabled = false;
            await Task.Delay(200);
            if (!Directory.Exists(AppInstaller.AppRootFolder))
                Directory.CreateDirectory(AppInstaller.AppRootFolder);
            File.WriteAllText(AppInstaller.ClassToExportTxtFilePath, JsonConvert.SerializeObject(SelectedCollection, Formatting.Indented));
            File.WriteAllText(AppInstaller.ClassSeparationFileTxtFilePath, JsonConvert.SerializeObject(SelectedSeparationFile, Formatting.Indented));

            Action<int> callback = MainWindowViewModel.Instance != null
                ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                : _ => { };
            await App.StartOrganizeApp(callback);
        }
        catch
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Não foi possível preencher os dados automaticamente. " +
                "Será necessário inserir manualmente o nome da pasta e inserir o arquivo separacao.hermes no local correto manualmente.");
        }
        finally
        {
            BtExportIsEnabled = true;
            BtExportIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task DownloadHDCommand()
    {
        try
        {
            BtDownloadHdIsEnabled = false;
            BtDownloadHdIsRunning = true;
            if (!Directory.Exists(AppInstaller.AppRootFolder))
                Directory.CreateDirectory(AppInstaller.AppRootFolder);
            File.WriteAllText(AppInstaller.ClassToDownloadHDTxtFilePath, JsonConvert.SerializeObject(SelectedCollection, Formatting.Indented));

            Action<int> callback = MainWindowViewModel.Instance != null
                ? MainWindowViewModel.Instance.UpdateProgressBarUpdateComponent
                : _ => { };
            AppInstaller ai = new AppInstaller("download-hd", callback);
            await ai.startApp();
        }
        catch (Exception ex)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(ex.Message + ex.StackTrace);
        }
        finally
        {
            BtDownloadHdIsEnabled = true;
            BtDownloadHdIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task ReEnqueueCommand()
    {
        try
        {
            BtReEnqueueIsEnabled = false;
            BtReenqueueIsRunning = true;
            var r = await GlobalAppStateViewModel.lfc.ReenqueueClassHttpTrigger(SelectedCollection.classCode);
            if (!string.IsNullOrEmpty(r.message))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(r.message);
            }
        }
        catch
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Ocorreu um erro ao colocar as imagens na fila para reconhecimento novamente.");
        }
        finally
        {
            BtReEnqueueIsEnabled = true;
            BtReenqueueIsRunning = false;
        }
    }
    [RelayCommand]
    public async Task RequestManualTreatmentAllCollectionCommand()
    {
        try
        {
            BtRequestManualTreatmentAllCollectionIsEnabled = false;
            BtRequestManualTreatmentAllCollectionIsRunning = true;
            bool dialog = await GlobalAppStateViewModel.Instance.ShowDialogYesNo("Tem certeza que deseja solicitar o tratamento manual de todas as imagens da sua coleção? \n Ação Irreversível", "Atenção");

            if (dialog == true)
            {
                var r = await GlobalAppStateViewModel.lfc.RequestTreatmentOfEntireCollection(SelectedCollection.classCode);
            }
        }
        catch
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk("Ocorreu um erro ao colocar as imagens na fila para reconhecimento novamente.");
        }
        finally
        {
            BtRequestManualTreatmentAllCollectionIsEnabled = true;
            BtRequestManualTreatmentAllCollectionIsRunning = false;
        }
    }
    [RelayCommand]
    public void InsertDataBasedOnRecFolderCommand()
    {
        if (Directory.Exists(TbRecFolder) == false)
        {
            //MessageBox.Show("A pasta de reconhecimentos especificada não existe.");
            return;
        }
        var gradPhotos = FileHelper.GetFilesWithExtensionsAndFilters(TbRecFolder);

        string getShortpathFromImagePath(string fullPath)
        {
            var shortPath = fullPath.Substring(TbRecFolder.Length, fullPath.Length - TbRecFolder.Length);
            shortPath = shortPath.Substring(1, shortPath.Length - 1);
            return shortPath;
        }

        foreach (var im in gradPhotos)
        {
            var shortPath = getShortpathFromImagePath(im.FullName);
            if (GraduatesData.ToList().Find(x => x.ShortPath.ToLower() == shortPath.ToLower()) != null)
                continue;
            GraduatesData.Add(new GraduateByCPF()
            {
                ShortPath = shortPath,
            });

        }

        for (var i = 0; i < GraduatesData.Count; i++)
        {
            var grad = GraduatesData[i];
            if (gradPhotos.Find(x => getShortpathFromImagePath(x.FullName).ToLower() == grad.ShortPath.ToLower()) == null)
            {
                GraduatesData.RemoveAt(i);
                i--;
            }
        }
    }
    [RelayCommand]
    public async Task GenerateExcelBasedOnDataCommand()
    {
        try
        {
            ComponentNewCollectionIsEnabled = false;
            if (!Directory.Exists(TbRecFolder))
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Recognition folder dosent exist"));
                return;
            }
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using (ExcelPackage excel = new ExcelPackage())
            {
                excel.Workbook.Worksheets.Add("Worksheet");
                var excelWorksheet = excel.Workbook.Worksheets["Worksheet"];

                List<string[]> cellsData = new List<string[]>()
                    {
                      new string[] { TranslationHelper.Default.PHOTO_NAME, TranslationHelper.Default.ID, "Email", TranslationHelper.Default.MAX_PHOTOS, TranslationHelper.Default.MAX_PHOTOS_FOR_TREATMENT, TranslationHelper.Default.BLOCKED, TranslationHelper.Default.BLOCK_MODE }
                    };
                string headerRange = "A1:" + Char.ConvertFromUtf32(cellsData[0].Length + 64) + "1";
                excelWorksheet.Cells[headerRange].Style.Font.Bold = true;
                excelWorksheet.Column(1).Width = 100;
                excelWorksheet.Column(2).Width = 30;
                excelWorksheet.Column(3).Width = 30;
                excelWorksheet.Column(4).Width = 30;
                //New Columns
                excelWorksheet.Column(5).Width = 30;
                excelWorksheet.Column(6).Width = 30;

                foreach (var g in GraduatesData)
                {
                    if (g.BlockType == null)
                    {
                        g.BlockType = "WATERMARK";
                    }
                    cellsData.Add(new string[] { g.ShortPath, g.CPF, g.Email, g.MaxPhotos.ToString(), g.MaxPhotosForTreatmentRequest.ToString(), g.Blocked.ToString(), g.BlockType.ToString() });
                }
                excelWorksheet.Cells[1, 1].LoadFromArrays(cellsData);

                var excelFile = new FileInfo(TbRecFolder + "/Excel/" +
                    DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + "-" + DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second + "-" + DateTime.Now.Millisecond + ".xlsx");

                try
                {
                    Directory.CreateDirectory(excelFile.Directory.FullName);

                }
                catch
                {
                    GlobalAppStateViewModel.Instance.ShowDialogOk("A pasta " + excelFile.Directory.FullName + "näo pôde ser criada. Verifique se está acessível.");
                    return;
                }
                excel.SaveAs(excelFile);

                UpdateGraduateDataFromFile(excelFile);

                var p = new System.Diagnostics.Process();
                p.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = excelFile.ToString(),
                    UseShellExecute = true // <- necessário para abrir com o aplicativo padrão
                };
                p.Start();

                await Task.Run(() => WaitForProcess(p));

                UpdateGraduateDataFromFile(excelFile);
            }
        }
        catch (Exception e) 
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(e.Message);
        }
        finally
        {
            ComponentNewCollectionIsEnabled = true;
        }
    }
    [RelayCommand]
    public void ToggleViewReportsContainer()
    {
        ReportsContainerIsVisible = !ReportsContainerIsVisible;
    }
    [RelayCommand]
    public async Task CancelBillingCommand()
    {
        try 
        {
            CancelBillingIsRunning = true;
            if (SelectedCollectionForCancelBilling == null)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Selecione uma item para para continuar.");
                return;
            }

            var repeatedClass = SelectedCollectionForCancelBilling.classCode;
            var result = await GlobalAppStateViewModel.lfc.CheckAndCancelBillingForRepeatedClass(SelectedCollection.classCode + "?" + repeatedClass);
            if (result.success == false)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk(result.message);
            }
            else
            {
                var pt = await GlobalAppStateViewModel.lfc.GetProfessionalTask(SelectedCollection.classCode);
                SelectedCollectionForCancelBilling.BillingCancelled = pt.BillingCancelled;
                FilterProfessionalTasks("","");
                GlobalAppStateViewModel.Instance.ShowDialogOk("Cobrança do contrato cancelada com sucesso.");
                await OpenSelectProfessionalViewCommand();
            }

        }
        finally
        {
            CancelBillingIsRunning = false;
        }
    }
    [RelayCommand]
    public void GeneralReportCommand()
    {
        try
        {
            GeneratingGeneralReport = true;
            if (SeparationProgressValue == null)
            {
                GlobalAppStateViewModel.Instance.ShowDialogOk("Não foram encontrados dados no servidor sobre esta turma, ou houve problema na conexão. O profissional já separou esta turma?");
                return;
            }
            var photographers = SeparationProgressValue.photographers;
            var stringList = new List<string>() { "Dados sobre a turma " + SeparationProgressValue.code, "Fotógrafo,Total de fotos,Fotos aproveitadas" };
            foreach (Photographer p in photographers)
            {
                stringList.Add(p.name + ";" + p.totalPhotos + ";" + p.totalPhotosUtilized);
            }
            string text = string.Join("\r\n", stringList);
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            string fileName = System.IO.Path.GetTempFileName() + ".csv";

            File.WriteAllBytes(fileName, bytes);

            var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true // <- necessário para abrir com o aplicativo padrão
            };
            proc.Start();
        }
        finally
        {
            GeneratingGeneralReport = false;
        }
    }
    [RelayCommand]
    public async Task ReportPerGraduatesCommand()
    {
        try
        {
            GeneratingReportPerGraduate = true;
            var lfc = GlobalAppStateViewModel.lfc;

            string fileAddress = await lfc.GetClassProgressFileAddress(SelectedCollection.classCode, "separacao.hermes");

            using (WebClient wc = new WebClient())
            {
                wc.DownloadDataAsync(new Uri(fileAddress));

                wc.DownloadDataCompleted += (s, ev) =>
                {
                    string str = Encoding.UTF8.GetString(ev.Result);
                    var lines = str.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();


                    List<Graduate> graduates = new List<Graduate>();
                    bool success; string message;
                    var photosLoad = new PhotosInfoLoad();
                    (success, message) = photosLoad.loadGraduatesAndPhotosInfoFromStringList(lines, "", graduates, true);

                    var reports = new Dictionary<string, GraduateReportInfo>();
                    foreach (Graduate g in graduates)
                    {
                        if (g.Deleted)
                            continue;
                        if (!reports.ContainsKey(g.Name)) // in case the user somehow created two graduates with the same name. This has happened before.
                            reports.Add(g.Name, new GraduateReportInfo() { Name = g.Name });
                    }
                    foreach (PhotoInfo pi in photosLoad.photosInfo)
                    {
                        foreach (Tag tag in pi.tags)
                        {
                            if (!reports.ContainsKey(tag.graduate.Name))
                                continue;
                            if (tag.color == "AZUL")
                                reports[tag.graduate.Name].BlueTags++;
                            else
                                reports[tag.graduate.Name].RedTags++;
                        }
                    }
                    var file = new FileInfo(System.IO.Path.GetTempFileName() + ".xlsx");
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (ExcelPackage package = new ExcelPackage())
                    {
                        ExcelWorksheet ws = package.Workbook.Worksheets.Add(Loc.Tr("Report"));
                        ws.SetValue(1, 1, Loc.Tr("Name"));
                        ws.SetValue(1, 2, Loc.Tr("Approved"));
                        ws.SetValue(1, 3, Loc.Tr("Rejected"));
                        ws.SetValue(1, 4, Loc.Tr("Total"));
                        int line = 1;
                        foreach (var g in reports)
                        {

                            line++;
                            var r = g.Value;
                            ws.SetValue(line, 1, r.Name);
                            ws.SetValue(line, 2, r.BlueTags);
                            ws.SetValue(line, 3, r.RedTags);
                            ws.SetValue(line, 4, r.TotalTags);
                        }
                        package.SaveAs(file);

                        var p = new System.Diagnostics.Process();
                        p.StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = file.FullName,
                            UseShellExecute = true // <- necessário para abrir com o aplicativo padrão
                        };
                        p.Start();
                    }
                };
            }
        }
        catch (Exception exception)
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(exception.Message);
        }
        finally
        {
            GeneratingReportPerGraduate = false;
        }
    }
    [RelayCommand]
    public void OpenLinkToUpgradePlanCommand()
    {
        try
        {
            var p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.lesser.biz/lesser-system-helpers/pricing-page/index.html",
                UseShellExecute = true // <- necessário para abrir com o aplicativo padrão
            };
            p.Start();
        }
        catch { }
    }
    #endregion

    #region Dynamic Pricing

    /// <summary>
    /// Carrega preços dinâmicos para todos os combos
    /// </summary>
    private async void LoadDynamicPrices()
    {
        try
        {
            Console.WriteLine("CollectionsViewModel: Iniciando LoadDynamicPrices");
            
            // Verificar se o cliente está inicializado
            if (GlobalAppStateViewModel.lfc == null)
            {
                Console.WriteLine("CollectionsViewModel: GlobalAppStateViewModel.lfc não está inicializado, aguardando...");
                // Aguardar um pouco e tentar novamente
                await Task.Delay(2000);
                if (GlobalAppStateViewModel.lfc == null)
                {
                    Console.WriteLine("CollectionsViewModel: GlobalAppStateViewModel.lfc ainda não está inicializado, usando preços estáticos");
                    return;
                }
            }
            
            // Array com todos os combos disponíveis
            var combos = new[]
            {
                NewCollection_Combo0,
                NewCollection_Combo1,
                NewCollection_Combo2,
                NewCollection_Combo3,
                NewCollection_Combo4,
                NewCollection_Combo5,
                NewCollection_Combo6
            };

            Console.WriteLine($"CollectionsViewModel: Carregando preços para {combos.Length} combos");
            
            // Log dos preços antes do carregamento
            foreach (var combo in combos)
            {
                Console.WriteLine($"CollectionsViewModel: Preço ANTES do carregamento para '{combo.ComboTitle}': {combo.ComboPrice:F4}");
            }

            // Carregar preços dinâmicos
            await ComboPriceService.UpdateCombosWithDynamicPricesAsync(combos);
            
            // Log dos preços depois do carregamento
            foreach (var combo in combos)
            {
                Console.WriteLine($"CollectionsViewModel: Preço DEPOIS do carregamento para '{combo.ComboTitle}': {combo.ComboPrice:F4}");
            }
            
            // Notificar mudanças na UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Console.WriteLine("CollectionsViewModel: Notificando mudanças na UI");
                // Forçar atualização das propriedades de preço
                OnPropertyChanged(nameof(NewCollection_Combo0));
                OnPropertyChanged(nameof(NewCollection_Combo1));
                OnPropertyChanged(nameof(NewCollection_Combo2));
                OnPropertyChanged(nameof(NewCollection_Combo3));
                OnPropertyChanged(nameof(NewCollection_Combo4));
                OnPropertyChanged(nameof(NewCollection_Combo5));
                OnPropertyChanged(nameof(NewCollection_Combo6));
                Console.WriteLine("CollectionsViewModel: Mudanças notificadas na UI");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar preços dinâmicos: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            // Em caso de erro, os combos continuarão usando os preços estáticos
        }
    }

    /// <summary>
    /// Recarrega os preços dinâmicos (útil para refresh manual)
    /// </summary>
    [RelayCommand]
    public void RefreshPrices()
    {
        ComboPriceService.ClearCache();
        LoadDynamicPrices();
    }

    #endregion
}



