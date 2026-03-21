const TelegramBot = require('node-telegram-bot-api');

// Thay bằng Token của Bot Telegram của bạn (Lấy từ @BotFather)
const token = '8706515807:AAFaT8Ah-kf95TfZSly_vUVPSFx1fZp2dKw';

// Khởi tạo bot ở chế độ Polling
const bot = new TelegramBot(token, { polling: true });

console.log("Bot Telegram đang chạy. Hãy mở Telegram và gửi một Sticker bất kỳ cho Bot này...");

// Lắng nghe sự kiện người dùng gửi Sticker
bot.on('sticker', (msg) => {
  const chatId = msg.chat.id;
  const stickerId = msg.sticker.file_id; // Đây chính là ID của sticker

  console.log("\n=========================================");
  console.log("👤 Người gửi Chat ID:", chatId);
  console.log("🎯 Sticker ID (file_id):", stickerId);
  console.log("=========================================\n");

  // Bot gửi lại chính sticker đó để xác nhận
  bot.sendSticker(chatId, stickerId);
  bot.sendMessage(chatId, `Đây là mã Sticker ID của bạn (hãy copy & dán vào C#):\n\n${stickerId}`);
});

// Lắng nghe các tin nhắn khác không phải sticker
bot.on('message', (msg) => {
  if (!msg.sticker) {
    bot.sendMessage(msg.chat.id, "Đây không phải là Sticker! Vui lòng chọn và gửi một Sticker bất kỳ nhé.");
  }
});
