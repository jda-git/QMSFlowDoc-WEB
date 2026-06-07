using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Shared.DTOs;

namespace QMSFlowDoc.Application.Services.Documents;

/// <summary>
/// Interfaz para el servicio de gestión de documentos (ISO 15189)
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Obtiene la lista de documentos activos.
    /// </summary>
    Task<IEnumerable<DocumentDto>> GetDocumentsAsync(bool includeObsolete = false);

    /// <summary>
    /// Obtiene los tipos de documentos configurados.
    /// </summary>
    Task<IEnumerable<DocumentType>> GetDocumentTypesAsync();

    /// <summary>
    /// Obtiene un documento por su identificador único.
    /// </summary>
    Task<Document?> GetDocumentByIdAsync(Guid id);

    /// <summary>
    /// Crea un nuevo registro de documento sin archivo asociado.
    /// </summary>
    Task<Document?> CreateDocumentAsync(CreateDocumentRequest request, Guid? ownerUserId);

    /// <summary>
    /// Crea un documento con su versión inicial y archivo físico en una operación consistente.
    /// </summary>
    Task<Document?> CreateDocumentWithInitialFileAsync(
        CreateDocumentRequest request,
        byte[] fileData,
        string fileName,
        string contentType,
        Guid? ownerUserId,
        string username);

    /// <summary>
    /// Cambia el estado de aprobación de un documento.
    /// </summary>
    Task<bool> UpdateStatusAsync(Guid id, DocumentStatus newStatus, string comments, Guid? userId, string username, string? confirmPassword = null);

    /// <summary>
    /// Actualiza los metadatos de un documento existente.
    /// </summary>
    Task<bool> UpdateDocumentAsync(Guid id, CreateDocumentRequest request, Guid? userId, string username);

    /// <summary>
    /// Elimina un documento (borrado lógico).
    /// </summary>
    Task<bool> DeleteDocumentAsync(Guid id, Guid? userId, string username);

    /// <summary>
    /// Sube un archivo físico y crea una nueva versión de documento asociada.
    /// </summary>
    Task<bool> UploadFileAsync(Guid id, byte[] fileData, string fileName, string contentType, string versionLabel, string changeSummary, Guid? userId, string username);

    /// <summary>
    /// Obtiene los bytes del archivo físico de la versión actual.
    /// </summary>
    Task<byte[]?> GetFileContentAsync(Guid id);

    /// <summary>
    /// Busca una carpeta por nombre o la crea si no existe.
    /// </summary>
    Task<Guid?> GetOrCreateFolderIdAsync(string folderName);
}
