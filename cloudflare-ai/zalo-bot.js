const ZaloBot = require("node-zalo-bot");

const token = "3026137945329371352:ACbYsTjScYXDbntSddMctMHqhADZydxwuthugPBbTTtvUvdWLtTNBgtDsshhfAmH";

async function sendZaloMessage(chatId, text) {
  try {
    const res = await fetch(`https://bot-api.zaloplatforms.com/bot${token}/sendMessage`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ chat_id: chatId.toString(), text })
    });
    console.log(`Gửi tín nhắn đến ${chatId}: HTTP ${res.status}`, await res.text());
  } catch (err) {
    console.error("Lỗi gửi tin Zalo:", err);
  }
}

const bot = new ZaloBot(token, {
  polling: true
});

bot.onText(/\/start/, (msg, match) => {
  console.log("Bạn vừa nhận lệnh /start từ", msg.chat.id);
  sendZaloMessage(msg.chat.id, `Chào ${msg.from.display_name}! Tôi là chatbot do LexiVocab AI Agent tạo ra!`);
});

bot.onText(/\/echo (.+)/, (msg, match) => {
  let message = match[1];
  if (message) {
    console.log("Bạn vừa nhận lệnh /echo từ", msg.chat.id);
    sendZaloMessage(msg.chat.id, `Bạn vừa nói: ${message}`);
  } else {
    sendZaloMessage(msg.chat.id, "Hãy nhập gì đó sau lệnh /echo");
  }
});

bot.on("message", (msg) => {
  console.log("Bạn vừa nhận được tin nhắn mới", msg.chat.id);
  console.log("Bạn vừa nhận được tin nhắn mới", msg);
});

console.log("Zalo Bot đang chạy ở chế độ Polling...");
