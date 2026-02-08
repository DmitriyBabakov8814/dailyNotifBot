using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramPlannerBot.Models;
using TelegramPlannerBot.Services;
using TelegramPlannerBot.UI;

namespace TelegramPlannerBot.Handlers
{
    public class CommandHandler
    {
        private readonly PlannerService _plannerService;

        public CommandHandler(PlannerService plannerService)
        {
            _plannerService = plannerService;
        }

        public async Task HandleViewPlans(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var now = _plannerService.GetUserCurrentTime(chatId);
            var dates = _plannerService.GetDatesWithPlans(chatId, now);

            if (dates.Count == 0)
            {
                await bot.SendMessage(chatId, "📅 У вас нет запланированных событий.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            await bot.SendMessage(chatId, "📅 Выберите дату для просмотра:",
                replyMarkup: KeyboardHelper.GetDatesKeyboard(dates, now), cancellationToken: ct);
        }

        public async Task HandleDateSelection(ITelegramBotClient bot, long chatId, string messageText, CancellationToken ct)
        {
            var now = _plannerService.GetUserCurrentTime(chatId);
            DateTime targetDate;

            if (messageText.Contains("Сегодня"))
            {
                targetDate = now.Date;
            }
            else if (messageText.Contains("Завтра"))
            {
                targetDate = now.Date.AddDays(1);
            }
            else
            {
                // Парсим дату из кнопки формата "📅 20.02.2026"
                var dateStr = messageText.Replace("📅 ", "").Trim();
                if (!DateTime.TryParseExact(dateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out targetDate))
                {
                    if (!DateTime.TryParseExact(dateStr, "dd.MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out targetDate))
                    {
                        await bot.SendMessage(chatId, "❌ Не удалось распознать дату.",
                            replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                        return;
                    }
                    targetDate = new DateTime(now.Year, targetDate.Month, targetDate.Day);
                }
            }

            var plans = _plannerService.GetPlansForDate(chatId, targetDate);

            if (plans.Count == 0)
            {
                await bot.SendMessage(chatId, $"📅 На {targetDate:dd.MM.yyyy} планов нет.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }
            else
            {
                var sb = new StringBuilder($"📅 Планы на {targetDate:dd.MM.yyyy}:\n\n");
                foreach (var plan in plans)
                {
                    sb.AppendLine(PlannerService.FormatPlan(plan));
                }
                await bot.SendMessage(chatId, sb.ToString(),
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }
        }

        public async Task StartSearch(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            session.State = UserState.WaitingForSearchQuery;
            await bot.SendMessage(chatId, "🔍 Введите ключевое слово:",
                replyMarkup: KeyboardHelper.GetCancelKeyboard(), cancellationToken: ct);
        }

        public async Task HandleSearch(ITelegramBotClient bot, long chatId, string query, UserSession session, CancellationToken ct)
        {
            if (query == "❌ Отмена" || query == "🏠 В меню")
            {
                session.State = UserState.None;
                await bot.SendMessage(chatId, "Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            var results = _plannerService.SearchPlans(chatId, query);

            if (results.Count == 0)
            {
                await bot.SendMessage(chatId, $"🔍 По запросу '{query}' ничего не найдено.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }
            else
            {
                var sb = new StringBuilder($"🔍 Найдено ({results.Count}):\n\n");
                foreach (var plan in results.Take(15))
                {
                    sb.AppendLine($"📅 {plan.DateTime:dd.MM.yyyy} {PlannerService.FormatPlan(plan)}");
                }
                if (results.Count > 15)
                    sb.AppendLine($"\n...и ещё {results.Count - 15}");

                await bot.SendMessage(chatId, sb.ToString(),
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }

            session.State = UserState.None;
        }

        public async Task HandleHelp(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var help = @"📖 Как пользоваться:

➕ ДОБАВИТЬ - Быстрое создание (дата → время → описание → повтор)
📅 МОИ ПЛАНЫ - Выбрать дату для просмотра
✏️ РЕДАКТИРОВАТЬ - Изменить любое поле
🗑 УДАЛИТЬ - Удалить по номерам / все на дату / все повторяющиеся
🔍 ПОИСК - Найти по слову

🔄 ПОВТОРЕНИЯ:
Создайте план, затем выберите период повтора (день/неделя/месяц)

⏰ УВЕДОМЛЕНИЯ:
• Каждый день в 8:00 - список планов
• Перед событием - напоминание

💡 СОВЕТЫ:
• В любой момент: 🏠 В меню
• Удаление по номерам: 1 2 5 7 (через пробел)
• Быстрый ввод: Завтра в 15:00 встреча";

            await bot.SendMessage(chatId, help,
                replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
        }
    }
}