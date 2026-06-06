using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Application.Services.Documents;

/// <summary>
/// Define las reglas y transiciones permitidas para el ciclo de vida de los documentos (ISO 15189).
/// </summary>
public static class DocumentWorkflow
{
    /// <summary>
    /// Verifica si una transición de estado es válida según la máquina de estados.
    /// </summary>
    public static bool CanTransition(DocumentStatus from, DocumentStatus to)
    {
        if (from == to) return true;

        return from switch
        {
            DocumentStatus.DRAFT => to == DocumentStatus.REVIEW || to == DocumentStatus.OBSOLETE || to == DocumentStatus.RETIRED,
            DocumentStatus.REVIEW => to == DocumentStatus.DRAFT || to == DocumentStatus.APPROVED || to == DocumentStatus.OBSOLETE || to == DocumentStatus.RETIRED,
            DocumentStatus.APPROVED => to == DocumentStatus.OBSOLETE || to == DocumentStatus.RETIRED,
            DocumentStatus.OBSOLETE => false, // Estado terminal
            DocumentStatus.RETIRED => false,  // Estado terminal
            _ => false
        };
    }

    /// <summary>
    /// Obtiene un mensaje descriptivo del error de transición.
    /// </summary>
    public static string GetTransitionError(DocumentStatus from, DocumentStatus to)
    {
        return $"Transición no permitida: no se puede cambiar el estado de {GetStatusLabel(from)} a {GetStatusLabel(to)}.";
    }

    private static string GetStatusLabel(DocumentStatus status)
    {
        return status switch
        {
            DocumentStatus.DRAFT => "Borrador (DRAFT)",
            DocumentStatus.REVIEW => "En Revisión (REVIEW)",
            DocumentStatus.APPROVED => "Vigente (APPROVED)",
            DocumentStatus.OBSOLETE => "Obsoleto (OBSOLETE)",
            DocumentStatus.RETIRED => "Retirado (RETIRED)",
            _ => status.ToString()
        };
    }
}
