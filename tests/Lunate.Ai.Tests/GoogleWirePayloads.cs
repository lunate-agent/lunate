namespace Lunate.Ai.Tests;

/// <summary>
/// The Gemini API's streaming wire payloads: each chunk the SDK parses is one <c>data:</c> line
/// carrying a JSON object, closed by a blank line — the shapes Google's <c>streamGenerateContent</c>
/// serves (SSE mode, which the SDK requests with <c>?alt=sse</c>).
/// </summary>
public static class GoogleWirePayloads
{
    /// <summary>A plain streamed text turn: one text part and use metadata.</summary>
    public const string TextTurn =
        "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"pong\"}],\"role\":\"model\"}}]"
        + ",\"usageMetadata\":{\"promptTokenCount\":5,\"candidatesTokenCount\":1,\"totalTokenCount\":6}"
        + ",\"modelVersion\":\"gemini-2.5-flash\"}\n\n";

    /// <summary>A tool-call turn: a thinking part and a single <c>echo</c> call.</summary>
    public const string ToolCallTurn =
        "data: {\"candidates\":[{\"content\":{\"parts\":"
        + "[{\"text\":\"weighing it\",\"thought\":true},"
        + "{\"functionCall\":{\"name\":\"echo\",\"args\":{\"text\":\"ping\"}}}]}}]}\n\n";

    /// <summary>
    /// Two parallel <c>echo</c> calls in one parts list, the first carrying a thought signature
    /// (the shape Gemini 3 streams around tool calls).
    /// </summary>
    public const string ParallelToolCallTurn =
        "data: {\"candidates\":[{\"content\":{\"parts\":"
        + "[{\"text\":\"thinking now\",\"thought\":true},"
        + "{\"functionCall\":{\"name\":\"echo\",\"args\":{\"text\":\"first\"}},\"thoughtSignature\":\"c2lnX2FfZnVuY3Rpb24=\"},"
        + "{\"functionCall\":{\"name\":\"echo\",\"args\":{\"text\":\"second\"}}}]}}]"
        + ",\"modelVersion\":\"gemini-2.5-flash\"}\n\n";

    /// <summary>The turn after tool results: text and the finish marker together.</summary>
    public const string FinalTurn =
        "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"final answer\"}],\"role\":\"model\"}"
        + ",\"finishReason\":\"STOP\"}]"
        + ",\"usageMetadata\":{\"promptTokenCount\":7,\"candidatesTokenCount\":3,\"totalTokenCount\":10}}\n\n";

    /// <summary>
    /// A turn whose use metadata names a prompt-cache read, the size a cached system prefix
    /// reports on the native API.
    /// </summary>
    public const string CachedTurn =
        "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"cached answer\"}]}}]"
        + ",\"usageMetadata\":{\"promptTokenCount\":1024,\"candidatesTokenCount\":2,\"totalTokenCount\":1026"
        + ",\"cachedContentTokenCount\":768}}\n\n";

    /// <summary>
    /// A turn streamed as two chunks: the second one carries the finish marker, so a multi-chunk
    /// stream surfaces through the factory's own streaming layers.
    /// </summary>
    public const string SplitTextTurn =
        "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"pon\"}],\"role\":\"model\"}}]}\n\n"
        + "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"g\"}],\"role\":\"model\"},\"finishReason\":\"STOP\"}]}\n\n";
}
