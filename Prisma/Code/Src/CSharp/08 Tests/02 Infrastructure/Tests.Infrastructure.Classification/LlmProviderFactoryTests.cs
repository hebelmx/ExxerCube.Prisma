using ExxerCube.Prisma.Domain.Llm;
using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="LlmProviderFactory"/>.
/// </summary>
public sealed class LlmProviderFactoryTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ILlmProvider MakeProvider(string name, LlmCapabilities caps = LlmCapabilities.TextGenerate)
    {
        var p = Substitute.For<ILlmProvider>();
        p.Name.Returns(name);
        p.Capabilities.Returns(caps);
        return p;
    }

    private static IOptionsMonitor<LlmProvidersOptions> MakeOptions(string active = "Ollama")
    {
        var opts = new LlmProvidersOptions { Active = active };
        var monitor = Substitute.For<IOptionsMonitor<LlmProvidersOptions>>();
        monitor.CurrentValue.Returns(opts);
        return monitor;
    }

    // -----------------------------------------------------------------------
    // GetActive returns configured provider
    // -----------------------------------------------------------------------

    [Fact]
    public void GetActive_ReturnsConfiguredProvider()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions("Ollama"));

        var active = factory.GetActive();

        active.Name.ShouldBe("Ollama");
    }

    [Fact]
    public void GetActive_NameLookupIsCaseInsensitive()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions("ollama"));

        var active = factory.GetActive();

        active.Name.ShouldBe("Ollama");
    }

    // -----------------------------------------------------------------------
    // SetActive switches the provider
    // -----------------------------------------------------------------------

    [Fact]
    public void SetActive_SwitchesToNamedProvider()
    {
        var ollama = MakeProvider("Ollama");
        var gemini = MakeProvider("Gemini");
        var factory = new LlmProviderFactory([ollama, gemini], MakeOptions("Ollama"));

        var setResult = factory.SetActive("Gemini");

        setResult.IsSuccess.ShouldBeTrue();
        factory.GetActive().Name.ShouldBe("Gemini");
    }

    [Fact]
    public void SetActive_SwitchIsCaseInsensitive()
    {
        var ollama = MakeProvider("Ollama");
        var gemini = MakeProvider("Gemini");
        var factory = new LlmProviderFactory([ollama, gemini], MakeOptions("Ollama"));

        factory.SetActive("gemini");

        factory.GetActive().Name.ShouldBe("Gemini");
    }

    // -----------------------------------------------------------------------
    // SetActive unknown name → failure
    // -----------------------------------------------------------------------

    [Fact]
    public void SetActive_UnknownName_ReturnsFailure()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions("Ollama"));

        var result = factory.SetActive("NonExistent");

        result.IsSuccess.ShouldBeFalse();
        result.Errors?.FirstOrDefault().ShouldContain("NonExistent");
    }

    [Fact]
    public void SetActive_NullName_ReturnsFailure()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions("Ollama"));

        var result = factory.SetActive(null!);

        result.IsSuccess.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // RegisteredNames
    // -----------------------------------------------------------------------

    [Fact]
    public void RegisteredNames_ReturnsAllProviders()
    {
        var ollama = MakeProvider("Ollama");
        var gemini = MakeProvider("Gemini");
        var factory = new LlmProviderFactory([ollama, gemini], MakeOptions());

        var names = factory.RegisteredNames;

        names.Count.ShouldBe(2);
        names.ShouldContain("Ollama");
        names.ShouldContain("Gemini");
    }

    [Fact]
    public void RegisteredNames_SingleProvider_ReturnsSingleEntry()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions());

        factory.RegisteredNames.Count.ShouldBe(1);
        factory.RegisteredNames.ShouldContain("Ollama");
    }

    // -----------------------------------------------------------------------
    // Get by name
    // -----------------------------------------------------------------------

    [Fact]
    public void Get_KnownName_ReturnsSuccess()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions());

        var result = factory.Get("Ollama");

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("Ollama");
    }

    [Fact]
    public void Get_UnknownName_ReturnsFailure()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions());

        var result = factory.Get("Gemini");

        result.IsSuccess.ShouldBeFalse();
        result.Errors?.FirstOrDefault().ShouldContain("Gemini");
    }

    [Fact]
    public void Get_EmptyName_ReturnsFailure()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions());

        var result = factory.Get(string.Empty);

        result.IsSuccess.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // GetActive with unknown config → InvalidOperationException
    // -----------------------------------------------------------------------

    [Fact]
    public void GetActive_ConfiguredNameNotRegistered_ThrowsInvalidOperationException()
    {
        var ollama = MakeProvider("Ollama");
        var factory = new LlmProviderFactory([ollama], MakeOptions("NonExistent"));

        Should.Throw<InvalidOperationException>(() => factory.GetActive());
    }
}
