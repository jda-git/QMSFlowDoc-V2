using QMSFlowDoc.Domain.Entities;

namespace QMSFlowDoc.Application.Services.Documents;

/// <summary>
/// Servicio para añadir marcas de agua a documentos PDF para cumplimiento ISO 15189
/// </summary>
public interface IPdfWatermarkService
{
    /// <summary>
    /// Añade marca de agua diagonal a todas las páginas del PDF
    /// </summary>
    Task<byte[]> AddWatermarkAsync(byte[] pdfBytes, WatermarkType type, string? metadata = null);

    /// <summary>
    /// Añade Copy ID al pie de página de todas las páginas
    /// </summary>
    Task<byte[]> AddCopyIdAsync(byte[] pdfBytes, string copyId, DateTime printedAt, string username);

    /// <summary>
    /// Prepara el documento para previsualización en pantalla (ISO: CONTROLADO)
    /// </summary>
    Task<byte[]> PrepareForScreenViewAsync(byte[] pdfBytes, string docId, string version, string status, DateTime? nextReview);

    /// <summary>
    /// Prepara el documento para exportación/impresión (ISO: NO CONTROLADO)
    /// </summary>
    Task<byte[]> PrepareForExportAsync(byte[] pdfBytes, string version, DateTime printedAt);
}
