using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramPlannerBot.Handlers;
using TelegramPlannerBot.Models;
using TelegramPlannerBot.Services;
using TelegramPlannerBot.UI;

namespace TelegramPlannerBot.Bot
{
    public class TelegramBotService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly PlannerService _plannerService;
        private readonly Dictionary<long, UserSession> _sessions;

        // Handlers
        private readonly SimplePlanCreationHandler _creationHandler;
        private readonly PlanEditHandler _editHandler;
        private readonly CommandHandler _commandHandler;
        private readonly DeleteHandler _deleteHandler;

        public TelegramBotService(string botToken)
        {
            _botClient = new TelegramBotClient(botToken);
            _plannerService = new PlannerService();
            _sessions = new Dictionary<long, UserSession>();

            _creationHandler = new SimplePlanCreationHandler(_plannerService);
            _editHandler = new PlanEditHandler(_plannerService);
            _commandHandler = new CommandHandler(_plannerService);
            _deleteHandler = new DeleteHandler(_plannerService);
        }

        public async Task Start()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            var cts = new CancellationTokenSource();

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await _botClient.GetMe();
            Console.WriteLine($"✅ Бот @{me.Username} запущен!");

            // Запуск фоновых задач
            _ = Task.Run(() => DailyNotificationTask(cts.Token));
            _ = Task.Run(() => EventNotificationTask(cts.Token));

