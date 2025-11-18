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
                "Отправь мне числа через запятую или пробел, и я:\n" +
                "• Разложу каждое число на простые множители\n" +
                "• Найду НОД и НОК всех чисел\n" +
                "• Покажу разложение НОД и НОК на простые числа\n\n" +
                "Пример: 12, 18, 24 или 12 18 24");
        }
        else if (text.StartsWith("/help"))
        {
            await botClient.SendTextMessageAsync(chatId,
                "Как пользоваться ботом:\n\n" +
                "• Отправь числа через запятую или пробел (от 2 до 2,147,483,647)\n" +
                "• Я разложу каждое число на простые множители\n" +
                "• Найду НОД (наибольший общий делитель) и НОК (наименьшее общее кратное)\n" +
                "• Покажу разложение НОД и НОК на простые числа\n\n" +
                "Примеры:\n" +
                "12, 18, 24\n" +
                "12 18 24\n" +
                "8, 12\n\n" +
                "Команды:\n" +
                "/start - начать работу\n" +
                "/help - справка");
        }
        else
        {
            var numbers = ParseNumbers(text);
            if (numbers.Count == 0)
            {
                await botClient.SendTextMessageAsync(chatId, 
                    "Не удалось найти числа. Введите числа через запятую или пробел.\n\nПример: 12, 18, 24");
            }
            else if (numbers.Count == 1)
            {
                // Одно число - только разложение
                var number = numbers[0];
                if (number < 2)
                    await botClient.SendTextMessageAsync(chatId, "Введите число больше 1.");
                else if (number > int.MaxValue)
                    await botClient.SendTextMessageAsync(chatId, "Число слишком большое. Введите до 2,147,483,647.");
                else
                    await botClient.SendTextMessageAsync(chatId, FormatFactorization(number, Factorize(number)));
            }
            else
            {
                // Несколько чисел - разложение + НОД + НОК
                var result = ProcessMultipleNumbers(numbers);
                await botClient.SendTextMessageAsync(chatId, result);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка update: {ex}");
    }

    return Results.Ok();
});

// 🔥 Добавлено для Render — НЕ засыпать
app.MapGet("/", () => "OK");
app.MapGet("/ping", () => "pong");

app.Run("http://0.0.0.0:10000");

List<long> ParseNumbers(string input)
{
    var numbers = new List<long>();
    
    // Разделяем по запятым или пробелам
    var parts = input.Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var part in parts)
    {
        if (long.TryParse(part.Trim(), out long number))
        {
            numbers.Add(number);
        }
    }
    
    return numbers;
}

string ProcessMultipleNumbers(List<long> numbers)
{
    var result = "";
    
    // Разложение каждого числа
    foreach (var number in numbers)
    {
        if (number < 2)
        {
            result += $"{number} - должно быть больше 1\n";
            continue;
        }
        
        var factors = Factorize(number);
        result += $"{number} = {FormatFactors(factors)}\n";
    }
    
    // Проверяем, что все числа валидны для НОД/НОК
    var validNumbers = numbers.Where(n => n >= 2).ToList();
    if (validNumbers.Count < 2)
    {
        result += "\n⚠️ Для вычисления НОД и НОК нужно минимум 2 числа больше 1.";
        return result;
    }
    
    // Вычисляем НОД и НОК
    var gcd = CalculateGCD(validNumbers);
    var lcm = CalculateLCM(validNumbers);
    
    result += $"\nНОД = {gcd} = {FormatFactors(Factorize(gcd))}\n";
    
    result += $"\nНОК = {lcm} = {FormatFactors(Factorize(lcm))}\n";
    
    return result;
}

long CalculateGCD(List<long> numbers)
{
    long result = numbers[0];
    for (int i = 1; i < numbers.Count; i++)
    {
        result = GCD(result, numbers[i]);
    }
    return result;
}

long CalculateLCM(List<long> numbers)
{
    long result = numbers[0];
    for (int i = 1; i < numbers.Count; i++)
    {
        result = LCM(result, numbers[i]);
    }
    return result;
}

long GCD(long a, long b)
{
    while (b != 0)
    {
        long temp = b;
        b = a % b;
        a = temp;
    }
    return a;
}

long LCM(long a, long b)
{
    return (a / GCD(a, b)) * b;
}

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
    return $"{number} = {FormatFactors(factors)}";
}

string FormatFactors(List<(long factor, int count)> factors)
{
    string ToSuperscript(int num)
    {
        var map = new Dictionary<char, char> {
            {'0','⁰'}, {'1','¹'}, {'2','²'}, {'3','³'}, {'4','⁴'},
            {'5','⁵'}, {'6','⁶'}, {'7','⁷'}, {'8','⁸'}, {'9','⁹'}
        };
        return string.Concat(num.ToString().Select(c => map[c]));
    }

    return string.Join(" × ", factors
        .Select(f => f.count > 1 ? $"{f.factor}{ToSuperscript(f.count)}" : f.factor.ToString()));
}