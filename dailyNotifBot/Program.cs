using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TelegramPlannerBot.Bot;

namespace TelegramPlannerBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════");
            Console.WriteLine("  ТЕЛЕГРАМ БОТ-ЕЖЕДНЕВНИК v2.1");
            Console.WriteLine("  Production Ready Edition");
            Console.WriteLine("═══════════════════════════════════\n");

            string botToken;

            // Читаем токен из appsettings.json (для продакшена)
            if (File.Exists("appsettings.json"))
            {
                try
                {
                    var json = File.ReadAllText("appsettings.json");
                    var config = JsonSerializer.Deserialize<Config>(json);
                    botToken = config?.BotToken ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(botToken))
                    {
                        Console.WriteLine("✅ Токен загружен из appsettings.json");
                    }
                    else
                    {
                        Console.Write("Введите токен бота: ");
                        botToken = Console.ReadLine() ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Ошибка чтения appsettings.json: {ex.Message}");
                    Console.Write("Введите токен бота: ");
                    botToken = Console.ReadLine() ?? string.Empty;
                }
            }
            else
            {
                Console.Write("Введите токен бота: ");
                botToken = Console.ReadLine() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(botToken))
            {
                Console.WriteLine("❌ Токен не может быть пустым!");
                return;
            }

            try
            {
                var bot = new TelegramBotService(botToken);
                await bot.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Критическая ошибка: {ex.Message}");
                Console.WriteLine($"Тип: {ex.GetType().Name}");
                Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
                Console.WriteLine("\nПроверьте:");
                Console.WriteLine("1. Правильность токена");
                Console.WriteLine("2. Подключение к интернету");
                Console.WriteLine("3. Доступность api.telegram.org");
                Console.WriteLine("4. Права доступа к файлам");

                Console.WriteLine("\nНажмите Enter для выхода...");
                Console.ReadLine();
            }
        }
    }

    public class Config
    {
        public string BotToken { get; set; } = string.Empty;
    }
}