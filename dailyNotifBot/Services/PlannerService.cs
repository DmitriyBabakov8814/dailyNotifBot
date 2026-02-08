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
        private readonly string _templatesFile = "templates.json";
        private List<PlanItem> _plans;
        private Dictionary<long, string> _userTimezones;
        private List<PlanTemplate> _templates;

        public PlannerService()
        {
            LoadPlans();
            LoadTimezones();
            LoadTemplates();
        }

        #region Data Loading/Saving

        private void LoadPlans()
        {
            if (File.Exists(_dataFile))
            {
                var json = File.ReadAllText(_dataFile);
                _plans = JsonConvert.DeserializeObject<List<PlanItem>>(json) ?? new List<PlanItem>();
            }
            else
            {
                _plans = new List<PlanItem>();
            }
        }

        private void LoadTimezones()
        {
            if (File.Exists(_timezonesFile))
            {
                var json = File.ReadAllText(_timezonesFile);
                _userTimezones = JsonConvert.DeserializeObject<Dictionary<long, string>>(json) ?? new Dictionary<long, string>();
            }
            else
            {
                _userTimezones = new Dictionary<long, string>();
            }
        }

        private void LoadTemplates()
        {
            if (File.Exists(_templatesFile))
            {
                var json = File.ReadAllText(_templatesFile);
                _templates = JsonConvert.DeserializeObject<List<PlanTemplate>>(json) ?? new List<PlanTemplate>();
            }
            else
            {
                _templates = new List<PlanTemplate>();
            }
        }

        private void SavePlans()
        {
            var json = JsonConvert.SerializeObject(_plans, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_dataFile, json);
        }

        private void SaveTimezones()
        {
            var json = JsonConvert.SerializeObject(_userTimezones, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_timezonesFile, json);
        }

        private void SaveTemplates()
        {
            var json = JsonConvert.SerializeObject(_templates, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_templatesFile, json);
        }

        #endregion

        #region Timezone Management

        public void SetUserTimezone(long chatId, string timezoneId)
        {
            _userTimezones[chatId] = timezoneId;
            SaveTimezones();
        }

        public string GetUserTimezone(long chatId)
        {
            return _userTimezones.ContainsKey(chatId) ? _userTimezones[chatId] : "Russian Standard Time";
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
            _plans.Add(plan);

            if (plan.Recurrence != RecurrenceType.None)
            {
                CreateRecurringPlans(plan);
            }

            SavePlans();
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
                    Category = originalPlan.Category,
                    Priority = originalPlan.Priority,
                    NotificationMinutes = originalPlan.NotificationMinutes,
                    Location = originalPlan.Location,
                    Notes = originalPlan.Notes,
                    Recurrence = originalPlan.Recurrence,
                    ParentRecurrenceId = originalPlan.Id,
                    RecurrenceEndDate = originalPlan.RecurrenceEndDate
                };

                _plans.Add(recurringPlan);
            }
        }

        public bool UpdatePlan(PlanItem plan)
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

        public bool DeletePlan(long chatId, string planId)
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

        public PlanItem? GetPlanById(long chatId, string planId)
        {
            return _plans.FirstOrDefault(p => p.Id == planId && p.ChatId == chatId);
        }

        #endregion

        #region Plan Queries

        public List<PlanItem> GetPlansForDate(long chatId, DateTime date)
        {
            return _plans
                .Where(p => p.ChatId == chatId && p.DateTime.Date == date.Date)
                .OrderBy(p => p.DateTime.TimeOfDay)
                .ThenBy(p => p.Priority)
                .ToList();
        }

        public List<PlanItem> GetTodayPlans(long chatId, DateTime today)
        {
            return GetPlansForDate(chatId, today);
        }

        public List<PlanItem> GetAllUpcomingPlans(long chatId, DateTime fromDate)
        {
            return _plans
                .Where(p => p.ChatId == chatId && p.DateTime >= fromDate)
                .OrderBy(p => p.DateTime)
                .ToList();
        }

        public List<PlanItem> GetPlansByCategory(long chatId, PlanCategory category, DateTime fromDate)
        {
            return _plans
                .Where(p => p.ChatId == chatId && p.Category == category && p.DateTime >= fromDate)
                .OrderBy(p => p.DateTime)
                .ToList();
        }

        public List<PlanItem> SearchPlans(long chatId, string query)
        {
            query = query.ToLower();
            return _plans
                .Where(p => p.ChatId == chatId &&
                           (p.Description.ToLower().Contains(query) ||
                            p.Location.ToLower().Contains(query) ||
                            p.Notes.ToLower().Contains(query)))
                .OrderBy(p => p.DateTime)
                .ToList();
        }

        public List<DateTime> GetDatesWithPlans(long chatId, DateTime fromDate)
        {
            return _plans
                .Where(p => p.ChatId == chatId && p.DateTime >= fromDate)
                .Select(p => p.DateTime.Date)
                .Distinct()
                .OrderBy(d => d)
                .Take(10)
                .ToList();
        }

        public int DeletePlansByDate(long chatId, DateTime date)
        {
            var plansToDelete = _plans.Where(p => p.ChatId == chatId && p.DateTime.Date == date.Date).ToList();
            foreach (var plan in plansToDelete)
            {
                _plans.Remove(plan);
            }
            SavePlans();
            return plansToDelete.Count;
        }

        public int DeleteRecurringPlans(long chatId, string parentId)
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

        public int DeleteMultiplePlans(long chatId, List<string> planIds)
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

        #endregion

        #region Notifications

        public List<PlanItem> GetPendingNotifications(DateTime currentTime)
        {
            var notifications = new List<PlanItem>();

            foreach (var plan in _plans.Where(p => !p.IsNotified))
            {
                var timeDiff = (plan.DateTime - currentTime).TotalMinutes;
                if (timeDiff <= plan.NotificationMinutes && timeDiff >= 0)
                {
                    notifications.Add(plan);
                }
            }

            return notifications;
        }

        public void MarkAsNotified(string planId)
        {
            var plan = _plans.FirstOrDefault(p => p.Id == planId);
            if (plan != null)
            {
                plan.IsNotified = true;
                SavePlans();
            }
        }

        public List<long> GetAllChatIds()
        {
            return _plans.Select(p => p.ChatId).Distinct().ToList();
        }

        #endregion

        #region Templates

        public void AddTemplate(PlanTemplate template)
        {
            _templates.Add(template);
            SaveTemplates();
        }

        public List<PlanTemplate> GetUserTemplates(long chatId)
        {
            return _templates.Where(t => t.ChatId == chatId).ToList();
        }

        public bool DeleteTemplate(long chatId, string templateId)
        {
            var template = _templates.FirstOrDefault(t => t.Id == templateId && t.ChatId == chatId);
            if (template != null)
            {
                _templates.Remove(template);
                SaveTemplates();
                return true;
            }
            return false;
        }

        public PlanTemplate? GetTemplateById(long chatId, string templateId)
        {
            return _templates.FirstOrDefault(t => t.Id == templateId && t.ChatId == chatId);
        }

        #endregion

        #region Helper Methods

        public static string GetCategoryEmoji(PlanCategory category)
        {
            return category switch
            {
                PlanCategory.Work => "💼",
                PlanCategory.Family => "👨‍👩‍👧",
                PlanCategory.Sport => "🏃",
                PlanCategory.Health => "🏥",
                PlanCategory.Shopping => "🛒",
                PlanCategory.Study => "📚",
                _ => "📌"
            };
        }

        public static string GetCategoryName(PlanCategory category)
        {
            return category switch
            {
                PlanCategory.Work => "Работа",
                PlanCategory.Family => "Семья",
                PlanCategory.Sport => "Спорт",
                PlanCategory.Health => "Здоровье",
                PlanCategory.Shopping => "Покупки",
                PlanCategory.Study => "Учеба",
                _ => "Другое"
            };
        }

        public static string GetPriorityEmoji(PlanPriority priority)
        {
            return priority switch
            {
                PlanPriority.High => "🔴",
                PlanPriority.Medium => "🟡",
                PlanPriority.Low => "🟢",
                _ => "⚪"
            };
        }

        public static string GetPriorityName(PlanPriority priority)
        {
            return priority switch
            {
                PlanPriority.High => "Высокая",
                PlanPriority.Medium => "Средняя",
                PlanPriority.Low => "Низкая",
                _ => "Не указан"
            };
        }

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
            var priority = GetPriorityEmoji(plan.Priority);
            var category = GetCategoryEmoji(plan.Category);

            if (!detailed)
            {
                return $"{priority} {category} {plan.DateTime:HH:mm} - {plan.Description}";
            }

            var result = $"{priority} {category} {plan.Description}\n";
            result += $"🗓 Дата: {plan.DateTime:dd.MM.yyyy HH:mm}\n";

            if (!string.IsNullOrEmpty(plan.Location))
                result += $"📍 Место: {plan.Location}\n";

            if (!string.IsNullOrEmpty(plan.Notes))
                result += $"📝 Заметки: {plan.Notes}\n";

            if (plan.Recurrence != RecurrenceType.None)
                result += $"🔄 Повтор: {GetRecurrenceName(plan.Recurrence)}\n";

            result += $"⏰ Напомнить за {plan.NotificationMinutes} мин";

            return result;
        }

        #endregion
    }
}