using System.Buffers;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Domain.Serialization;
using ExxerCube.Prisma.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Orion.Worker.Tests;

/// <summary>
/// Faithful reproduction of the 2026-06-24 gate stall at the exact layer that fails: the SignalR
/// <see cref="JsonHubProtocol"/> envelope. Both the Orion hub (sender) and the Athena ingestion client
/// (receiver) configure <c>AddJsonProtocol(... EnumModelJsonConverterFactory)</c>, so this writes an
/// <see cref="InvocationMessage"/>("ReceiveMessage", event) with those exact options and parses it back —
/// no host, no transport, no flakiness. A plain System.Text.Json round-trip of the same event already
/// passes, so any failure here is specific to how the hub protocol binds the populated CaseFiles argument.
/// </summary>
public sealed class JsonHubProtocolCaseFilesTests
{
    private static JsonHubProtocol BuildProtocol()
    {
        var options = new JsonHubProtocolOptions();
        options.PayloadSerializerOptions.Converters.Add(new EnumModelJsonConverterFactory());
        return new JsonHubProtocol(Options.Create(options));
    }

    private sealed class SingleArgBinder : IInvocationBinder
    {
        private readonly Type _argType;
        public SingleArgBinder(Type argType) => _argType = argType;
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => new[] { _argType };
        public Type GetReturnType(string invocationId) => typeof(object);
        public Type GetStreamItemType(string streamId) => typeof(object);
    }

    [Fact]
    public void HubProtocol_RoundTrips_DocumentDownloadedEvent_WithCompanionCaseFiles()
    {
        var protocol = BuildProtocol();
        var evt = new DocumentDownloadedEvent
        {
            FileId = Guid.NewGuid(),
            FileName = "expediente.pdf",
            Source = "SIARA",
            FileSizeBytes = 1234,
            Format = FileFormat.Pdf,
            CorrelationId = Guid.NewGuid(),
            CaseFiles = new List<CaseFileReference>
            {
                new() { RelativePath = "2026/06/24/case/expediente.pdf", Format = FileFormat.Pdf },
                new() { RelativePath = "2026/06/24/case/expediente.xml", Format = FileFormat.Xml },
                new() { RelativePath = "2026/06/24/case/expediente.docx", Format = FileFormat.Docx },
            },
            IsComplete = true,
        };

        var outgoing = new InvocationMessage("ReceiveMessage", new object?[] { evt });

        var writer = new ArrayBufferWriter<byte>();
        protocol.WriteMessage(outgoing, writer);

        // Surface the wire JSON in the failure message so the exact serialized shape is visible.
        var wireJson = System.Text.Encoding.UTF8.GetString(writer.WrittenSpan);

        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        var binder = new SingleArgBinder(typeof(DocumentDownloadedEvent));

        var parsed = protocol.TryParseMessage(ref sequence, binder, out var message);

        parsed.ShouldBeTrue($"the hub protocol must parse the ReceiveMessage envelope. Wire JSON: {wireJson}");
        var invocation = message.ShouldBeOfType<InvocationMessage>();
        var arg = invocation.Arguments.ShouldHaveSingleItem();
        var received = arg.ShouldBeOfType<DocumentDownloadedEvent>();
        received.CaseFiles.Count.ShouldBe(3, $"companion case files must survive the hub protocol. Wire JSON: {wireJson}");
        received.CaseFiles.Select(f => f.Format).ShouldBe(new[] { FileFormat.Pdf, FileFormat.Xml, FileFormat.Docx });
    }
}
