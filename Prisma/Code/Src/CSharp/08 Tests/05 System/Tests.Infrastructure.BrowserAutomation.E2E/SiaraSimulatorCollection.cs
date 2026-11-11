namespace ExxerCube.Prisma.Tests.System.BrowserAutomation.E2E;

/// <summary>
/// xUnit collection that serializes the SIARA-simulator E2E classes. Both the downloader and the
/// document-source E2E manage a simulator on the same port (5001) — starting one if none is running and
/// killing it on teardown. Running them in parallel would race on the port and risk one class tearing down
/// the simulator the other is still using, so they share this collection to run sequentially.
/// </summary>
[CollectionDefinition("SiaraSimulator")]
public sealed class SiaraSimulatorCollection
{
}
