using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Domain.Tolerances;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Read-only port for loading the legally-mandated tolerance specifications from the
/// encrypted legal-baseline SQL store.
/// </summary>
/// <remarks>
/// <para>
/// This interface exposes ONLY read operations. The production SQL role for the Veriqan
/// DB login should be <b>db_datareader</b> (read-only); no write permission is granted.
/// Additionally, SQL Server Transparent Data Encryption (TDE) should be enabled on the
/// database as an OPS-provisioning step — this is documented here for completeness and is
/// NOT enforced by this code. See ADR on legal-baseline store provisioning.
/// </para>
/// <para>
/// The implementation (<c>SqlLegalBaselineStore</c>) decrypts each column value via an
/// AES value converter at the EF Core layer; the raw bytes in the column are ciphertext.
/// </para>
/// </remarks>
public interface ILegalBaselineStore
{
    /// <summary>
    /// Loads all legal-baseline <see cref="Tolerance"/> records from the encrypted store.
    /// </summary>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <returns>
    /// A read-only dictionary keyed by check ID (e.g. <c>"CL-10"</c>), containing the
    /// decrypted <see cref="Tolerance"/> specification for each rule that has a legal-default
    /// tolerance value.
    /// </returns>
    Task<IReadOnlyDictionary<string, Tolerance>> LoadTolerancesAsync(
        CancellationToken cancellationToken = default);
}
