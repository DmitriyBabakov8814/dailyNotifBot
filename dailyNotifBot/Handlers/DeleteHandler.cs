using System;
using System.Collections.Generic;
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
    public class DeleteHandler
    {
        private readonly PlannerService _plannerService;

        public DeleteHandler(PlannerService plannerService)
        {
            _plannerService = plannerService;
        }

        public async Task StartDelete(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            var now = _plannerService.GetUserCurrentTime(chatId);
            var plans = _plannerService.GetAllUpcomingPlans(chatId, now);

            if (plans.Count == 0)
            {
                await bot.SendMessage(chatId, "📅 Нет планов для удаления.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            session.TempPlansList = plans.Take(20).ToList(); // Максимум 20
            session.State = UserState.WaitingForDeleteConfirmation;

            var sb = new StringBuilder($"🗑 Удаление ({plans.Count} планов)\n\n");

            for (int i = 0; i < Math.Min(session.TempPlansList.Count, 20); i++)
            {
                sb.AppendLine($"{i + 1}. {PlannerService.FormatPlan(session.TempPlansList[i])}");
            }

            if (plans.Count > 20)
                sb.AppendLine($"\n...и ещё {plans.Count - 20}");

            sb.AppendLine("\nВыберите способ:");

            await bot.SendMessage(chatId, sb.ToString(),
                replyMarkup: KeyboardHelper.GetDeleteOptionsKeyboard(), cancellationToken: ct);
        }

        public async Task HandleDeleteChoice(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "🏠 В меню")
            {
                session.State = UserState.None;
                session.TempPlansList.Clear();
                await bot.SendMessage(chatId, "Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            if (messageText == "🗑 По номерам (1 2 3)")
            {
                await bot.SendMessage(chatId,
                    "Введите номера планов через пробел:\nНапример: 1 2 5 7",
                    replyMarkup: KeyboardHelper.GetCancelKeyboard(), cancellationToken: ct);
                return;
            }

            if (messageText == "🗑 Все на эту дату")
            {
                await bot.SendMessage(chatId,
                    "Введите дату (ДД.ММ.ГГГГ):\nНапример: 20.02.2026",
                    replyMarkup: KeyboardHelper.GetCancelKeyboard(), cancellationToken: ct);
                session.TempValue = "delete_by_date";
                return;
            }

            if (messageText == "🗑 Все повторяющиеся")
            {
                var sb = new StringBuilder("Выберите повторяющееся событие:\n\n");
                var recurring = session.TempPlansList
                    .Where(p => !string.IsNullOrEmpty(p.ParentRecurrenceId) ||
                               session.TempPlansList.Any(x => x.ParentRecurrenceId == p.Id))
                    .GroupBy(p => string.IsNullOrEmpty(p.ParentRecurrenceId) ? p.Id : p.ParentRecurrenceId)
                    .Select(g => g.First())
                    .Take(10)
                    .ToList();

                if (recurring.Count == 0)
                {
                    await bot.SendMessage(chatId, "❌ Нет повторяющихся планов.",
                        replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                    session.State = UserState.None;
                    return;
                }

                for (int i = 0; i < recurring.Count; i++)
                {
                    sb.AppendLine($"{i + 1}. {recurring[i].Description} ({PlannerService.GetRecurrenceName(recurring[i].Recurrence)})");
                }

                session.TempPlansList = recurring;
                session.TempValue = "delete_recurring";
                await bot.SendMessage(chatId, sb.ToString(), cancellationToken: ct);
                return;
            }

            // Обработка ввода номеров
            await HandleNumbersInput(bot, chatId, messageText, session, ct);
        }

        private async Task HandleNumbersInput(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            try
            {
                if (messageText == "🏠 В меню" || messageText == "❌ Отмена")
                {
                    session.State = UserState.None;
                    await bot.SendMessage(chatId, "Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                    return;
                }

                // Удаление по дате
                if (session.TempValue == "delete_by_date")
                {
                    if (DateTime.TryParseExact(messageText, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
                    {
                        var count = _plannerService.DeletePlansByDate(chatId, date);
                        await bot.SendMessage(chatId, $"✅ Удалено {count} планов на {date:dd.MM.yyyy}",
                            replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                        session.State = UserState.None;
                        session.TempValue = string.Empty;
                    }
                    else
                    {
                        await bot.SendMessage(chatId, "❌ Неверный формат даты! ДД.ММ.ГГГГ", cancellationToken: ct);
                    }
                    return;
                }

                // Удаление повторяющихся
                if (session.TempValue == "delete_recurring")
                {
                    if (int.TryParse(messageText, out int num) && num > 0 && num <= session.TempPlansList.Count)
                    {
                        var plan = session.TempPlansList[num - 1];
                        var parentId = string.IsNullOrEmpty(plan.ParentRecurrenceId) ? plan.Id : plan.ParentRecurrenceId;
                        var count = _plannerService.DeleteRecurringPlans(chatId, parentId);

                        await bot.SendMessage(chatId, $"✅ Удалено {count} повторяющихся планов: {plan.Description}",
                            replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                        session.State = UserState.None;
                        session.TempValue = string.Empty;
                    }
                    else
                    {
                        await bot.SendMessage(chatId, $"❌ Неверный номер! Введите от 1 до {session.TempPlansList.Count}", cancellationToken: ct);
                    }
                    return;
                }

                // Удаление по номерам (1 2 3 4)
                var numbers = messageText.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out int n) ? n : 0)
                    .Where(n => n > 0 && n <= session.TempPlansList.Count)
                    .Distinct()
                    .ToList();

                if (numbers.Count == 0)
                {
                    await bot.SendMessage(chatId, "❌ Неверный формат!\nВведите номера через пробел: 1 2 5", cancellationToken: ct);
                    return;
                }

                var idsToDelete = numbers.Select(n => session.TempPlansList[n - 1].Id).ToList();
                var deleted = _plannerService.DeleteMultiplePlans(chatId, idsToDelete);

                await bot.SendMessage(chatId, $"✅ Удалено планов: {deleted}",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);

                session.State = UserState.None;
                session.TempPlansList.Clear();
            }
            catch (Exception ex)
            {
                await bot.SendMessage(chatId,
                    $"❌ Ошибка: {ex.Message}\n\nВведите команду заново или вернитесь в меню.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                session.State = UserState.None;
            }
        }
    }
}