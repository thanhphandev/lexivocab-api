
export interface Env {
  AI: any;
  API_KEY: string;
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
      const model = body.model || "@cf/meta/llama-3.1-70b-instruct";
      
      const payload: any = {
        messages: body.messages,
        stream: body.stream || false,
      };

      if (body.max_tokens) payload.max_tokens = body.max_tokens;
      if (body.temperature) payload.temperature = body.temperature;
      if (body.response_format) payload.response_format = body.response_format;

      let response;
      try {
        response = await env.AI.run(model, payload);
      } catch (err: any) {
        if (err.message && err.message.includes("json_object")) {
          console.warn(`Model ${model} rejected json_object format, retrying without it.`);
          delete payload.response_format;
          try {
            response = await env.AI.run(model, payload);
          } catch(err2: any) {
            if (model.includes("70b")) {
              console.warn(`Model ${model} failed again, falling back to 8b. Error: ${err2.message}`);
              response = await env.AI.run("@cf/meta/llama-3-8b-instruct", payload);
            } else {
              throw err2;
            }
          }
        } 
        // Fallback to 8b if 70b fails
        else if (model.includes("70b")) {
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
        
        // If response_format is json_object, try to clean it
        if (payload.response_format?.type === 'json_object') {
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

