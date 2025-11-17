using ExxerCube.Prisma.Domain.Entities;

namespace ExxerCube.Prisma.Testing.Infrastructure.TestData;

/// <summary>
/// Generates test image data for integration tests.
/// </summary>
public static class TestImageDataGenerator
{
    /// <summary>
    /// Creates test image data from a text file for testing purposes.
    /// </summary>
    /// <param name="fileName">The name of the test file.</param>
    /// <returns>Image data suitable for testing.</returns>
    public static ImageData CreateFromTextFile(string fileName)
    {
        var testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        var filePath = Path.Combine(testDataPath, fileName);
        
        if (!File.Exists(filePath))
        {
            // Create a default test file if it doesn't exist
            var defaultContent = GetDefaultTestContent();
            File.WriteAllText(filePath, defaultContent);
        }
        
        var fileContent = File.ReadAllText(filePath);
        var bytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
        
        return new ImageData(
            data: bytes,
            sourcePath: filePath,
            pageNumber: 1,
            totalPages: 1
        );
    }
    
    /// <summary>
    /// Creates simple test image data for basic testing.
    /// </summary>
    /// <returns>Simple image data for testing.</returns>
    public static ImageData CreateSimpleTestData()
    {
        var testContent = GetDefaultTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(testContent);
        
        return new ImageData(
            data: bytes,
            sourcePath: "test_document.txt",
            pageNumber: 1,
            totalPages: 1
        );
    }
    
    /// <summary>
    /// Creates large test image data for performance testing.
    /// </summary>
    /// <returns>Large image data for performance testing.</returns>
    public static ImageData CreateLargeTestData()
    {
        var largeContent = GenerateLargeTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(largeContent);
        
        return new ImageData(
            data: bytes,
            sourcePath: "large_test_document.txt",
            pageNumber: 1,
            totalPages: 1
        );
    }
    
    private static string GetDefaultTestContent()
    {
        return @"EXPEDIENTE: 2024/001234

CAUSA: DEMANDA DE PAGO DE DEUDA MERCANTIL

ACCION SOLICITADA: Se solicita el pago de la cantidad de $75,000.00 (SETENTA Y CINCO MIL PESOS 00/100 M.N.) por concepto de deuda contraída el día 20 de marzo de 2024, más intereses moratorios y costas.

FECHA: 20 de marzo de 2024

MONTO: $75,000.00 MXN

DESCRIPCION: El demandado contrajo una deuda mercantil por la compra de mercancías, comprometiéndose a pagar la cantidad de $75,000.00 MXN en un plazo de 30 días. A la fecha, han transcurrido 90 días sin que se haya realizado el pago correspondiente.

FECHAS ADICIONALES:
- Fecha de vencimiento: 19 de abril de 2024
- Fecha de notificación: 15 de mayo de 2024
- Fecha de presentación: 10 de junio de 2024

MONTOS ADICIONALES:
- Intereses moratorios: $5,250.00 MXN
- Gastos de cobranza: $2,500.00 MXN
- Total adeudado: $82,750.00 MXN";
    }
    
    private static string GenerateLargeTestContent()
    {
        var baseContent = GetDefaultTestContent();
        var largeContent = baseContent;
        
        // Add more content to make it larger for performance testing
        for (int i = 1; i <= 10; i++)
        {
            largeContent += $@"

PAGINA ADICIONAL {i}:
Este es contenido adicional para simular un documento más grande y complejo.
Contiene información adicional que debe ser procesada por el sistema de OCR.
La extracción de campos debe funcionar correctamente incluso con documentos extensos.

FECHA ADICIONAL {i}: {DateTime.Now.AddDays(i):dd/MM/yyyy}
MONTO ADICIONAL {i}: ${1000 * i:N2} MXN";
        }
        
        return largeContent;
    }
    
    private static string GetFormatFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "jpeg",
            ".png" => "png",
            ".pdf" => "pdf",
            ".txt" => "text",
            _ => "unknown"
        };
    }
}

