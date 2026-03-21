export interface Env {
  AI: any;
  API_KEY: string;
  SILICONFLOW_API_KEY?: string;
  QWEN_API_KEY?: string;
  MINMAX_API_KEY?: string;
}

async function fetchOpenAIStream(apiUrl: string, apiKey: string, model: string, messages: any[], isJson: boolean = false) {
  if (!apiKey || apiKey === "sk-REPLACE_ME" || apiKey === "REPLACE_ME") {
    throw new Error(`API Key for ${apiUrl} is not configured.`);
  }
  const response = await fetch(apiUrl, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${apiKey}`
    },
    body: JSON.stringify({
      model: model,
      messages: messages,
      stream: true,
      max_tokens: 300,
      ...(isJson ? { response_format: { type: "json_object" } } : {})
    })
  });

  if (!response.ok) {
    throw new Error(`API Error: ${response.status} ${await response.text()}`);
  }
  return response.body as ReadableStream;
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

    if (url.pathname === "/suggest-related") {
      try {
        const { word, targetLanguage, userLanguage } = await request.json() as { word: string, targetLanguage?: string, userLanguage?: string };
        const tl = targetLanguage || "English";
        const ul = userLanguage || "Vietnamese";

        const response = await runWithFallback(env, {
          messages: [
            {
              role: "system",
              content: `Return synonyms, antonyms, and collocations for the ${tl} word "${word}". Any brief explanations or notes should be in ${ul}. Strictly JSON: { "synonyms": ["word1", "word2"], "antonyms": ["word1", "word2"], "collocations": ["colloc1", "colloc2"] }`
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
        const { word, targetLanguage, userLanguage } = await request.json() as { word: string, targetLanguage?: string, userLanguage?: string };
        const tl = targetLanguage || "English";
        const ul = userLanguage || "Vietnamese";

        const response = await runWithFallback(env, {
          messages: [
            {
              role: "system",
              content: `Generate a multiple-choice quiz for the ${tl} word "${word}". The question and options must be in ${tl} to test the user's proficiency. The explanation for the correct answer must be in ${ul} so the user understands why. Strictly JSON: { "question": "...", "options": ["A", "B", "C", "D"], "correctIndex": 0, "explanation": "..." }`
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

    if (url.pathname === "/generate-fill-blank") {
      try {
        const { word, targetLanguage, userLanguage } = await request.json() as { word: string, targetLanguage?: string, userLanguage?: string };
        const tl = targetLanguage || "English";
        const ul = userLanguage || "Vietnamese";

        const response = await runWithFallback(env, {
          messages: [
            {
              role: "system",
              content: `Generate a fill-in-the-blank question for the ${tl} word "${word}". Create a natural context sentence in ${tl} with a blank "___" representing the word. Provide 3 plausible but incorrect options in ${tl}. The translation of the sentence must be in ${ul}. Strictly JSON: { "sentence": "I like to ___ coffee.", "translation": "Tôi thích uống cà phê.", "correctWord": "drink", "options": ["eat", "run", "drink", "sleep"], "explanation": "Brief tip in ${ul} why this word fits" }`
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
        const { word, context, format, targetLanguage, userLanguage } = await request.json() as { word: string, context?: string, format?: string, targetLanguage?: string, userLanguage?: string };
        if (!word) return new Response("Missing word", { status: 400 });

        const tl = targetLanguage || "English";
        const ul = userLanguage || "Vietnamese";

        const isJson = format === "json";
        const systemPrompt = isJson
          ? `You are a professional dictionary assistant. The user is learning ${tl} and their native language is ${ul}. Explain "${word}"${context ? ` in the context: "${context}"` : ""}. Provide the main explanation and the Pro Tip in ${ul}, but any nuances and examples must strictly be in ${tl} (you may provide short ${ul} translations for the examples if helpful). Strictly JSON: { "explanation": "brief usage explanation in ${ul}", "nuances": ["nuance in ${ul} or ${tl}"], "examples": ["example in ${tl}"], "tip": "key takeaway in ${ul}" }`
          : `You are a professional dictionary assistant. The user is learning ${tl} and their native language is ${ul}. Explain "${word}" in 1 sentence in ${ul}, then provide 2 nuances in ${ul}, 2 examples in ${tl}, and a Pro Tip in ${ul}. Use perfect markdown.`;

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

    if (url.pathname === "/translate-stream") {
      try {
        const { word, context, provider, from, to } = await request.json() as { word: string, context?: string, provider?: string, from?: string, to?: string };
        if (!word) return new Response("Missing word", { status: 400 });

        const targetLang = to && to !== "vi" ? to : "Vietnamese";
        const sourceLang = from && from !== "auto" ? from : "the source language";

        const systemPrompt = `Translate and enrich the word "${word}" from ${sourceLang} to ${targetLang}${context ? ` using the following context: "${context}"` : ""}. Return ONLY a valid JSON object. Do not include markdown formatting. Format strictly as: { "word": "translated/root form", "meaning": "concise meaning in ${targetLang}", "phonetic": "IPA transcription", "context": "translated/simplified context" }`;

        const requestedModel = provider || "@cf/meta/llama-3.1-70b-instruct";
        let stream;

        try {
          if (requestedModel.startsWith("siliconflow/")) {
            const actualModel = requestedModel.substring(12);
            stream = await fetchOpenAIStream("https://api.siliconflow.cn/v1/chat/completions", env.SILICONFLOW_API_KEY || "", actualModel, [{ role: "system", content: systemPrompt }], true);
          } else if (requestedModel.startsWith("qwen/")) {
            const actualModel = requestedModel.substring(5);
            stream = await fetchOpenAIStream("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", env.QWEN_API_KEY || "", actualModel, [{ role: "system", content: systemPrompt }], true);
          } else if (requestedModel.startsWith("minmax/")) {
            const actualModel = requestedModel.substring(7);
            stream = await fetchOpenAIStream("https://api.minmax.chat/v1/chat/completions", env.MINMAX_API_KEY || "", actualModel, [{ role: "system", content: systemPrompt }], true);
          } else {
            stream = await env.AI.run(requestedModel, {
              messages: [
                { role: "system", content: systemPrompt }
              ],
              max_tokens: 300,
              stream: true,
              response_format: { type: "json_object" }
            });
          }
        } catch (modelError: any) {
          console.warn(`Model ${requestedModel} failed, falling back to default 8B. Error: ${modelError.message}`);
          // Fallback Logic
          stream = await env.AI.run("@cf/meta/llama-3-8b-instruct", {
            messages: [{ role: "system", content: systemPrompt }],
            max_tokens: 300,
            stream: true,
            response_format: { type: "json_object" }
          });
        }

        return new Response(stream, {
          headers: {
            "Content-Type": "text/event-stream",
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "Access-Control-Allow-Origin": "*"
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
    } catch { }

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
