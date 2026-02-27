using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TelegramPlannerBot.Models;

namespace TelegramPlannerBot.Services
{
    public class PlannerService
    {
        private readonly string _dataFile = "plans.json";
        private readonly string _timezonesFile = "timezones.json";
        private readonly object _plansLock = new object();
        private readonly object _timezonesLock = new object();

        private List<PlanItem> _plans;
        private Dictionary<long, string> _userTimezones;

        public PlannerService()
        {
            LoadPlans();
            LoadTimezones();
        }

        #region Data Loading/Saving

        private void LoadPlans()
        {
            lock (_plansLock)
            {
                if (File.Exists(_dataFile))
                {
                    try
                    {
                        var json = File.ReadAllText(_dataFile);
                        _plans = JsonConvert.DeserializeObject<List<PlanItem>>(json) ?? new List<PlanItem>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Не удалось загрузить plans.json: {ex.Message}");
                        _plans = new List<PlanItem>();

                        // Backup поврежденного файла
                        if (File.Exists(_dataFile))
                        {
                            var backupPath = $"plans_corrupted_{DateTime.Now:yyyyMMddHHmmss}.json";
                            File.Copy(_dataFile, backupPath);
                            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: Создан бэкап: {backupPath}");
                        }
                    }
                }
                else
                {
                    _plans = new List<PlanItem>();
                }
            }
        }

        private void LoadTimezones()
        {
            lock (_timezonesLock)
            {
                if (File.Exists(_timezonesFile))
                {
                    try
                    {
                        var json = File.ReadAllText(_timezonesFile);
                        _userTimezones = JsonConvert.DeserializeObject<Dictionary<long, string>>(json) ?? new Dictionary<long, string>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Не удалось загрузить timezones.json: {ex.Message}");
                        _userTimezones = new Dictionary<long, string>();
                    }
                }
                else
                {
                    _userTimezones = new Dictionary<long, string>();
                }
            }
        }

