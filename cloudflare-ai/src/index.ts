export interface Env {
  AI: any;
  API_KEY: string;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    // Security Check: API Key
    const authHeader = request.headers.get("Authorization");
    if (authHeader !== `Bearer ${env.API_KEY}`) {
      console.warn(`Unauthorized access attempt from ${request.headers.get("cf-connecting-ip")}`);
      return new Response("Unauthorized", { status: 401 });
    }

    if (url.pathname === "/enrich-word") {
      try {
        const { word } = await request.json() as { word: string };
        if (!word) return new Response("Missing word", { status: 400 });

        const response = await runWithFallback(env, {
          messages: [
            {
              role: "system",
              content: `Enrich "${word}". Strictly JSON: { "partOfSpeech": "pos", "phoneticUk": "/.../", "phoneticUs": "/.../", "definition": "concise", "exampleSentence": "..." }`
            }
          ],
          max_tokens: 300,
          response_format: { type: "json_object" }
        });

        return new Response(JSON.stringify(parseAiResponse(response)), {
          headers: { "Content-Type": "application/json" }
        });
      } catch (error: any) {
        return errorResponse(error);
      }
    }

    if (url.pathname === "/explain-usage") {
      try {
        const { word, context } = await request.json() as { word: string, context?: string };
        if (!word) return new Response("Missing word", { status: 400 });

        const response = await runWithFallback(env, {
          messages: [
            {
              role: "system",
              content: `Explain "${word}"${context ? ` in: "${context}"` : ""}. Strictly JSON: { "explanation": "brief usage", "nuances": ["diff 1"], "examples": ["ex 1"], "tip": "key takeaway" }`
            }
          ],
          max_tokens: 350,
          response_format: { type: "json_object" }
        });

        return new Response(JSON.stringify(parseAiResponse(response)), {
          headers: { "Content-Type": "application/json" }
        });
      } catch (error: any) {
        return errorResponse(error);
      }
    }

    if (url.pathname === "/suggest-related") {
      try {
        const { word } = await request.json() as { word: string };
        const response = await runWithFallback(env, {
          messages: [
            {
              role: "system",
              content: `Return synonyms, antonyms, collocations for "${word}". Strictly JSON: { "synonyms": [], "antonyms": [], "collocations": [] }`
            }
          ],
          max_tokens: 300,
          response_format: { type: "json_object" }
        });

        return new Response(JSON.stringify(parseAiResponse(response)), {
          headers: { "Content-Type": "application/json" }
        });
      } catch (error: any) {
        return errorResponse(error);
      }
    }

    if (url.pathname === "/generate-quiz") {
      try {
        const { word } = await request.json() as { word: string };
        const response = await runWithFallback(env, {
          messages: [
            {
              role: "system",
              content: `Generate MC-quiz for "${word}". Strictly JSON: { "question": "...", "options": ["A", "B", "C", "D"], "correctIndex": 0, "explanation": "..." }`
            }
          ],
          max_tokens: 300,
          response_format: { type: "json_object" }
        });

        return new Response(JSON.stringify(parseAiResponse(response)), {
          headers: { "Content-Type": "application/json" }
        });
      } catch (error: any) {
        return errorResponse(error);
      }
    }

    if (url.pathname === "/explain-usage-stream") {
      try {
        const { word, context, format } = await request.json() as { word: string, context?: string, format?: string };
        if (!word) return new Response("Missing word", { status: 400 });

        const isJson = format === "json";
        const systemPrompt = isJson 
          ? `Explain "${word}"${context ? ` in: "${context}"` : ""}. Strictly JSON: { "explanation": "brief usage", "nuances": ["diff"], "examples": ["ex"], "tip": "key takeaway" }`
          : `Explain "${word}" in 1 sentence, then provide 2 nuances, 2 examples, and a Pro Tip. Use perfect markdown.`;

        const stream = await env.AI.run("@cf/meta/llama-3.1-70b-instruct", {
          messages: [
            { role: "system", content: systemPrompt }
          ],
          max_tokens: 500,
          stream: true,
          response_format: isJson ? { type: "json_object" } : undefined
        });

        return new Response(stream, {
          headers: { 
            "Content-Type": "text/event-stream",
            "Cache-Control": "no-cache",
            "Connection": "keep-alive"
          }
        });
      } catch (error: any) {
        return errorResponse(error);
      }
    }

    return new Response(JSON.stringify({ error: "Endpoint not found" }), { 
      status: 404,
      headers: { "Content-Type": "application/json" }
    });
  }
};

function parseAiResponse(response: any) {
  let content = response.response;
  
  if (!content) return response;
  if (typeof content !== 'string') return content;

  // Cleanup: AI might still wrap in markdown or add conversational intro
  // Find the first '{' and last '}'
  const start = content.indexOf('{');
  const end = content.lastIndexOf('}');
  
  if (start !== -1 && end !== -1 && end > start) {
    content = content.substring(start, end + 1);
  }

  try {
    return JSON.parse(content);
  } catch (e) {
    // Attempt rescue: check if it's just missing a closing brace
    try {
      if (content.trim().endsWith('"]')) return JSON.parse(content + '}');
      if (content.trim().endsWith('"}')) return JSON.parse(content); // Should have worked, but who knows
    } catch {}

    console.error("Critical parse error. Raw content:", content);
    return { 
      error: "AI returned invalid JSON", 
      explanation: typeof content === 'string' ? content : "Internal Error"
    };
  }
}

async function runWithFallback(env: Env, options: any) {
  try {
    return await env.AI.run("@cf/meta/llama-3.1-70b-instruct", options);
  } catch (error: any) {
    console.warn("Llama 70B failed, falling back to 8B...", error.message);
    return await env.AI.run("@cf/meta/llama-3-8b-instruct", options);
  }
}

function errorResponse(error: any) {
  return new Response(JSON.stringify({ 
    success: false, 
    error: "AI Service Error",
    details: error.message 
  }), { 
    status: 500,
    headers: { "Content-Type": "application/json" }
  });
}
