
export interface Env {
  AI: any;
  API_KEY: string;
}

// Standard LlmRequest format matching OpenAiCompatibleLLMProvider.cs
interface LlmRequestMessage {
  Role: string;
  Content: string;
}

interface LlmRequest {
  Messages: LlmRequestMessage[];
  ModelId?: string;
  Temperature?: number;
  MaxTokens?: number;
  Stream?: boolean;
  ResponseFormatJson?: boolean;
  ProviderName?: string;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    // Security check
    const authHeader = request.headers.get("Authorization");
    if (authHeader !== `Bearer ${env.API_KEY}`) {
      console.warn(`Unauthorized access attempt from ${request.headers.get("cf-connecting-ip")}`);
      return new Response(JSON.stringify({ error: "Unauthorized" }), { status: 401, headers: { "Content-Type": "application/json" } });
    }

    if (request.method !== "POST" || !url.pathname.endsWith("/chat/completions")) {
      return new Response(JSON.stringify({ error: "Not Found" }), { status: 404, headers: { "Content-Type": "application/json" } });
    }

    try {
      const body = await request.json() as any;
      
      // Normalize input: support both PascalCase (LlmRequest) and OpenAI format (snake_case/camelCase)
      const requestData: LlmRequest = {
        Messages: body.Messages || body.messages || [],
        ModelId: body.ModelId || body.model || "@cf/meta/llama-3.1-70b-instruct",
        Temperature: body.Temperature ?? body.temperature,
        MaxTokens: body.MaxTokens ?? body.max_tokens,
        Stream: body.Stream ?? body.stream ?? false,
        ResponseFormatJson: body.ResponseFormatJson ?? (body.response_format?.type === 'json_object'),
        ProviderName: body.ProviderName
      };
      
      // Map to Cloudflare AI format
      const model = requestData.ModelId!;
      
      const payload: any = {
        messages: requestData.Messages.map(m => ({ role: m.Role, content: m.Content })),
        stream: requestData.Stream,
      };

      if (requestData.MaxTokens) payload.max_tokens = requestData.MaxTokens;
      if (requestData.Temperature !== undefined) payload.temperature = requestData.Temperature;
      if (requestData.ResponseFormatJson) payload.response_format = { type: 'json_object' };

      let response;
      try {
        response = await env.AI.run(model, payload);
      } catch (err: any) {
    
        if (model.includes("70b")) {
          console.warn(`Model ${model} failed, falling back to 8b. Error: ${err.message}`);
          response = await env.AI.run("@cf/meta/llama-3-8b-instruct", payload);
        } else {
          throw err;
        }
      }

      if (payload.stream) {
        // Transform Cloudflare AI stream to OpenAI stream format
        const { readable, writable } = new TransformStream();
        const writer = writable.getWriter();
        const reader = response.getReader();
        const encoder = new TextEncoder();
        const decoder = new TextDecoder();

        (async () => {
          let buffer = "";
          try {
            while (true) {
              const { done, value } = await reader.read();
              if (done) {
                await writer.write(encoder.encode('data: [DONE]\n\n'));
                await writer.close();
                break;
              }
              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split('\n');
              buffer = lines.pop() || '';
              
              for (const line of lines) {
                if (line.startsWith('data: ')) {
                  const dataStr = line.slice(6).trim();
                  if (dataStr === '[DONE]') {
                    continue;
                  }
                  try {
                    const data = JSON.parse(dataStr);
                    if (data.response) {
                      const openAiChunk = {
                        id: "chatcmpl-cf",
                        object: "chat.completion.chunk",
                        created: Math.floor(Date.now() / 1000),
                        model: model,
                        choices: [{ delta: { content: data.response }, index: 0, finish_reason: null }]
                      };
                      await writer.write(encoder.encode(`data: ${JSON.stringify(openAiChunk)}\n\n`));
                    }
                  } catch (e) { }
                }
              }
            }
          } catch (err) {
            await writer.abort(err);
          }
        })();

        return new Response(readable, {
          headers: {
            "Content-Type": "text/event-stream",
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "Access-Control-Allow-Origin": "*"
          }
        });
      } else {
        // Non-streaming response transformation
        let content = response.response;
        
        // If ResponseFormatJson is true, try to clean the JSON response
        if (requestData.ResponseFormatJson) {
          const start = content.indexOf('{');
          const end = content.lastIndexOf('}');
          if (start !== -1 && end !== -1 && end > start) {
            content = content.substring(start, end + 1);
          }
        }

        const openAiResponse = {
          id: "chatcmpl-cf",
          object: "chat.completion",
          created: Math.floor(Date.now() / 1000),
          model: model,
          choices: [{
            index: 0,
            message: { role: "assistant", content: content },
            finish_reason: "stop"
          }]
        };
        return new Response(JSON.stringify(openAiResponse), {
          headers: { 
            "Content-Type": "application/json",
            "Access-Control-Allow-Origin": "*" 
          }
        });
      }
    } catch (e: any) {
      return new Response(JSON.stringify({ error: { message: e.message } }), { 
        status: 500, 
        headers: { "Content-Type": "application/json" } 
      });
    }
  }
}

