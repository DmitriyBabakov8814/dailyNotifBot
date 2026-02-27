using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
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
        private readonly ConcurrentDictionary<long, UserSession> _sessions;
        private readonly ConcurrentDictionary<long, DateTime> _lastCommandTime;

        private readonly SimplePlanCreationHandler _creationHandler;
        private readonly PlanEditHandler _editHandler;
        private readonly CommandHandler _commandHandler;
        private readonly DeleteHandler _deleteHandler;

        private CancellationTokenSource? _cts;
        private const int MAX_DESCRIPTION_LENGTH = 500;
        private const int MAX_PLANS_PER_USER = 200;
        private readonly TimeSpan _commandCooldown = TimeSpan.FromMilliseconds(500);

        public TelegramBotService(string botToken)
        {
            _botClient = new TelegramBotClient(botToken);
            _plannerService = new PlannerService();
            _sessions = new ConcurrentDictionary<long, UserSession>();
            _lastCommandTime = new ConcurrentDictionary<long, DateTime>();

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

            _cts = new CancellationTokenSource();

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: _cts.Token
            );

            var me = await _botClient.GetMe(_cts.Token);
            LogInfo($"Бот @{me.Username} запущен!");

            // Фоновые задачи
            _ = Task.Run(() => DailyNotificationTask(_cts.Token), _cts.Token);
            _ = Task.Run(() => EventNotificationTask(_cts.Token), _cts.Token);

            // Graceful shutdown handler
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                LogInfo("Получен сигнал остановки...");
                _cts?.Cancel();
            };

            LogInfo("Бот готов к работе. Нажмите Ctrl+C для остановки.");

            // Ждём отмены
            try
            {
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                LogInfo("Остановка бота...");
            }

            // Даём время завершить текущие операции
            await Task.Delay(TimeSpan.FromSeconds(2));
            LogInfo("Бот остановлен.");
        }

        private UserSession GetSession(long chatId)
        {
            return _sessions.GetOrAdd(chatId, _ => new UserSession());
        }

        private bool CheckRateLimit(long chatId)
        {
            var now = DateTime.UtcNow;
            var lastTime = _lastCommandTime.GetOrAdd(chatId, now);

            if (now - lastTime < _commandCooldown)
            {
                return false; // Слишком быстро
            }

            _lastCommandTime[chatId] = now;
            return true;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            try
            {
                if (update.Message is not { } message) return;

                var chatId = message.Chat.Id;

                // Rate limiting
                if (!CheckRateLimit(chatId))
                {
                    return; // Молча игнорируем спам
                }

                var session = GetSession(chatId);

                // Голосовые сообщения
                if (message.Voice != null)
                {
                    await SafeSendMessage(bot, chatId,
                        "🎤 Голосовые пока не поддерживаются.\nИспользуйте текст: 'Завтра в 15:00 встреча'",
                        KeyboardHelper.GetMainKeyboard(), ct);
                    return;
                }

                if (message.Text is not { } messageText) return;

                // Логирование
                LogInfo($"[{chatId}] [{message.From?.Username ?? "unknown"}] {messageText}");

                await HandleStateAsync(bot, chatId, messageText, session, ct);
            }
            catch (Exception ex)
            {
                LogError($"HandleUpdateAsync: {ex.Message}", ex);

                try
                {
                    if (update.Message?.Chat.Id is { } chatId)
                    {
                        await SafeSendMessage(_botClient, chatId,
                            "❌ Произошла ошибка. Попробуйте еще раз.",
                            KeyboardHelper.GetMainKeyboard(), default);
                    }
                }
                catch { /* Игнорируем ошибки при отправке сообщения об ошибке */ }
            }
        }

        private async Task HandleStateAsync(ITelegramBotClient bot, long chatId, string text, UserSession session, CancellationToken ct)
        {
            try
            {
                // Глобальная команда возврата в меню
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
                        // Валидация длины
                        if (text.Length > MAX_DESCRIPTION_LENGTH)
                        {
                            await SafeSendMessage(bot, chatId,
                                $"❌ Описание слишком длинное!\n\nМаксимум: {MAX_DESCRIPTION_LENGTH} символов\nСейчас: {text.Length}",
                                KeyboardHelper.GetCancelKeyboard(), ct);
                            return;
                        }
                        await _creationHandler.HandleDescription(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForNotificationTime:
                        await _creationHandler.HandleNotificationTime(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForRecurrence:
                        // Проверка лимита перед созданием
                        var userPlansCount = _plannerService.GetAllUpcomingPlans(chatId, DateTime.Now).Count;
                        if (userPlansCount >= MAX_PLANS_PER_USER)
                        {
                            await SafeSendMessage(bot, chatId,
                                $"❌ Достигнут лимит: {MAX_PLANS_PER_USER} планов!\n\nУдалите старые планы.",
                                KeyboardHelper.GetMainKeyboard(), ct);
                            session.State = UserState.None;
                            session.CurrentPlan = null;
                            return;
                        }
                        await _creationHandler.HandleRecurrence(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForEditSelection:
                        await _editHandler.HandlePlanSelection(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForEditChoice:
                        await _editHandler.HandleFieldChoice(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForEditValue:
                        await _editHandler.HandleNewValue(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForDeleteConfirmation:
                        await _deleteHandler.HandleDeleteChoice(bot, chatId, text, session, ct);
                        return;

                    case UserState.WaitingForSearchQuery:
                        await _commandHandler.HandleSearch(bot, chatId, text, session, ct);
                        return;
                }

                // Обработка команд
                await HandleCommandAsync(bot, chatId, text, session, ct);
            }
            catch (Exception ex)
            {
                LogError($"HandleStateAsync [{chatId}]: {ex.Message}", ex);

                // Сброс состояния
                session.State = UserState.None;
                session.CurrentPlan = null;

                await SafeSendMessage(bot, chatId,
                    "❌ Произошла ошибка. Возврат в меню.",
                    KeyboardHelper.GetMainKeyboard(), ct);
            }
        }

        private async Task HandleCommandAsync(ITelegramBotClient bot, long chatId, string text, UserSession session, CancellationToken ct)
        {
            try
            {
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

                // Проверка на выбор даты
                if (text.StartsWith("📅 ") &&
                    (text.Contains("Сегодня") || text.Contains("Завтра") ||
                     (text.Contains(".") && text.Length > 10)))
                {
                    await _commandHandler.HandleDateSelection(bot, chatId, text, ct);
                    return;
                }

                // Неизвестная команда
                await SafeSendMessage(bot, chatId,
                    "❓ Неизвестная команда. Используйте меню.",
                    KeyboardHelper.GetMainKeyboard(), ct);
            }
            catch (Exception ex)
            {
                LogError($"HandleCommandAsync [{chatId}]: {ex.Message}", ex);

                await SafeSendMessage(bot, chatId,
                    "❌ Ошибка выполнения команды.",
                    KeyboardHelper.GetMainKeyboard(), ct);
            }
        }

        private async Task HandleStart(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            try
            {
                var timezone = _plannerService.GetUserTimezone(chatId);
                var isFirstRun = timezone == "Russian Standard Time" && !System.IO.File.Exists("timezones.json");

                if (isFirstRun)
                {
                    session.State = UserState.WaitingForTimezone;
                    await SafeSendMessage(bot, chatId,
                        "👋 Добро пожаловать в Бот-Ежедневник!\n\nВыберите часовой пояс:",
                        KeyboardHelper.GetTimezoneKeyboard(), ct);
                }
                else
                {
                    var now = _plannerService.GetUserCurrentTime(chatId);
                    await SafeSendMessage(bot, chatId,
                        $"Главное меню\n\n⏰ {now:HH:mm} | 📅 {now:dd.MM.yyyy}",
                        KeyboardHelper.GetMainKeyboard(), ct);
                }
            }
            catch (Exception ex)
            {
                LogError($"HandleStart [{chatId}]: {ex.Message}", ex);
            }
        }

        private async Task HandleTimezoneSelection(ITelegramBotClient bot, long chatId, string text, UserSession session, CancellationToken ct)
        {
            try
            {
                if (text.Contains("Москва"))
                {
                    _plannerService.SetUserTimezone(chatId, "Russian Standard Time");
                    await SafeSendMessage(bot, chatId, "✅ Установлено: Москва (UTC+3)",
                        KeyboardHelper.GetMainKeyboard(), ct);
                }
                else if (text.Contains("Екатеринбург"))
                {
                    _plannerService.SetUserTimezone(chatId, "Ekaterinburg Standard Time");
                    await SafeSendMessage(bot, chatId, "✅ Установлено: Екатеринбург (UTC+5)",
                        KeyboardHelper.GetMainKeyboard(), ct);
                }

                session.State = UserState.None;
            }
            catch (Exception ex)
            {
                LogError($"HandleTimezoneSelection [{chatId}]: {ex.Message}", ex);
            }
        }

        private async Task StartTimezoneSelection(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            session.State = UserState.WaitingForTimezone;
            await SafeSendMessage(bot, chatId, "🌍 Выберите часовой пояс:",
                KeyboardHelper.GetTimezoneKeyboard(), ct);
        }

        private async Task StartPlanCreation(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
        {
            try
            {
                // Проверка лимита планов
                var userPlansCount = _plannerService.GetAllUpcomingPlans(chatId, DateTime.Now).Count;
                if (userPlansCount >= MAX_PLANS_PER_USER)
                {
                    await SafeSendMessage(bot, chatId,
                        $"❌ Достигнут лимит: {MAX_PLANS_PER_USER} планов!\n\nУдалите старые планы перед созданием новых.",
                        KeyboardHelper.GetMainKeyboard(), ct);
                    return;
                }

                session.State = UserState.WaitingForDateTime;
                session.CurrentPlan = new PlanItem { ChatId = chatId };

                var now = _plannerService.GetUserCurrentTime(chatId);
                await SafeSendMessage(bot, chatId, "📝 Новый план\n\n1/5: Дата",
                    KeyboardHelper.GetDateQuickSelectKeyboard(now), ct);
            }
            catch (Exception ex)
            {
                LogError($"StartPlanCreation [{chatId}]: {ex.Message}", ex);
            }
        }

        private async Task HandleSettings(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            await SafeSendMessage(bot, chatId, "⚙️ Настройки:",
                KeyboardHelper.GetSettingsKeyboard(), ct);
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiException => $"Telegram API [{apiException.ErrorCode}]: {apiException.Message}",
                _ => $"{exception.GetType().Name}: {exception.Message}"
            };

            LogError(errorMessage, exception);
            return Task.CompletedTask;
        }

        #region Background Tasks

        private async Task DailyNotificationTask(CancellationToken ct)
        {
            LogInfo("DailyNotificationTask запущена");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);

                    var chatIds = _plannerService.GetAllChatIds();

                    foreach (var chatId in chatIds)
                    {
                        try
                        {
                            var now = _plannerService.GetUserCurrentTime(chatId);

                            if (now.Hour == 8 && now.Minute == 0)
                            {
                                var plans = _plannerService.GetTodayPlans(chatId, now);
                                var message = $"Доброе утро!\n📅 {now:dd.MM.yyyy}\n\n";

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

                                await SafeSendMessage(_botClient, chatId, message, null, ct);
                                LogInfo($"Отправлено утреннее уведомление [{chatId}]");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"DailyNotification [{chatId}]: {ex.Message}", ex);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"DailyNotificationTask: {ex.Message}", ex);
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
            }

            LogInfo("DailyNotificationTask остановлена");
        }

        private async Task EventNotificationTask(CancellationToken ct)
        {
            LogInfo("EventNotificationTask запущена");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);

                    var now = DateTime.UtcNow;
                    var pendingPlans = _plannerService.GetPendingNotifications(now);

                    foreach (var plan in pendingPlans)
                    {
                        try
                        {
                            var userTime = _plannerService.GetUserCurrentTime(plan.ChatId);
                            var timeUntil = (plan.DateTime - userTime).TotalMinutes;
                            var notifText = plan.NotificationMinutes == 60 ? "1 час" : $"{plan.NotificationMinutes} мин";

                            var message = $"⏰ Напоминание\n\n{PlannerService.FormatPlan(plan, detailed: true)}\n\n";

                            if (timeUntil > 1)
                                message += $"Начнётся через {(int)timeUntil} мин";
                            else
                                message += "Начинается сейчас!";

                            await SafeSendMessage(_botClient, plan.ChatId, message, null, ct);
                            _plannerService.MarkAsNotified(plan.Id);

                            LogInfo($"Отправлено напоминание [{plan.ChatId}]: {plan.Description}");
                        }
                        catch (Exception ex)
                        {
                            LogError($"EventNotification [{plan.ChatId}]: {ex.Message}", ex);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"EventNotificationTask: {ex.Message}", ex);
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
            }

            LogInfo("EventNotificationTask остановлена");
        }

        #endregion

        #region Helper Methods

        private async Task SafeSendMessage(ITelegramBotClient bot, long chatId, string text,
            ReplyKeyboardMarkup? replyMarkup, CancellationToken ct)
        {
            try
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: text,
                    replyMarkup: replyMarkup,
                    cancellationToken: ct
                );
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                LogWarning($"Бот заблокирован пользователем [{chatId}]");
            }
            catch (Exception ex)
            {
                LogError($"SafeSendMessage [{chatId}]: {ex.Message}", ex);
            }
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"[{timestamp}] INFO: {message}");
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"[{timestamp}] WARN: {message}");
        }

        private void LogError(string message, Exception? ex = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"[{timestamp}] ERROR: {message}");
            if (ex != null)
            {
                Console.WriteLine($"[{timestamp}] STACK: {ex.StackTrace}");
            }
        }

        #endregion
    }
}