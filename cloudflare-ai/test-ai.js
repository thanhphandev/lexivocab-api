/**
 * Cloudflare AI Worker Integration Test Script
 * Run this with: node test-ai.js
 */

const API_KEY = "161004"; // From appsettings.json
const WORKER_URL = "https://lexivocab-ai-worker.openswift-ai.workers.dev"; // From appsettings.json
const TEST_WORD = "serendipity";

async function callAIEndpoint(endpoint, payload) {
  console.log(`\n🚀 Testing [${endpoint}] for: "${payload.word}"...`);
  try {
    const response = await fetch(`${WORKER_URL}${endpoint}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${API_KEY}`
      },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      const errorText = await response.text();
      console.error(`❌ [${endpoint}] failed! Status: ${response.status}`);
      console.error(`   Details: ${errorText}`);
      return null;
    }

    const data = await response.json();
    console.log(`✅ [${endpoint}] success:`);
    console.log(JSON.stringify(data, null, 2));
    return data;
  } catch (error) {
    console.error(`❌ [${endpoint}] network error: ${error.message}`);
    return null;
  }
}

async function runAllTests() {
  console.log("=== LexiVocab AI Worker Test Suite ===");
  
  // 1. Basic Enrichment
  await callAIEndpoint("/enrich-word", { word: TEST_WORD });

  // 2. Usage Explanation
  await callAIEndpoint("/explain-usage", { 
    word: TEST_WORD, 
    context: "I found a $20 bill on the street while looking for my lost cat." 
  });

  // 3. Related Words
  await callAIEndpoint("/suggest-related", { word: TEST_WORD });

  // 4. Quiz Generation
  await callAIEndpoint("/generate-quiz", { word: TEST_WORD });

  console.log("\n=== Test Suite Completed ===");
}

runAllTests();
