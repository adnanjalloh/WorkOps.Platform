using WorkOps.Application.Common.Sanitization;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class InputSanitizerTests
{
    private readonly InputSanitizer _sanitizer = new();

    [DataRow("<script>alert(1)</script>")]
    [DataRow("line\r\nInjected: true")]
    [DataRow("unsafe\u0000value")]
    [TestMethod]
    public void Plain_text_rejects_active_content_and_control_characters(string submitted)
    {
        Assert.ThrowsExactly<InputRejectedException>(
            () => _sanitizer.Apply(submitted, InputProfile.PlainText, "body.name"));
    }

    [DataRow("-leading")]
    [DataRow("trailing-")]
    [DataRow("two--separators")]
    [DataRow("path/escape")]
    [DataRow("ab")]
    [TestMethod]
    public void Key_path_rejects_invalid_slugs(string submitted)
    {
        Assert.ThrowsExactly<InputRejectedException>(
            () => _sanitizer.Apply(submitted, InputProfile.KeyPath, "body.slug"));
    }

    [TestMethod]
    public void Key_path_normalizes_valid_slug()
    {
        var result = _sanitizer.Apply("  Team-Operations  ", InputProfile.KeyPath, "body.slug");

        Assert.AreEqual("team-operations", result);
    }

    [TestMethod]
    public void Identifier_accepts_external_subject_format()
    {
        var result = _sanitizer.Apply("oidc|user@example.org", InputProfile.Identifier, "identity.subject");

        Assert.AreEqual("oidc|user@example.org", result);
    }

    [DataRow("%wildcard")]
    [DataRow("_wildcard")]
    [DataRow("<script>")]
    [TestMethod]
    public void Search_text_rejects_wildcards_and_active_content(string submitted)
    {
        Assert.ThrowsExactly<InputRejectedException>(
            () => _sanitizer.Apply(submitted, InputProfile.SearchText, "query.search"));
    }
}
