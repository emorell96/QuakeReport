using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using QuakeReport.Web.Components.Shared;

namespace QuakeReport.Tests;

[TestClass]
public sealed class HomeSectionComponentTests
{
    [TestMethod]
    public void SectionTitle_RendersTitleAndOptionalActions()
    {
        using var context = CreateContext();

        var withoutActions = context.RenderComponent<SectionTitle>(parameters =>
            parameters.Add(component => component.Title, "Refugios"));
        Assert.IsTrue(withoutActions.Markup.Contains("Refugios", StringComparison.Ordinal));

        RenderFragment actions = builder => builder.AddContent(0, "Ver todos");
        var withActions = context.RenderComponent<SectionTitle>(parameters =>
            parameters
                .Add(component => component.Title, "Refugios")
                .Add(component => component.Actions, actions));
        Assert.IsTrue(withActions.Markup.Contains("Ver todos", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TemplateGrid_RendersItemsAndFooterAction()
    {
        using var context = CreateContext();

        RenderFragment footer = builder => builder.AddContent(0, "Ver todos");
        var cut = context.RenderComponent<TemplateGrid<string>>(parameters => parameters
            .Add(component => component.Items, new[] { "Uno", "Dos" })
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, $"Item:{item}"))
            .Add(component => component.ActionTemplate, footer)
            .Add(component => component.EmptyText, "Sin elementos"));

        var markup = cut.Markup;
        Assert.IsTrue(markup.Contains("Item:Uno", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Item:Dos", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Ver todos", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TemplateGrid_LoadingSuppressesItemsAndEmptyState()
    {
        using var context = CreateContext();

        var loading = context.RenderComponent<TemplateGrid<string>>(parameters => parameters
            .Add(component => component.Items, Array.Empty<string>())
            .Add(component => component.Loading, true)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item))
            .Add(component => component.EmptyText, "Sin elementos"));

        Assert.IsFalse(loading.Markup.Contains("Sin elementos", StringComparison.Ordinal));

        var empty = context.RenderComponent<TemplateGrid<string>>(parameters => parameters
            .Add(component => component.Items, Array.Empty<string>())
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item))
            .Add(component => component.EmptyText, "Sin elementos"));

        Assert.IsTrue(empty.Markup.Contains("Sin elementos", StringComparison.Ordinal));
    }

    [TestMethod]
    public void HomeCardSection_RendersIndependentHeaderAndFooterFragments()
    {
        using var context = CreateContext();

        RenderFragment header = builder => builder.AddContent(0, "Registrar");
        RenderFragment footer = builder => builder.AddContent(0, "Ver todos");
        var cut = context.RenderComponent<HomeCardSection<string>>(parameters => parameters
            .Add(component => component.Title, "Centros")
            .Add(component => component.Items, new[] { "Centro" })
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item))
            .Add(component => component.HeaderActions, header)
            .Add(component => component.FooterActions, footer));

        var markup = cut.Markup;
        Assert.IsTrue(markup.Contains("Centros", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Registrar", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Centro", StringComparison.Ordinal));
        Assert.IsTrue(markup.Contains("Ver todos", StringComparison.Ordinal));
    }

    private static Bunit.TestContext CreateContext()
    {
        var context = new Bunit.TestContext();
        context.Services.AddMudServices();
        return context;
    }
}
