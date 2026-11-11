using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Testing.Contracts;

namespace ExxerCube.Prisma.Tests.Infrastructure.Extraction;

/// <summary>
/// Implementation instance of <see cref="FileTypeIdentifierContract"/> for the production
/// <see cref="FileTypeIdentifierService"/> (ADR-005 §7 worked example, converted in Phase 6).
/// </summary>
/// <remarks>
/// Uses the ADR-005 §3 default injected-<c>Sut</c> mechanism (Example B — service with a logger).
/// The 6 contract <c>[Fact]</c>s (null/empty content, PDF magic-number, extension fallback, and the
/// pre-cancelled-token Cancelled behaviour) now run against the real service — the cancellation test
/// passes since Phase 6 added the <c>IsCancellationRequested</c> early-return to the service.
/// Signature-detection specifics beyond the universal PDF magic-number case (XML/DOCX/ZIP content
/// detection) stay in <see cref="FileTypeIdentifierServiceTests"/> per the contract's scope rule.
/// </remarks>
public sealed class FileTypeIdentifierServiceContractTests : FileTypeIdentifierContract
{
    public FileTypeIdentifierServiceContractTests()
        : base(new FileTypeIdentifierService(Substitute.For<ILogger<FileTypeIdentifierService>>()))
    {
    }
}
