namespace Lunate.Coding.Tests;

/// <summary>
/// The reducer between an HTML response and the text a model reads: the title, the visible prose,
/// block structure, lists and links as markdown. Script and style bodies never become text, and a
/// truncated document degrades to the text read so far instead of throwing.
/// </summary>
public sealed class HtmlToTextTests
{
    [Fact]
    public void The_title_and_visible_text_are_kept()
    {
        HtmlPage page = HtmlToText.Parse(
            WebTestSupport.Fixture("article.html"),
            new Uri("https://example.test/docs/page")
        );

        Assert.Equal("Guide & Reference", page.Title);
        Assert.Contains("Getting started", page.Text, StringComparison.Ordinal);
        Assert.Contains(
            "Read the [guide](https://example.test/guide) and the [API](https://example.test/api).",
            page.Text,
            StringComparison.Ordinal
        );
        Assert.Contains("- One", page.Text, StringComparison.Ordinal);
        Assert.Contains("- Two", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_and_style_bodies_are_not_text()
    {
        HtmlPage page = HtmlToText.Parse(
            WebTestSupport.Fixture("article.html"),
            new Uri("https://example.test/docs/page")
        );

        Assert.DoesNotContain("do not show", page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("color: red", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Relative_links_are_resolved_against_the_page()
    {
        HtmlPage page = HtmlToText.Parse(
            WebTestSupport.Fixture("article.html"),
            new Uri("https://example.test/docs/page")
        );

        Assert.Contains(
            "[next page](https://example.test/docs/next)",
            page.Text,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Entities_are_decoded()
    {
        HtmlPage page = HtmlToText.Parse(
            "<html><body><p>&lt;tag&gt; &amp; &#38; &#x26;</p></body></html>",
            new Uri("https://example.test/")
        );

        Assert.Contains("<tag> & & &", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_markup_is_read_as_far_as_it_goes()
    {
        HtmlPage page = HtmlToText.Parse(
            "<html><title>Broken</title><p>Hello <a href='https://example.test'>link",
            new Uri("https://example.test/")
        );

        Assert.Equal("Broken", page.Title);
        Assert.Contains("Hello", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_document_is_an_empty_page()
    {
        HtmlPage page = HtmlToText.Parse(string.Empty, new Uri("https://example.test/"));

        Assert.Null(page.Title);
        Assert.Equal(string.Empty, page.Text);
    }

    [Fact]
    public void A_tag_inside_an_attribute_does_not_confuse_the_scanner()
    {
        HtmlPage page = HtmlToText.Parse(
            """<html><body><p title="a > b">Before</p><p>After</p></body></html>""",
            new Uri("https://example.test/")
        );

        Assert.Contains("Before", page.Text, StringComparison.Ordinal);
        Assert.Contains("After", page.Text, StringComparison.Ordinal);
    }
}
