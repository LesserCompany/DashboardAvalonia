namespace LesserDashboardClient.Helpers;

/// <summary>
/// Versão de reconhecimento facial enviada na criação/atualização de coleção.
/// A UI não oferece 1.0 vs 2.0 pro usuário: este único valor decide pra todo mundo.
///
/// [FIX DE EMERGÊNCIA — 2026-09-22, PEDIDO DO USUÁRIO] Temporariamente "1.0" em vez de "2.0": a
/// calibração do V2 (SFace/deepface-service) contra o V1 não fechou (ver
/// C:\Users\claud\Desktop\calibrarv2\PIPELINE_MAP.md, seções 6/7). Com RecognitionVersion="1.0",
/// SS_FaceServicesVMCoordinatorV2 desvia pro serviço V1 (Facenet+FaceRecognition, Cloud Run) em vez
/// do deepface-service, continuando 100% no Postgres — ver RunFaceServicesV1AndSaveResultsToPostgres
/// (SharedServerSideClassLibrary/Storage/StorageOperationsNamespace/StorageOperations.UploadPhotoV3.cs)
/// e o branch em SS_FaceServicesVMCoordinatorV2/Program.cs (RunPTEPostgres). Reverter pra "2.0" assim
/// que o V2 estiver calibrado de verdade.
/// </summary>
public static class CollectionRecognitionVersion
{
    public const string Current = "1.0";
}
