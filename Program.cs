using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;

var builder = WebApplication.CreateBuilder(args);

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
var webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK_URL");

if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(webhookUrl))
{
    Console.WriteLine("Ошибка: TELEGRAM_BOT_TOKEN или WEBHOOK_URL не заданы!");
    return;
}

var botClient = new TelegramBotClient(token);
await botClient.SetWebhookAsync($"{webhookUrl}/bot");

var app = builder.Build();

app.MapPost("/bot", async (HttpRequest request) =>
{
    try
    {
        var update = await request.ReadFromJsonAsync<Update>();
        if (update?.Message?.Text == null) return Results.Ok();

        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text;
        
        Console.WriteLine($"Получено сообщение '{text}' в чате {chatId}.");        

        if (text.StartsWith("/start"))
        {
            await botClient.SendTextMessageAsync(chatId,
                "Привет! Я бот для разложения чисел на простые множители.\n" +
                "Просто отправь мне любое целое число, и я разложу его на простые множители.\n\nПример: 84");
        }
        else if (text.StartsWith("/help"))
        {
            await botClient.SendTextMessageAsync(chatId,
                "Как пользоваться ботом:\n\n" +
                "• Отправь любое целое число (от 2 до 2,147,483,647)\n" +
                "• Я разложу его на простые множители\n" +
                "• Пример: 84 = 2² × 3 × 7\n\n" +
                "Команды:\n" +
                "/start - начать работу\n" +
                "/help - справка");
        }
        else if (long.TryParse(text, out long number))
        {
            if (number < 2)
                await botClient.SendTextMessageAsync(chatId, "Введите число больше 1.");
            else if (number > int.MaxValue)
                await botClient.SendTextMessageAsync(chatId, "Число слишком большое. Введите до 2,147,483,647.");
            else
                await botClient.SendTextMessageAsync(chatId, FormatFactorization(number, Factorize(number)));
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Введите корректное целое число.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка update: {ex}");
    }

    return Results.Ok();
});

app.Run("http://0.0.0.0:10000");

List<(long factor, int count)> Factorize(long n)
{
    var factors = new List<(long, int)>();
    long temp = n;
    int count = 0;

    while (temp % 2 == 0) { temp /= 2; count++; }
    if (count > 0) factors.Add((2, count));

    for (long i = 3; i * i <= temp; i += 2)
    {
        count = 0;
        while (temp % i == 0) { temp /= i; count++; }
        if (count > 0) factors.Add((i, count));
    }

    if (temp > 1) factors.Add((temp, 1));

    return factors;
}

string FormatFactorization(long number, List<(long factor, int count)> factors)
{
    if (!factors.Any()) return $"{number} - простое число";

    string ToSuperscript(int num)
    {
        var map = new Dictionary<char, char> {
            {'0','⁰'}, {'1','¹'}, {'2','²'}, {'3','³'}, {'4','⁴'},
            {'5','⁵'}, {'6','⁶'}, {'7','⁷'}, {'8','⁸'}, {'9','⁹'}
        };
        return string.Concat(num.ToString().Select(c => map[c]));
    }

    var factorization = string.Join(" × ", factors
        .Select(f => f.count > 1 ? $"{f.factor}{ToSuperscript(f.count)}" : f.factor.ToString()));

    return $"{number} == {factorization}";
}