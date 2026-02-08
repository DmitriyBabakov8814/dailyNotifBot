using System;
using System.Threading.Tasks;
using TelegramPlannerBot.Bot;

namespace TelegramPlannerBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════");
            Console.WriteLine("  ТЕЛЕГРАМ БОТ-ЕЖЕДНЕВНИК v2.0");
            Console.WriteLine("═══════════════════════════════════\n");

            Console.Write("Введите токен бота: ");
            var botToken = Console.ReadLine();

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
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                Console.WriteLine("\nПроверьте:");
                Console.WriteLine("1. Правильность токена");
                Console.WriteLine("2. Подключение к интернету");
                Console.WriteLine("3. Доступность Telegram API");
            }
        }
    }
}