using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace PinterestBot
{
    class Program
    {
        private static TelegramBotClient botClient;
        private static Dictionary<long, List<string>> userPictureCache = new Dictionary<long, List<string>>();
        private static Dictionary<long, string> userAccountCache = new Dictionary<long, string>();

        static void Main()
        {
            botClient = new TelegramBotClient(Config.TelegramApiToken);
            botClient.OnMessage += Bot_OnMessage;
            botClient.OnCallbackQuery += Bot_OnCallbackQuery;
            botClient.StartReceiving();
            Console.WriteLine("Бот запущен. Нажмите любую клавишу для выхода.");
            Console.ReadKey();
            botClient.StopReceiving();
        }

        private static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Type == MessageType.Text)
            {
                var chatId = e.Message.Chat.Id;
                var messageText = e.Message.Text;

                if (messageText.StartsWith("/start"))
                {
                    await botClient.SendTextMessageAsync(chatId, "Добро пожаловать! Пожалуйста, укажите ссылку на свою учетную запись Pinterest.");
                }
                else if (messageText.StartsWith("http") || messageText.StartsWith("www"))
                {
                    var pictures = await GetPinterestPictures(messageText);
                    if (pictures.Count >= 3)
                    {
                        var initialPictures = pictures.Take(3).ToList();
                        userPictureCache[chatId] = pictures.Skip(3).ToList();
                        userAccountCache[chatId] = messageText;
                        await SendPictures(chatId, initialPictures);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Недостаточно изображений в указанной учетной записи Pinterest.");
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Неверный ввод. Пожалуйста, укажите корректную ссылку на учетную запись Pinterest.");
                }
            }
        }

        private static async void Bot_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            var callbackQuery = e.CallbackQuery;
            var chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data == "Далее")
            {
                if (userPictureCache.ContainsKey(chatId))
                {
                    var pictures = userPictureCache[chatId];
                    if (pictures.Count >= 3)
                    {
                        var nextPictures = pictures.Take(3).ToList();
                        userPictureCache[chatId] = pictures.Skip(3).ToList();
                        await SendPictures(chatId, nextPictures);
                    }
                    else
                    {
                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Больше нет доступных изображений.");
                    }
                }
            }
            else if (callbackQuery.Data == "Выбрать другой аккаунт")
            {
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                await botClient.SendTextMessageAsync(chatId, "Пожалуйста, укажите ссылку на другую учетную запись Pinterest.");
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
        }

        private static async Task SendPictures(long chatId, List<string> pictures)
        {
            foreach (var picture in pictures)
            {
                using (var stream = new MemoryStream(await DownloadImage(picture)))
                {
                    var inputFile = new InputOnlineFile(stream);
                    await botClient.SendPhotoAsync(chatId, inputFile);
                }
            }

            if (userPictureCache.ContainsKey(chatId) && userPictureCache[chatId].Count >= 3)
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Далее"),
                        InlineKeyboardButton.WithCallbackData("Выбрать другой аккаунт")
                    }

                });

                await botClient.SendTextMessageAsync(chatId, "Нажмите 'Далее', чтобы увидеть больше изображений или 'Выбрать другой аккаунт', чтобы выбрать другую учетную запись.", replyMarkup: inlineKeyboard);
            }
            else if (userAccountCache.ContainsKey(chatId))
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Выбрать другой аккаунт")
                    }
                });

                await botClient.SendTextMessageAsync(chatId, "Больше нет доступных изображений. Нажмите 'Выбрать другой аккаунт', чтобы выбрать другую учетную запись.", replyMarkup: inlineKeyboard);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Больше нет доступных изображений.");
            }
        }

        private static async Task<List<string>> GetPinterestPictures(string pinterestLink)
        {
            var pictures = new List<string>();

            try
            {
                var web = new HtmlWeb();
                var document = await web.LoadFromWebAsync(pinterestLink);
                var imageNodes = document.DocumentNode.SelectNodes($"//div[contains(@class, 'XiG') and contains(@class, 'zI7') and contains(@class, 'iyn') and contains(@class, 'Hsu')]//img");

                if (imageNodes != null)
                {
                    foreach (var imageNode in imageNodes)
                    {
                        var imageUrl = imageNode.GetAttributeValue("src", "");
                        pictures.Add(imageUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении изображений Pinterest: {ex.Message}");
            }

            return pictures;
        }

        private static async Task<byte[]> DownloadImage(string imageUrl)
        {
            using (var client = new WebClient())
            {
                var imageBytes = await client.DownloadDataTaskAsync(imageUrl);
                return imageBytes;
            }
        }
    }
}
