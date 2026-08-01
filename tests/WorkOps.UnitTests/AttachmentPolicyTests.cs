using System.Text;
using WorkOps.Application.Files;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class AttachmentPolicyTests
{
    [TestMethod]
    public void Accepted_types_require_matching_extension_mime_and_signature()
    {
        AttachmentPolicy.Validate("proof.pdf", "application/pdf", "%PDF-1.7"u8);
        AttachmentPolicy.Validate(
            "image.png",
            "image/png",
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 });
        AttachmentPolicy.Validate("notes.txt", "text/plain", "safe text\n"u8);
    }

    [DataRow("proof.pdf", "image/png")]
    [DataRow("proof.exe", "application/pdf")]
    [TestMethod]
    public void Mime_or_extension_mismatch_is_rejected(string fileName, string contentType)
    {
        var exception = Assert.ThrowsExactly<AttachmentRejectedException>(
            () => AttachmentPolicy.Validate(fileName, contentType, "%PDF-1.7"u8));

        Assert.AreEqual("invalid_attachment_type", exception.Code);
    }

    [TestMethod]
    public void Signature_mismatch_is_rejected()
    {
        var exception = Assert.ThrowsExactly<AttachmentRejectedException>(
            () => AttachmentPolicy.Validate("image.png", "image/png", "not a png"u8));

        Assert.AreEqual("invalid_attachment_type", exception.Code);
    }

    [TestMethod]
    public void Invalid_utf8_text_is_rejected()
    {
        var exception = Assert.ThrowsExactly<AttachmentRejectedException>(
            () => AttachmentPolicy.Validate("notes.txt", "text/plain", new byte[] { 0xC3, 0x28 }));

        Assert.AreEqual("invalid_attachment_type", exception.Code);
    }

    [TestMethod]
    public void Empty_and_oversized_files_are_rejected()
    {
        var empty = Assert.ThrowsExactly<AttachmentRejectedException>(
            () => AttachmentPolicy.Validate("notes.txt", "text/plain", ReadOnlySpan<byte>.Empty));
        var oversized = Assert.ThrowsExactly<AttachmentRejectedException>(
            () => AttachmentPolicy.Validate(
                "notes.txt",
                "text/plain",
                Encoding.UTF8.GetBytes(new string('a', AttachmentPolicy.MaximumBytes + 1))));

        Assert.AreEqual("invalid_attachment_size", empty.Code);
        Assert.AreEqual("invalid_attachment_size", oversized.Code);
    }
}
