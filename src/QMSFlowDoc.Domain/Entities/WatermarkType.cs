namespace QMSFlowDoc.Domain.Entities;

/// <summary>
/// Tipos de marca de agua para cumplimiento ISO 15189
/// </summary>
public enum WatermarkType
{
    Controlled,      // "CONTROLADO" - verde (vista en pantalla)
    Uncontrolled,    // "NO CONTROLADO" - gris (copias impresas/exportadas)
    Obsolete,        // "OBSOLETO" - rojo
    Draft            // "BORRADOR" - amarillo
}