            Console.WriteLine("Нажмите Enter для остановки...");
            Console.ReadLine();
            cts.Cancel();
        }

        private UserSession GetSession(long chatId)
        {
            if (!_sessions.ContainsKey(chatId))
            {
                _sessions[chatId] = new UserSession();
            }
            return _sessions[chatId];
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            try
            {
                if (update.Message is not { } message) return;

                var chatId = message.Chat.Id;
                var session = GetSession(chatId);

                // Обработка голосовых
                if (message.Voice != null)
                {
                    await bot.SendMessage(chatId,
                        "🎤 Голосовые пока не поддерживаются.\nИспользуйте текст: 'Завтра в 15:00 встреча'",
                        cancellationToken: ct);
                    return;
                }

                if (message.Text is not { } messageText) return;

                Console.WriteLine($"[{chatId}] {messageText}");

                // Обработка состояний
                await HandleStateAsync(bot, chatId, messageText, session, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                try
                {
                    await bot.SendMessage(update.Message!.Chat.Id,
                        "❌ Произошла ошибка. Попробуйте еще раз или вернитесь в меню.",
                        replyMarkup: KeyboardHelper.GetMainKeyboard(),
                        cancellationToken: ct);
                }
                catch { }
            }
        }

        private async Task HandleStateAsync(ITelegramBotClient bot, long chatId, string text, UserSession session, CancellationToken ct)
        {
            try
            {
                // Глобальная кнопка "В меню"
                if (text == "🏠 Главное меню" || text == "🏠 В меню")
                {
                    session.State = UserState.None;
                    session.CurrentPlan = null;
                    session.TempPlansList.Clear();
                    await HandleStart(bot, chatId, session, ct);
                    return;
                }

                // Обработка состояний
                switch (session.State)
                {
                    case UserState.WaitingForTimezone:
                        await HandleTimezoneSelection(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForDateTime:
                        await _creationHandler.HandleDateTimeSelection(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForTime:
                        await _creationHandler.HandleTimeSelection(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForDescription:
                        await _creationHandler.HandleDescription(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForRecurrence:
                        await _creationHandler.HandleRecurrence(bot, chatId, text, session, ct);
                        return;

                    // Редактирование
                    case UserState.WaitingForEditSelection:
                        await _editHandler.HandlePlanSelection(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForEditChoice:
                        await _editHandler.HandleFieldChoice(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForEditValue:
                        await _editHandler.HandleNewValue(bot, chatId, text, session, ct);
                        return;

                    // Удаление
                    case UserState.WaitingForDeleteConfirmation:
                        await _deleteHandler.HandleDeleteChoice(bot, chatId, text, session, ct);
                        return;

                    // Поиск
                    case UserState.WaitingForSearchQuery:
                        await _commandHandler.HandleSearch(bot, chatId, text, session, ct);
                        return;
                }

                // Обработка команд
                await HandleCommandAsync(bot, chatId, text, session, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка в HandleStateAsync: {ex.Message}");
                session.State = UserState.None;
                await bot.SendMessage(chatId,
                    "❌ Произошла ошибка. Возвращаю в главное меню.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(),
                    cancellationToken: ct);
            }
        }

        private async Task HandleCommandAsync(ITelegramBotClient bot, long chatId, string text, UserSession session, CancellationToken ct)
        {
            try
            {
                // СНАЧАЛА проверяем точные совпадения команд
                switch (text)
                {
                    case "/start":
                        await HandleStart(bot, chatId, session, ct);
                        return;

                    case "➕ Добавить":
                    case "/add":
                        await StartPlanCreation(bot, chatId, session, ct);
                        return;

                    case "📅 Мои планы":
                    case "/plans":
                        await _commandHandler.HandleViewPlans(bot, chatId, ct);
                        return;

                    case "✏️ Редактировать":
                    case "/edit":
                        await _editHandler.StartEdit(bot, chatId, session, ct);
                        return;

                    case "🗑 Удалить":
                    case "/delete":
                        await _deleteHandler.StartDelete(bot, chatId, session, ct);
                        return;

                    case "🔍 Поиск":
                    case "/search":
                        await _commandHandler.StartSearch(bot, chatId, session, ct);
                        return;

                    case "⚙️ Настройки":
                    case "/settings":
                        await HandleSettings(bot, chatId, ct);
                        return;

                    case "❓ Помощь":
                    case "/help":
                        await _commandHandler.HandleHelp(bot, chatId, ct);
                        return;

                    case "🌍 Часовой пояс":
                        await StartTimezoneSelection(bot, chatId, session, ct);
                        return;
                }

                // ПОТОМ проверяем выбор даты (ТОЛЬКО если это действительно выбор даты)
                if (text.StartsWith("📅 ") && (text.Contains("Сегодня") || text.Contains("Завтра") || text.Contains(".") && text.Length > 10))
                {
                    await _commandHandler.HandleDateSelection(bot, chatId, text, ct);
                    return;
                }

                // Неизвестная команда
                await bot.SendMessage(chatId, "❓ Неизвестная команда.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка в HandleCommandAsync: {ex.Message}");
                await bot.SendMessage(chatId,
                    "❌ Произошла ошибка. Попробуйте еще раз.",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(),
                    cancellationToken: ct);
            }
        }

        private async Task HandleStart(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            var timezone = _plannerService.GetUserTimezone(chatId);
            var isFirstRun = timezone == "Russian Standard Time" && !System.IO.File.Exists("timezones.json");

            if (isFirstRun)
            {
                session.State = UserState.WaitingForTimezone;
                await bot.SendMessage(chatId,
                    "👋 Добро пожаловать!\n\nВыберите часовой пояс:",
                    replyMarkup: KeyboardHelper.GetTimezoneKeyboard(), cancellationToken: ct);
            }
            else
            {
                var now = _plannerService.GetUserCurrentTime(chatId);
                await bot.SendMessage(chatId,
                    $"👋 Главное меню\n\n⏰ {now:HH:mm} | 📅 {now:dd.MM.yyyy}",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }
        }

        private async Task HandleTimezoneSelection(ITelegramBotClient bot, long chatId, string text, UserSession session, CancellationToken ct)
        {
            if (text.Contains("Москва"))
            {
                _plannerService.SetUserTimezone(chatId, "Russian Standard Time");
                await bot.SendMessage(chatId, "✅ Москва (UTC+3)",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }
            else if (text.Contains("Екатеринбург"))
            {
                _plannerService.SetUserTimezone(chatId, "Ekaterinburg Standard Time");
                await bot.SendMessage(chatId, "✅ Екатеринбург (UTC+5)",
                    replyMarkup: KeyboardHelper.GetMainKeyboard(), cancellationToken: ct);
            }

            session.State = UserState.None;
        }

        private async Task StartTimezoneSelection(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            session.State = UserState.WaitingForTimezone;
            await bot.SendMessage(chatId, "🌍 Выберите часовой пояс:",
                replyMarkup: KeyboardHelper.GetTimezoneKeyboard(), cancellationToken: ct);
        }

        private async Task StartPlanCreation(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            session.State = UserState.WaitingForDateTime;
            session.CurrentPlan = new PlanItem { ChatId = chatId };

            var now = _plannerService.GetUserCurrentTime(chatId);
            await bot.SendMessage(chatId, "📝 Новый план\n\n1/4: Дата",
                replyMarkup: KeyboardHelper.GetDateQuickSelectKeyboard(now), cancellationToken: ct);
        }

        private async Task HandleSettings(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            await bot.SendMessage(chatId, "⚙️ Настройки:",
                replyMarkup: KeyboardHelper.GetSettingsKeyboard(), cancellationToken: ct);
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiException => $"Telegram API: [{apiException.ErrorCode}] {apiException.Message}",
                _ => exception.Message
            };

            Console.WriteLine($"❌ {errorMessage}");
            return Task.CompletedTask;
        }

        #region Background Tasks

        private async Task DailyNotificationTask(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var chatIds = _plannerService.GetAllChatIds();

                    foreach (var chatId in chatIds)
                    {
                        var now = _plannerService.GetUserCurrentTime(chatId);

                        if (now.Hour == 8 && now.Minute == 0)
                        {
                            var plans = _plannerService.GetTodayPlans(chatId, now);
                            var message = $"🌅 Доброе утро!\n📅 {now:dd.MM.yyyy}\n\n";

                            if (plans.Count == 0)
                            {
                                message += "Сегодня планов нет. Хорошего дня! ☀️";
                            }
                            else
                            {
                                message += "Ваши планы:\n\n";
                                foreach (var plan in plans)
                                {
                                    message += PlannerService.FormatPlan(plan) + "\n";
                                }
                            }

                            await _botClient.SendMessage(chatId, message, cancellationToken: ct);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ DailyNotificationTask: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
            }
        }

        private async Task EventNotificationTask(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var pendingPlans = _plannerService.GetPendingNotifications(now);

                    foreach (var plan in pendingPlans)
                    {
                        try
                        {
                            var timeUntil = (plan.DateTime - now).TotalMinutes;
                            var message = $"⏰ Напоминание\n\n";
                            message += $"{PlannerService.FormatPlan(plan, detailed: true)}\n\n";

                            if (timeUntil > 1)
                                message += $"⏱ Начнётся через {(int)timeUntil} мин";
                            else
                                message += "⏱ Начинается сейчас!";

                            await _botClient.SendMessage(plan.ChatId, message, cancellationToken: ct);
                            _plannerService.MarkAsNotified(plan.Id);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Уведомление: {ex.Message}");
                        }
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ EventNotificationTask: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
            }
        }

        #endregion
    }
}