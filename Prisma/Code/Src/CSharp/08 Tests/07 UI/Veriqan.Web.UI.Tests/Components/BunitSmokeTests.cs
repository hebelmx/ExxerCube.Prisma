namespace ExxerCube.Prisma.Veriqan.Web.UI.Tests.Components;

/// <summary>
/// SPIKE (VLD-S4b): proves bUnit's <see cref="Bunit.BunitContext"/> renders a trivial component
/// under this repo's xunit.v3.mtp-v2 + Microsoft.Testing.Platform 2.1.0 stack before any real
/// component test is written. If this fails at run time (e.g. MissingMethodException), the
/// fallback per the story is to revert the bunit package add and test a plain view-model class
/// instead. (bUnit 2.x renamed its root fixture <c>TestContext</c> -&gt; <c>BunitContext</c>,
/// which also conveniently avoids colliding with xunit.v3's own <c>Xunit.TestContext</c>.)
/// </summary>
public sealed class BunitSmokeTests
{
    [Fact]
    public void Render_TrivialMarkup_ProducesExpectedHtml()
    {
        using var ctx = new Bunit.BunitContext();

        var cut = ctx.Render(builder =>
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, "hi");
            builder.CloseElement();
        });

        cut.Markup.ShouldBe("<p>hi</p>");
    }
}
