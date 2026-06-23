using QMSFlowDoc.Shared.DTOs;

namespace QMSFlowDoc.Application.Services.Folders;

/// <summary>
/// Interfaz para el servicio de gestión de carpetas del explorador documental
/// </summary>
public interface IFolderService
{
    /// <summary>
    /// Obtiene las carpetas hijas de un directorio padre (o raíz si parentId es nulo).
    /// </summary>
    Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid? parentId = null);

    /// <summary>
    /// Obtiene todas las carpetas registradas.
    /// </summary>
    Task<IEnumerable<FolderDto>> GetAllFoldersAsync();

    /// <summary>
    /// Crea una nueva carpeta en la base de datos.
    /// </summary>
    Task<bool> CreateFolderAsync(string name, Guid? parentId = null);

    /// <summary>
    /// Cambia el nombre y/o la carpeta superior de una carpeta existente.
    /// </summary>
    Task<bool> RenameFolderAsync(Guid id, string newName, Guid? parentId = null);

    /// <summary>
    /// Elimina una carpeta si está vacía.
    /// </summary>
    Task<bool> DeleteFolderAsync(Guid id);

    /// <summary>
    /// Mueve una carpeta una posición hacia arriba en el orden de visualización de sus hermanos.
    /// </summary>
    Task<bool> MoveFolderUpAsync(Guid id);

    /// <summary>
    /// Mueve una carpeta una posición hacia abajo en el orden de visualización de sus hermanos.
    /// </summary>
    Task<bool> MoveFolderDownAsync(Guid id);
}
