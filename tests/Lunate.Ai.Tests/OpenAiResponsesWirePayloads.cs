namespace Lunate.Ai.Tests;

/// <summary>
/// Canned Responses wire payloads (the OpenAI Responses API's SSE event sequence), served
/// verbatim by <see cref="ScriptedResponsesHandler"/> so the assertions see the same bytes a
/// provider sends.
/// </summary>
internal static class OpenAiResponsesWirePayloads
{
    /// <summary>A streamed assistant text turn ("pong").</summary>
    public const string TextTurn = """
        event: response.created
        data: {"type":"response.created","response":{"id":"resp_text","object":"response","created_at":0,"status":"in_progress","model":"gpt-wire","output":[]}}

        event: response.output_item.added
        data: {"type":"response.output_item.added","output_index":0,"item":{"id":"msg_text","type":"message","role":"assistant","status":"in_progress","content":[]}}

        event: response.output_text.delta
        data: {"type":"response.output_text.delta","item_id":"msg_text","output_index":0,"content_index":0,"delta":"pong"}

        event: response.output_text.done
        data: {"type":"response.output_text.done","item_id":"msg_text","output_index":0,"content_index":0,"text":"pong"}

        event: response.output_item.done
        data: {"type":"response.output_item.done","output_index":0,"item":{"id":"msg_text","type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"pong","annotations":[]}]}}

        event: response.completed
        data: {"type":"response.completed","response":{"id":"resp_text","object":"response","created_at":0,"status":"completed","model":"gpt-wire","output":[{"id":"msg_text","type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"pong","annotations":[]}]}],"usage":{"input_tokens":1,"output_tokens":1,"total_tokens":2}}}
        """;

    /// <summary>A streamed tool call turn: one function_call item announcing <c>echo</c>.</summary>
    public const string ToolCallTurn = """
        event: response.created
        data: {"type":"response.created","response":{"id":"resp_call","object":"response","created_at":0,"status":"in_progress","model":"gpt-wire","output":[]}}

        event: response.output_item.added
        data: {"type":"response.output_item.added","output_index":0,"item":{"id":"fc_1","call_id":"call_1","type":"function_call","name":"echo","arguments":"","status":"in_progress"}}

        event: response.function_call_arguments.delta
        data: {"type":"response.function_call_arguments.delta","item_id":"fc_1","output_index":0,"delta":"{\"text\":\"ping\"}"}

        event: response.function_call_arguments.done
        data: {"type":"response.function_call_arguments.done","item_id":"fc_1","output_index":0,"arguments":"{\"text\":\"ping\"}"}

        event: response.output_item.done
        data: {"type":"response.output_item.done","output_index":0,"item":{"id":"fc_1","call_id":"call_1","type":"function_call","name":"echo","arguments":"{\"text\":\"ping\"}","status":"completed"}}

        event: response.completed
        data: {"type":"response.completed","response":{"id":"resp_call","object":"response","created_at":0,"status":"completed","model":"gpt-wire","output":[{"id":"fc_1","call_id":"call_1","type":"function_call","name":"echo","arguments":"{\"text\":\"ping\"}"}],"usage":{"input_tokens":1,"output_tokens":1,"total_tokens":2}}}
        """;
}
