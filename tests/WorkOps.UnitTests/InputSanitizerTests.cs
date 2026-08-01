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

    [TestMethod]
    public void Sensitive_no_mutation_preserves_the_exact_value()
    {
        const string submitted = " leading-and-trailing ";

        var result = _sanitizer.Apply(
            submitted,
            InputProfile.SensitiveNoMutation,
            "body.subject");

        Assert.AreEqual(submitted, result);
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

    [DataRow("../report.pdf")]
    [DataRow(".hidden.txt")]
    [DataRow("folder/report.pdf")]
    [DataRow("report<script>.pdf")]
    [TestMethod]
    public void File_name_rejects_paths_and_unsafe_characters(string submitted)
    {
        Assert.ThrowsExactly<InputRejectedException>(
            () => _sanitizer.Apply(submitted, InputProfile.FileName, "form.file.fileName"));
    }

    [DataRow("text/plain; charset=utf-8")]
    [DataRow("text\\plain")]
    [DataRow("text/plain\r\nInjected: true")]
    [TestMethod]
    public void Mime_type_rejects_parameters_and_header_injection(string submitted)
    {
        Assert.ThrowsExactly<InputRejectedException>(
            () => _sanitizer.Apply(submitted, InputProfile.MimeType, "form.file.contentType"));
    }
}