        private void SavePlans()
        {
            lock (_plansLock)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_plans, Formatting.Indented);
                    File.WriteAllText(_dataFile, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Не удалось сохранить plans.json: {ex.Message}");
                }
            }
        }

        private void SaveTimezones()
        {
            lock (_timezonesLock)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_userTimezones, Formatting.Indented);
                    File.WriteAllText(_timezonesFile, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: Не удалось сохранить timezones.json: {ex.Message}");
                }
            }
        }

        #endregion

        #region Timezone Management

        public void SetUserTimezone(long chatId, string timezoneId)
        {
            lock (_timezonesLock)
            {
                _userTimezones[chatId] = timezoneId;
                SaveTimezones();
            }
        }

        public string GetUserTimezone(long chatId)
        {
            lock (_timezonesLock)
            {
                return _userTimezones.ContainsKey(chatId) ? _userTimezones[chatId] : "Russian Standard Time";
            }
        }

        public DateTime GetUserCurrentTime(long chatId)
        {
            var timezoneId = GetUserTimezone(chatId);
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            }
            catch
            {
                return DateTime.Now;
            }
        }

        #endregion

        #region Plan Management

        public void AddPlan(PlanItem plan)
        {
            lock (_plansLock)
            {
                _plans.Add(plan);

                if (plan.Recurrence != RecurrenceType.None)
                {
                    CreateRecurringPlans(plan);
                }

                SavePlans();
            }
        }

        private void CreateRecurringPlans(PlanItem originalPlan)
        {
            var endDate = originalPlan.RecurrenceEndDate ?? originalPlan.DateTime.AddMonths(3);
            var currentDate = originalPlan.DateTime;

            while (currentDate < endDate)
            {
                currentDate = originalPlan.Recurrence switch
                {
                    RecurrenceType.Daily => currentDate.AddDays(1),
                    RecurrenceType.Weekly => currentDate.AddDays(7),
                    RecurrenceType.Monthly => currentDate.AddMonths(1),
                    _ => currentDate
                };

                if (currentDate >= endDate) break;

                var recurringPlan = new PlanItem
                {
                    ChatId = originalPlan.ChatId,
                    DateTime = currentDate,
                    Description = originalPlan.Description,
                    NotificationMinutes = originalPlan.NotificationMinutes,
                    Recurrence = originalPlan.Recurrence,
                    ParentRecurrenceId = originalPlan.Id,
                    RecurrenceEndDate = originalPlan.RecurrenceEndDate
                };

                _plans.Add(recurringPlan);
            }
        }

        public bool UpdatePlan(PlanItem plan)
        {
            lock (_plansLock)
            {
                var existingPlan = _plans.FirstOrDefault(p => p.Id == plan.Id && p.ChatId == plan.ChatId);
                if (existingPlan != null)
                {
                    var index = _plans.IndexOf(existingPlan);
                    _plans[index] = plan;
                    SavePlans();
                    return true;
                }
                return false;
            }
        }

        public bool DeletePlan(long chatId, string planId)
        {
            lock (_plansLock)
            {
                var plan = _plans.FirstOrDefault(p => p.Id == planId && p.ChatId == chatId);
                if (plan != null)
                {
                    _plans.Remove(plan);
                    SavePlans();
                    return true;
                }
                return false;
            }
        }

        public PlanItem? GetPlanById(long chatId, string planId)
        {
            lock (_plansLock)
            {
                return _plans.FirstOrDefault(p => p.Id == planId && p.ChatId == chatId);
            }
        }

        #endregion

        #region Plan Queries

        public List<PlanItem> GetPlansForDate(long chatId, DateTime date)
        {
            lock (_plansLock)
            {
                return _plans
                    .Where(p => p.ChatId == chatId && p.DateTime.Date == date.Date)
                    .OrderBy(p => p.DateTime.TimeOfDay)
                    .ToList();
            }
        }

        public List<PlanItem> GetTodayPlans(long chatId, DateTime today)
        {
            return GetPlansForDate(chatId, today);
        }

        public List<PlanItem> GetAllUpcomingPlans(long chatId, DateTime fromDate)
        {
            lock (_plansLock)
            {
                return _plans
                    .Where(p => p.ChatId == chatId && p.DateTime >= fromDate)
                    .OrderBy(p => p.DateTime)
                    .ToList();
            }
        }

        public List<PlanItem> SearchPlans(long chatId, string query)
        {
            lock (_plansLock)
            {
                query = query.ToLower();
                return _plans
                    .Where(p => p.ChatId == chatId && p.Description.ToLower().Contains(query))
                    .OrderBy(p => p.DateTime)
                    .ToList();
            }
        }

        public List<DateTime> GetDatesWithPlans(long chatId, DateTime fromDate)
        {
            lock (_plansLock)
            {
                return _plans
                    .Where(p => p.ChatId == chatId && p.DateTime >= fromDate)
                    .Select(p => p.DateTime.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .Take(10)
                    .ToList();
            }
        }

        public int DeletePlansByDate(long chatId, DateTime date)
        {
            lock (_plansLock)
            {
                var plansToDelete = _plans.Where(p => p.ChatId == chatId && p.DateTime.Date == date.Date).ToList();
                foreach (var plan in plansToDelete)
                {
                    _plans.Remove(plan);
                }
                SavePlans();
                return plansToDelete.Count;
            }
        }

        public int DeleteRecurringPlans(long chatId, string parentId)
        {
            lock (_plansLock)
            {
                var plansToDelete = _plans.Where(p => p.ChatId == chatId &&
                    (p.Id == parentId || p.ParentRecurrenceId == parentId)).ToList();
                foreach (var plan in plansToDelete)
                {
                    _plans.Remove(plan);
                }
                SavePlans();
                return plansToDelete.Count;
            }
        }

        public int DeleteMultiplePlans(long chatId, List<string> planIds)
        {
            lock (_plansLock)
            {
                var deleted = 0;
                foreach (var id in planIds)
                {
                    var plan = _plans.FirstOrDefault(p => p.Id == id && p.ChatId == chatId);
                    if (plan != null)
                    {
                        _plans.Remove(plan);
                        deleted++;
                    }
                }
                SavePlans();
                return deleted;
            }
        }

        #endregion

        #region Notifications

        // ✅ ИСПРАВЛЕННЫЙ МЕТОД - принимает chatId для конвертации в локальное время
        public List<PlanItem> GetPendingNotifications()
        {
            lock (_plansLock)
            {
                var notifications = new List<PlanItem>();

                foreach (var plan in _plans.Where(p => !p.IsNotified))
                {
                    // Получаем текущее время пользователя в его часовом поясе
                    var userNow = GetUserCurrentTime(plan.ChatId);

                    // Сравниваем локальное время плана с локальным временем пользователя
                    var timeDiff = (plan.DateTime - userNow).TotalMinutes;

                    // Если осталось от 0 до NotificationMinutes минут - отправляем
                    if (timeDiff <= plan.NotificationMinutes && timeDiff >= -1)
                    {
                        notifications.Add(plan);

                        // Логирование для отладки
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DEBUG: План {plan.Description}");
                        Console.WriteLine($"  Время плана: {plan.DateTime:HH:mm:ss}");
                        Console.WriteLine($"  Текущее время пользователя: {userNow:HH:mm:ss}");
                        Console.WriteLine($"  Разница: {timeDiff:F1} мин");
                        Console.WriteLine($"  Напомнить за: {plan.NotificationMinutes} мин");
                    }
                }

                return notifications;
            }
        }

        public void MarkAsNotified(string planId)
        {
            lock (_plansLock)
            {
                var plan = _plans.FirstOrDefault(p => p.Id == planId);
                if (plan != null)
                {
                    plan.IsNotified = true;
                    SavePlans();
                }
            }
        }

        public List<long> GetAllChatIds()
        {
            lock (_plansLock)
            {
                return _plans.Select(p => p.ChatId).Distinct().ToList();
            }
        }

        #endregion

        #region Helper Methods

        public static string GetRecurrenceName(RecurrenceType recurrence)
        {
            return recurrence switch
            {
                RecurrenceType.Daily => "Каждый день",
                RecurrenceType.Weekly => "Каждую неделю",
                RecurrenceType.Monthly => "Каждый месяц",
                _ => "Не повторяется"
            };
        }

        public static string FormatPlan(PlanItem plan, bool detailed = false)
        {
            if (!detailed)
            {
                // Краткий формат: время + описание (БЕЗ повторяющегося эмодзи)
                return $"{plan.DateTime:HH:mm} - {plan.Description}";
            }

            // Детальный формат
            var notifText = plan.NotificationMinutes == 60 ? "1 час" : $"{plan.NotificationMinutes} мин";

            var result = $"📝 {plan.Description}\n";
            result += $"📅 {plan.DateTime:dd.MM.yyyy HH:mm}\n";

            if (plan.Recurrence != RecurrenceType.None)
                result += $"🔄 {GetRecurrenceName(plan.Recurrence)}\n";

            result += $"⏰ Напомнить за {notifText}";

            return result;
        }

        #endregion
    }
}