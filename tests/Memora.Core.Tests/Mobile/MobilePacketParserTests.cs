using Memora.Core.Mobile;

namespace Memora.Core.Tests.Mobile;

public sealed class MobilePacketParserTests
{
    // Raw string literals capture whatever line endings the source file uses on disk.
    // Normalize to LF so tests behave the same on Windows checkouts (CRLF) and Linux (LF).
    private static readonly string ValidQuestionPacket = NormalizeLineEndings("""
        ---
        packet_version: 1
        packet_id: 5f0e1a3a-9c1c-4f6d-8b8e-cb6f6d1d51a1
        created_at: 2026-05-12T18:41:00Z
        source: mobile
        intent: question
        lifecycle_target: planning_input
        canonical: false
        title: Cache eviction policy
        tags:
          - context-cache
          - retrieval
        ---

        ## Question

        How should cache entries be evicted?
        """);

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void Parse_ValidQuestionPacket_ReturnsSuccess()
    {
        var result = MobilePacketParser.Parse(ValidQuestionPacket);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Packet);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1, result.Packet!.PacketVersion);
        Assert.Equal("5f0e1a3a-9c1c-4f6d-8b8e-cb6f6d1d51a1", result.Packet.PacketId);
        Assert.Equal(MobilePacketIntent.Question, result.Packet.Intent);
        Assert.Equal(MobilePacketLifecycleTarget.PlanningInput, result.Packet.LifecycleTarget);
        Assert.Equal("Cache eviction policy", result.Packet.Title);
        Assert.Equal(new[] { "context-cache", "retrieval" }, result.Packet.Tags);
        Assert.Contains("How should cache entries be evicted?", result.Packet.Body);
        Assert.False(result.Packet.Canonical);
        Assert.Equal("mobile", result.Packet.Source);
    }

    [Fact]
    public void Parse_MissingFrontmatter_ReturnsFrontmatterMissingDiagnostic()
    {
        var result = MobilePacketParser.Parse("## Question\n\nbody only");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Packet);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.frontmatter.missing");
    }

    [Fact]
    public void Parse_MissingClosingDelimiter_ReturnsMissingEndDiagnostic()
    {
        var markdown = """
            ---
            packet_version: 1
            packet_id: abc
            """;

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.frontmatter.missing_end");
    }

    [Fact]
    public void Parse_CanonicalTrue_IsRejected()
    {
        var markdown = ValidQuestionPacket.Replace("canonical: false", "canonical: true", StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.envelope.canonical_must_be_false");
    }

    [Fact]
    public void Parse_SourceNotMobile_IsRejected()
    {
        var markdown = ValidQuestionPacket.Replace("source: mobile", "source: desktop", StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.envelope.source_must_be_mobile");
    }

    [Fact]
    public void Parse_ReservedField_IsRejected()
    {
        var markdown = ValidQuestionPacket.Replace(
            "canonical: false\n",
            "canonical: false\nstatus: approved\n",
            StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == "mobile_packet.envelope.reserved_field" && d.Path == "frontmatter.status");
    }

    [Fact]
    public void Parse_UnknownIntent_IsRejected()
    {
        var markdown = ValidQuestionPacket.Replace("intent: question", "intent: rant", StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == "mobile_packet.envelope.invalid_value" && d.Path == "frontmatter.intent");
    }

    [Fact]
    public void Parse_IntentLifecycleMismatch_IsRejected()
    {
        var markdown = ValidQuestionPacket.Replace(
            "lifecycle_target: planning_input",
            "lifecycle_target: proposal_draft",
            StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.envelope.intent_lifecycle_mismatch");
    }

    [Fact]
    public void Parse_EmptyBody_IsRejected()
    {
        var markdown = """
            ---
            packet_version: 1
            packet_id: abc
            created_at: 2026-05-12T18:41:00Z
            source: mobile
            intent: planning_note
            lifecycle_target: planning_input
            canonical: false
            ---

            """;

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.body.empty");
    }

    [Fact]
    public void Parse_UnsupportedVersion_IsRejected()
    {
        var markdown = ValidQuestionPacket.Replace("packet_version: 1", "packet_version: 99", StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.unsupported_version");
    }

    [Fact]
    public void Parse_MissingRequiredField_ReportsMissingDiagnostic()
    {
        var markdown = ValidQuestionPacket.Replace("packet_id: 5f0e1a3a-9c1c-4f6d-8b8e-cb6f6d1d51a1\n", string.Empty, StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == "mobile_packet.envelope.missing_required" && d.Path == "frontmatter.packet_id");
    }

    [Fact]
    public void Parse_ProposedArtifactTypeWithIncompatibleIntent_IsRejected()
    {
        var markdown = ValidQuestionPacket.Replace(
            "canonical: false\n",
            "canonical: false\nproposed_artifact_type: decision\n",
            StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.envelope.proposed_type_not_allowed");
    }

    [Fact]
    public void Parse_DecisionDraftWithProposedType_IsAccepted()
    {
        var markdown = """
            ---
            packet_version: 1
            packet_id: abc-123
            created_at: 2026-05-12T18:41:00Z
            source: mobile
            intent: decision_draft
            lifecycle_target: proposal_draft
            canonical: false
            proposed_artifact_type: decision
            ---

            ## Context

            We need to decide on caching.
            """;

        var result = MobilePacketParser.Parse(markdown);

        Assert.True(result.IsSuccess);
        Assert.Equal("decision", result.Packet!.ProposedArtifactType);
        Assert.Equal(MobilePacketIntent.DecisionDraft, result.Packet.Intent);
    }

    [Fact]
    public void Parse_CrlfLineEndings_AreNormalized()
    {
        var markdown = ValidQuestionPacket.Replace("\n", "\r\n", StringComparison.Ordinal);

        var result = MobilePacketParser.Parse(markdown);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parse_QuotedScalarValues_AreUnquoted()
    {
        var markdown = """
            ---
            packet_version: 1
            packet_id: "abc-with-hyphens"
            created_at: "2026-05-12T18:41:00Z"
            source: mobile
            intent: planning_note
            lifecycle_target: planning_input
            canonical: false
            title: "A title: with colon"
            ---

            ## Note

            body
            """;

        var result = MobilePacketParser.Parse(markdown);

        Assert.True(result.IsSuccess);
        Assert.Equal("abc-with-hyphens", result.Packet!.PacketId);
        Assert.Equal("A title: with colon", result.Packet.Title);
    }
}
