using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramPlannerBot.Models;
using TelegramPlannerBot.Services;
using TelegramPlannerBot.UI;

namespace TelegramPlannerBot.Handlers
{
    public class SimplePlanCreationHandler
    {
        private readonly PlannerService _plannerService;

        public SimplePlanCreationHandler(PlannerService plannerService)
        {
            _plannerService = plannerService;
        }

        public async Task HandleDateTimeSelection(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "❌ Отмена" || messageText == "🏠 В меню")
            {
                session.State = UserState.None;
                session.CurrentPlan = null;
                await bot.SendMessage(chatId, "Отменено.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            DateTime selectedDate;

            if (messageText.Contains("Сегодня"))
                selectedDate = _plannerService.GetUserCurrentTime(chatId).Date;
            else if (messageText.Contains("Завтра"))
                selectedDate = _plannerService.GetUserCurrentTime(chatId).Date.AddDays(1);
            else if (messageText.Contains("Послезавтра"))
                selectedDate = _plannerService.GetUserCurrentTime(chatId).Date.AddDays(2);
            else if (messageText == "✏️ Ввести дату")
            {
                await bot.SendMessage(chatId, "Введите дату (ДД.ММ.ГГГГ):", cancellationToken: ct);
                return;
            }
            else if (DateTime.TryParseExact(messageText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedDate))
            {
                // OK
            }
            else
            {
                await bot.SendMessage(chatId, "❌ Неверный формат!\nИспользуйте: ДД.ММ.ГГГГ", cancellationToken: ct);
                return;
            }

            session.CurrentPlan = new PlanItem { ChatId = chatId, DateTime = selectedDate };
            session.State = UserState.WaitingForTime;

            await bot.SendMessage(chatId, $"✅ {selectedDate:dd.MM.yyyy}\n\n🕐 Время:",
                replyMarkup: KeyboardHelper.GetTimeQuickSelectKeyboard(), cancellationToken: ct);
        }

        public async Task HandleTimeSelection(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "❌ Отмена" || messageText == "🏠 В меню")
            {
                session.State = UserState.None;
                session.CurrentPlan = null;
                await bot.SendMessage(chatId, "Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            TimeSpan selectedTime;

            if (messageText.Contains("09:00")) selectedTime = new TimeSpan(9, 0, 0);
            else if (messageText.Contains("12:00")) selectedTime = new TimeSpan(12, 0, 0);
            else if (messageText.Contains("15:00")) selectedTime = new TimeSpan(15, 0, 0);
            else if (messageText.Contains("18:00")) selectedTime = new TimeSpan(18, 0, 0);
            else if (messageText.Contains("20:00")) selectedTime = new TimeSpan(20, 0, 0);
            else if (messageText.Contains("21:00")) selectedTime = new TimeSpan(21, 0, 0);
            else if (messageText == "✏️ Ввести время")
            {
                await bot.SendMessage(chatId, "Введите время (ЧЧ:ММ):", cancellationToken: ct);
                return;
            }
            else if (TimeSpan.TryParseExact(messageText, "hh\\:mm", CultureInfo.InvariantCulture, out selectedTime))
            {
                // OK
            }
            else
            {
                await bot.SendMessage(chatId, "❌ Неверный формат! ЧЧ:ММ", cancellationToken: ct);
                return;
            }

            session.CurrentPlan!.DateTime = session.CurrentPlan.DateTime.Date + selectedTime;
            session.State = UserState.WaitingForDescription;

            await bot.SendMessage(chatId,
                $"✅ {session.CurrentPlan.DateTime:dd.MM.yyyy HH:mm}\n\n📝 Описание:",
                replyMarkup: KeyboardHelper.GetCancelKeyboard(),
                cancellationToken: ct);
        }

        public async Task HandleDescription(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "❌ Отмена" || messageText == "🏠 В меню")
            {
                session.State = UserState.None;
                await bot.SendMessage(chatId, "Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            session.CurrentPlan!.Description = messageText;
            session.State = UserState.WaitingForNotificationTime;

            await bot.SendMessage(chatId, "⏰ За сколько напомнить?",
                replyMarkup: KeyboardHelper.GetNotificationTimeKeyboard(), cancellationToken: ct);
        }

        public async Task HandleNotificationTime(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "❌ Отмена" || messageText == "🏠 В меню")
            {
                session.State = UserState.None;
                await bot.SendMessage(chatId, "Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            session.CurrentPlan!.NotificationMinutes = KeyboardHelper.ParseNotificationMinutes(messageText);
            session.State = UserState.WaitingForRecurrence;

            await bot.SendMessage(chatId, "🔄 Повторять?",
                replyMarkup: KeyboardHelper.GetRecurrenceKeyboard(), cancellationToken: ct);
        }

        public async Task HandleRecurrence(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "❌ Отмена" || messageText == "🏠 В меню")
            {
                session.State = UserState.None;
                await bot.SendMessage(chatId, "Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            if (messageText != "⏭ Не повторять")
            {
                session.CurrentPlan!.Recurrence = KeyboardHelper.ParseRecurrence(messageText);
                if (session.CurrentPlan.Recurrence != RecurrenceType.None)
                {
                    session.CurrentPlan.RecurrenceEndDate = session.CurrentPlan.DateTime.AddMonths(3);
                }
            }

            // Сохраняем план
            _plannerService.AddPlan(session.CurrentPlan!);

            var notifText = session.CurrentPlan!.NotificationMinutes == 60
                ? "1 час"
                : $"{session.CurrentPlan.NotificationMinutes} мин";

            var recurrenceInfo = session.CurrentPlan.Recurrence != RecurrenceType.None
                ? $"\n🔄 {PlannerService.GetRecurrenceName(session.CurrentPlan.Recurrence)}"
                : "";

            await bot.SendMessage(chatId,
                $"✅ План создан!\n\n" +
                $"📅 {session.CurrentPlan.DateTime:dd.MM.yyyy HH:mm}\n" +
                $"📝 {session.CurrentPlan.Description}\n" +
                $"⏰ Напомнить за {notifText}{recurrenceInfo}",
                replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);

            session.State = UserState.None;
            session.CurrentPlan = null;
        }
    }
}