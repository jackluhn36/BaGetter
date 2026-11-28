using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Moq;
using Xunit;

namespace BaGest.Web.Tests;

public class NavLinkTagHelperFacts
{
    [Fact]
    public void KeepsExistingClassesAndAddsActiveForCurrentPage()
    {
        var output = Process(currentPage: "/Index", linkPage: "/Index", existingClass: "nav-link");

        Assert.Equal("nav-link active", ClassOf(output));
        Assert.Equal("page", output.Attributes["aria-current"].Value);
    }

    [Fact]
    public void AddsActiveWhenLinkHasNoClass()
    {
        var output = Process(currentPage: "/Index", linkPage: "/Index", existingClass: null);

        Assert.Equal("active", ClassOf(output));
    }

    [Fact]
    public void LeavesClassesUnchangedForOtherPage()
    {
        var output = Process(currentPage: "/Upload", linkPage: "/Index", existingClass: "nav-link");

        Assert.Equal("nav-link", ClassOf(output));
        Assert.False(output.Attributes.ContainsName("aria-current"));
    }

    [Fact]
    public void LeavesClassesUnchangedWhenPageIsInAnArea()
    {
        var output = Process(currentPage: "/Index", linkPage: "/Index", existingClass: "nav-link", area: "Admin");

        Assert.Equal("nav-link", ClassOf(output));
    }

    [Fact]
    public void LeavesClassesUnchangedWithoutEndpoint()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
        var output = CreateOutput("nav-link");

        new NavLinkTagHelper(accessor.Object) { Page = "/Index" }.Process(CreateContext(output), output);

        Assert.Equal("nav-link", ClassOf(output));
    }

    [Theory]
    [InlineData("/Index")]
    [InlineData("/Upload")]
    public void RemovesTheMarkerAttribute(string currentPage)
    {
        var output = Process(currentPage, linkPage: "/Index", existingClass: "nav-link");

        Assert.False(output.Attributes.ContainsName("nav-link"));
    }

    private static TagHelperOutput Process(string currentPage, string linkPage, string existingClass, string area = null)
    {
        var httpContext = new DefaultHttpContext();
        var descriptor = new PageActionDescriptor { ViewEnginePath = currentPage, AreaName = area };
        httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(descriptor), "page"));

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        var output = CreateOutput(existingClass);
        var target = new NavLinkTagHelper(accessor.Object) { Page = linkPage };

        target.Process(CreateContext(output), output);

        return output;
    }

    private static TagHelperOutput CreateOutput(string existingClass)
    {
        var attributes = new TagHelperAttributeList { { "nav-link", null } };
        if (existingClass != null)
        {
            attributes.Add("class", existingClass);
        }

        return new TagHelperOutput(
            "a",
            attributes,
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
    }

    private static TagHelperContext CreateContext(TagHelperOutput output) =>
        new("a", new TagHelperAttributeList(output.Attributes), new Dictionary<object, object>(), "unique-id");

    private static string ClassOf(TagHelperOutput output)
    {
        if (!output.Attributes.TryGetAttribute("class", out var attribute))
        {
            return null;
        }

        if (attribute.Value is IHtmlContent content)
        {
            using var writer = new StringWriter();
            content.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }

        return attribute.Value?.ToString();
    }
}
