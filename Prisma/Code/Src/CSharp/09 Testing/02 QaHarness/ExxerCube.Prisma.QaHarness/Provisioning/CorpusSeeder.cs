// <copyright file="CorpusSeeder.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Seeds the document corpus used by QA workflows.
/// </summary>
/// <remarks>
/// The seeder applies the following priority chain:
/// <list type="number">
///   <item><description>If the SIARA corpus directory already exists and is non-empty → <see cref="CorpusStatus.Seeded"/>.</description></item>
///   <item><description>Try the Python document generator (<c>generate_corpus.py</c>) → <see cref="CorpusStatus.Seeded"/> on success, <see cref="CorpusStatus.AbsentGeneratorFailed"/> on failure.</description></item>
///   <item><description>Fall back to static <c>Prisma/Code/Fixtures/</c> directory → <see cref="CorpusStatus.RestoredFromFixtures"/>.</description></item>
///   <item><description>Nothing available → <see cref="CorpusStatus.AbsentNoGenerator"/>.</description></item>
/// </list>
/// Never throws — every path returns a status.
/// </remarks>
public sealed class CorpusSeeder
{
    private readonly ILogger<CorpusSeeder> _logger;

    /// <summary>Relative path (from repo root) to the SIARA simulator corpus directory.</summary>
    private const string SiaraCorpusRelPath = @"Prisma\Deployments\Siara.Simulator\bulk_generated_documents_all_formats";

    /// <summary>Relative path (from repo root) to the Python generator script.</summary>
    private const string GeneratorRelPath = @"Prisma\Code\Src\Python\Prisma-dumy-generator-AAA\generate_corpus.py";

    /// <summary>Relative path (from repo root) to static fixtures.</summary>
    private const string FixturesRelPath = @"Prisma\Code\Fixtures";

    /// <summary>Timeout in milliseconds for the Python generator process.</summary>
    private const int GeneratorTimeoutMs = 120_000;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorpusSeeder"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public CorpusSeeder(ILogger<CorpusSeeder> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Ensures a document corpus is available and reports the seeding outcome.
    /// </summary>
    /// <param name="repoRoot">Absolute path to the repository root directory.</param>
    /// <param name="outputPath">
    /// Override for the output directory; when <see langword="null"/> the default SIARA corpus
    /// directory is used.
    /// </param>
    /// <param name="documentCount">Target number of documents for the Python generator.</param>
    /// <param name="cancellationToken">Token used to cancel the seeding operation.</param>
    /// <returns>
    /// A tuple of (<see cref="CorpusStatus"/>, <c>corpusPath</c>) where <c>corpusPath</c> is the
    /// absolute path to the directory containing the seeded/restored corpus.
    /// Never throws.
    /// </returns>
    public async Task<(CorpusStatus Status, string CorpusPath)> SeedAsync(
        string repoRoot,
        string? outputPath = null,
        int documentCount = 10,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            var fallback = GetFallbackPath(repoRoot, outputPath);
            return (CorpusStatus.AbsentNoGenerator, fallback);
        }

        try
        {
            // 1 — Check if the SIARA corpus dir already exists and is non-empty.
            var siaraDir = outputPath ?? Path.Combine(repoRoot, SiaraCorpusRelPath);
            if (DirectoryIsNonEmpty(siaraDir))
            {
                _logger.LogInformation("SIARA corpus directory exists and is non-empty at {CorpusPath}.", siaraDir);
                return (CorpusStatus.Seeded, siaraDir);
            }

            // 2 — Try the Python generator.
            var generatorScript = Path.Combine(repoRoot, GeneratorRelPath);
            if (File.Exists(generatorScript))
            {
                var pythonExe = LocatePython();
                if (pythonExe is not null)
                {
                    _logger.LogInformation(
                        "Corpus absent; running Python generator {Script} with count={Count}.",
                        generatorScript, documentCount);

                    Directory.CreateDirectory(siaraDir);
                    var success = await RunPythonGeneratorAsync(
                        pythonExe, generatorScript, siaraDir, documentCount, cancellationToken)
                        .ConfigureAwait(false);

                    if (success && DirectoryIsNonEmpty(siaraDir))
                    {
                        _logger.LogInformation("Python generator succeeded; corpus at {CorpusPath}.", siaraDir);
                        return (CorpusStatus.Seeded, siaraDir);
                    }

                    _logger.LogWarning("Python generator ran but produced no documents.");
                    return (CorpusStatus.AbsentGeneratorFailed, siaraDir);
                }

                _logger.LogWarning("generate_corpus.py found but Python interpreter not located.");
            }
            else
            {
                _logger.LogDebug("Python generator script not found at {Script}.", generatorScript);
            }

            // 3 — Fall back to static Fixtures/.
            var fixturesDir = Path.Combine(repoRoot, FixturesRelPath);
            if (DirectoryIsNonEmpty(fixturesDir))
            {
                _logger.LogInformation("Falling back to static Fixtures at {FixturesPath}.", fixturesDir);
                return (CorpusStatus.RestoredFromFixtures, fixturesDir);
            }

            // 4 — Nothing available.
            _logger.LogWarning("No corpus source available; SIARA workflows will be skipped.");
            return (CorpusStatus.AbsentNoGenerator, siaraDir);
        }
        catch (OperationCanceledException)
        {
            var fallback = GetFallbackPath(repoRoot, outputPath);
            return (CorpusStatus.AbsentNoGenerator, fallback);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CorpusSeeder encountered an unexpected error; treating as AbsentNoGenerator.");
            var fallback = GetFallbackPath(repoRoot, outputPath);
            return (CorpusStatus.AbsentNoGenerator, fallback);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static bool DirectoryIsNonEmpty(string path)
    {
        try
        {
            return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    private static string GetFallbackPath(string repoRoot, string? outputPath) =>
        outputPath ?? Path.Combine(repoRoot, SiaraCorpusRelPath);

    private static string? LocatePython()
    {
        foreach (var candidate in new[] { "python3", "python", "py" })
        {
            try
            {
                using var probe = new Process();
                probe.StartInfo = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                probe.Start();
                probe.WaitForExit(3_000);
                if (probe.ExitCode == 0)
                {
                    return candidate;
                }
            }
            catch
            {
                // Not found — try next.
            }
        }

        return null;
    }

    private async Task<bool> RunPythonGeneratorAsync(
        string pythonExe,
        string scriptPath,
        string outputDir,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            // generate_corpus.py argparse definition (verified against script source):
            //   --num   INT   number of records to generate (default: 5)
            //   --output STR  output JSON file path (default: "test_corpus.json")
            // The script writes a single JSON file; the output argument is a FILE path,
            // not a directory. We place the JSON inside the caller-supplied outputDir.
            var outputJsonPath = Path.Combine(outputDir, "corpus.json");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\" --output \"{outputJsonPath}\" --num {count}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(GeneratorTimeoutMs);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                _logger.LogWarning("Python generator was cancelled or timed out.");
                return false;
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Python generator exited {ExitCode}: {Stderr}", process.ExitCode, stderr.Trim());
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python generator process failed to start or run.");
            return false;
        }
    }
}
