using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramPlannerBot.Models;
using TelegramPlannerBot.Services;
using TelegramPlannerBot.UI;

namespace TelegramPlannerBot.Handlers
{
    public class PlanEditHandler
    {
        private readonly PlannerService _plannerService;

        public PlanEditHandler(PlannerService plannerService)
        {
            _plannerService = plannerService;
        }

        public async Task StartEdit(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            var now = _plannerService.GetUserCurrentTime(chatId);
            var plans = _plannerService.GetAllUpcomingPlans(chatId, now);

            if (plans.Count == 0)
            {
                await bot.SendMessage(chatId, "📅 Нет планов для редактирования.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            session.TempPlansList = plans;
            session.State = UserState.WaitingForEditSelection;

            var sb = new StringBuilder("✏️ Выберите план для редактирования:\n\n");
            for (int i = 0; i < Math.Min(plans.Count, 10); i++)
            {
                sb.AppendLine($"{i + 1}. {PlannerService.FormatPlan(plans[i])}");
            }
            sb.AppendLine("\nВведите номер:");

            await bot.SendMessage(chatId, sb.ToString(), cancellationToken: ct);
        }

        public async Task HandlePlanSelection(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (int.TryParse(messageText, out int planNumber) && planNumber > 0 && planNumber <= session.TempPlansList.Count)
            {
                session.CurrentPlan = session.TempPlansList[planNumber - 1];
                session.State = UserState.WaitingForEditChoice;

                var plan = session.CurrentPlan;
                var info = PlannerService.FormatPlan(plan, detailed: true);

                await bot.SendMessage(chatId, $"Текущий план:\n\n{info}\n\nЧто изменить?",
                    replyMarkup: KeyboardHelper.GetEditFieldKeyboard(), cancellationToken: ct);
            }
            else
            {
                await bot.SendMessage(chatId, $"❌ Неверный номер! Введите от 1 до {session.TempPlansList.Count}", cancellationToken: ct);
            }
        }

        public async Task HandleFieldChoice(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "❌ Отмена")
            {
                session.State = UserState.None;
                session.CurrentPlan = null;
                await bot.SendMessage(chatId, "❌ Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            session.EditField = messageText;
            session.State = UserState.WaitingForEditValue;

            var prompt = messageText switch
            {
                "📅 Дату" => "Введите новую дату (ДД.ММ.ГГГГ):",
                "🕐 Время" => "Введите новое время (ЧЧ:ММ):",
                "📝 Описание" => "Введите новое описание:",
                "🏷 Категорию" => "Выберите категорию:",
                "⚡ Приоритет" => "Выберите приоритет:",
                "⏰ Уведомление" => "За сколько напомнить?",
                "📍 Местоположение" => "Введите место:",
                "📋 Заметки" => "Введите заметки:",
                _ => "Введите новое значение:"
            };

            var keyboard = messageText switch
            {
                "🏷 Категорию" => KeyboardHelper.GetCategoryKeyboard(),
                "⚡ Приоритет" => KeyboardHelper.GetPriorityKeyboard(),
                "⏰ Уведомление" => KeyboardHelper.GetNotificationTimeKeyboard(),
                _ => KeyboardHelper.GetCancelKeyboard()
            };

            await bot.SendMessage(chatId, prompt, replyMarkup: keyboard, cancellationToken: ct);
        }

        public async Task HandleNewValue(ITelegramBotClient bot, long chatId, string messageText, UserSession session, CancellationToken ct)
        {
            if (messageText == "❌ Отмена")
            {
                session.State = UserState.None;
                await bot.SendMessage(chatId, "❌ Отменено.", replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
                return;
            }

            var plan = session.CurrentPlan!;

            try
            {
                switch (session.EditField)
                {
                    case "📅 Дату":
                        if (DateTime.TryParseExact(messageText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime newDate))
                        {
                            plan.DateTime = newDate.Date + plan.DateTime.TimeOfDay;
                        }
                        else
                        {
                            await bot.SendMessage(chatId, "❌ Неверный формат даты!", cancellationToken: ct);
                            return;
                        }
                        break;

                    case "🕐 Время":
                        if (TimeSpan.TryParseExact(messageText, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan newTime))
                        {
                            plan.DateTime = plan.DateTime.Date + newTime;
                        }
                        else
                        {
                            await bot.SendMessage(chatId, "❌ Неверный формат времени!", cancellationToken: ct);
                            return;
                        }
                        break;

                    case "📝 Описание":
                        plan.Description = messageText;
                        break;

                    case "🏷 Категорию":
                        plan.Category = KeyboardHelper.ParseCategory(messageText);
                        break;

                    case "⚡ Приоритет":
                        plan.Priority = KeyboardHelper.ParsePriority(messageText);
                        break;

                    case "⏰ Уведомление":
                        plan.NotificationMinutes = KeyboardHelper.ParseNotificationMinutes(messageText);
                        break;

                    case "📍 Местоположение":
                        plan.Location = messageText;
                        break;

                    case "📋 Заметки":
                        plan.Notes = messageText;
                        break;
                }

                _plannerService.UpdatePlan(plan);

                await bot.SendMessage(chatId, $"✅ План обновлён!\n\n{PlannerService.FormatPlan(plan, detailed: true)}",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);

                session.State = UserState.None;
                session.CurrentPlan = null;
            }
            catch (Exception ex)
            {
                await bot.SendMessage(chatId, $"❌ Ошибка: {ex.Message}", cancellationToken: ct);
            }
        }
    }
}