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
              content: `You are a professional lexicographer. Provide structured info for the word in JSON.
              FATAL REQUIREMENT: OUTPUT ONLY VALID JSON. NO MARKDOWN (no \`\`\`json). NO CONVERSATIONAL TEXT.
              Required keys: "partOfSpeech", "phoneticUk", "phoneticUs", "definition", "exampleSentence".`
            },
            { role: "user", content: `Enrich: "${word}"` }
          ],
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
              content: `Explain the usage and nuances of the word "${word}"${context ? ` in the context of: "${context}"` : ""}. 
              FATAL REQUIREMENT: OUTPUT ONLY VALID JSON object. NO MARKDOWN. NO CONVERSATIONAL TEXT.
              Return JSON: { "explanation": "detailed string", "nuances": ["string"], "tips": ["string"] }`
            },
            { role: "user", content: `Explain: "${word}"` }
          ],
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
              content: `Provide synonyms, antonyms, and common collocations for "${word}".
              FATAL REQUIREMENT: OUTPUT ONLY VALID JSON object. NO MARKDOWN. NO CONVERSATIONAL TEXT.
              Return JSON: { "synonyms": ["string"], "antonyms": ["string"], "collocations": ["string"] }`
            },
            { role: "user", content: `Related: "${word}"` }
          ],
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
              content: `Generate a multiple-choice quiz question for the word "${word}".
              FATAL REQUIREMENT: OUTPUT ONLY VALID JSON object. NO MARKDOWN. NO CONVERSATIONAL TEXT.
              Required keys: "question" (string), "options" (array of 4 strings), "correctIndex" (number 0-3), "explanation" (string).`
            },
            { role: "user", content: `Quiz: "${word}"` }
          ],
          response_format: { type: "json_object" }
        });

        return new Response(JSON.stringify(parseAiResponse(response)), {
          headers: { "Content-Type": "application/json" }
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
    console.error("Critical parse error. Raw content:", content);
    return { 
      error: "AI returned invalid JSON", 
      raw: content,
      // Fallback for UI if partial matches are possible
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
