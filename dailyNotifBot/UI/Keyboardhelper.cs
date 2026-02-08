using System;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPlannerBot.Models;

namespace TelegramPlannerBot.UI
{
    public static class KeyboardHelper
    {
        public static ReplyKeyboardMarkup GetMainKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "➕ Добавить", "📅 Мои планы" },
                new KeyboardButton[] { "✏️ Редактировать", "🗑 Удалить" },
                new KeyboardButton[] { "🔍 Поиск", "⚙️ Настройки" }
            })
            {
                ResizeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetSettingsKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🌍 Часовой пояс" },
                new KeyboardButton[] { "❓ Помощь", "🏠 Главное меню" }
            })
            {
                ResizeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetTimezoneKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🇷🇺 Москва (UTC+3)" },
                new KeyboardButton[] { "🇷🇺 Екатеринбург (UTC+5)" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetDateQuickSelectKeyboard(DateTime currentTime)
        {
            var today = currentTime.Date;
            var tomorrow = today.AddDays(1);
            var dayAfterTomorrow = today.AddDays(2);

            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[]
                {
                    $"📅 Сегодня ({today:dd.MM})",
                    $"📅 Завтра ({tomorrow:dd.MM})"
                },
                new KeyboardButton[]
                {
                    $"📅 Послезавтра ({dayAfterTomorrow:dd.MM})"
                },
                new KeyboardButton[] { "✏️ Ввести дату", "❌ Отмена" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetTimeQuickSelectKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🕐 09:00", "🕐 12:00", "🕐 15:00" },
                new KeyboardButton[] { "🕐 18:00", "🕐 20:00", "🕐 21:00" },
                new KeyboardButton[] { "✏️ Ввести время", "❌ Отмена" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetCategoryKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "💼 Работа", "👨‍👩‍👧 Семья", "🏃 Спорт" },
                new KeyboardButton[] { "🏥 Здоровье", "🛒 Покупки", "📚 Учеба" },
                new KeyboardButton[] { "📌 Другое", "⏭ Пропустить" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetPriorityKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🔴 Высокая", "🟡 Средняя", "🟢 Низкая" },
                new KeyboardButton[] { "⏭ Пропустить" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetNotificationTimeKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "⏰ 5 минут", "⏰ 10 минут", "⏰ 15 минут" },
                new KeyboardButton[] { "⏰ 30 минут", "⏰ 1 час" },
                new KeyboardButton[] { "⏭ Пропустить" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetRecurrenceKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🔄 Каждый день", "🔄 Каждую неделю" },
                new KeyboardButton[] { "🔄 Каждый месяц", "⏭ Не повторять" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetEditFieldKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📅 Дату", "🕐 Время" },
                new KeyboardButton[] { "📝 Описание", "🏷 Категорию" },
                new KeyboardButton[] { "⚡ Приоритет", "⏰ Уведомление" },
                new KeyboardButton[] { "📍 Местоположение", "📋 Заметки" },
                new KeyboardButton[] { "❌ Отмена" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetSkipOrCancelKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "⏭ Пропустить", "❌ Отмена" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetCancelKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🏠 В меню", "❌ Отмена" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetDatesKeyboard(List<DateTime> dates, DateTime today)
        {
            var buttons = new List<KeyboardButton[]>();

            // Первый ряд - сегодня/завтра если есть планы
            var firstRow = new List<KeyboardButton>();
            if (dates.Any(d => d.Date == today.Date))
                firstRow.Add(new KeyboardButton($"📅 Сегодня ({today:dd.MM})"));
            if (dates.Any(d => d.Date == today.AddDays(1).Date))
                firstRow.Add(new KeyboardButton($"📅 Завтра ({today.AddDays(1):dd.MM})"));

            if (firstRow.Count > 0)
                buttons.Add(firstRow.ToArray());

            // Остальные даты (кроме сегодня и завтра)
            var otherDates = dates.Where(d => d.Date != today.Date && d.Date != today.AddDays(1).Date).ToList();
            for (int i = 0; i < otherDates.Count; i += 2)
            {
                var row = new List<KeyboardButton>();
                row.Add(new KeyboardButton($"📅 {otherDates[i]:dd.MM.yyyy}"));
                if (i + 1 < otherDates.Count)
                    row.Add(new KeyboardButton($"📅 {otherDates[i + 1]:dd.MM.yyyy}"));
                buttons.Add(row.ToArray());
            }

            // Кнопка назад
            buttons.Add(new KeyboardButton[] { "🏠 В меню" });

            return new ReplyKeyboardMarkup(buttons)
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static ReplyKeyboardMarkup GetDeleteOptionsKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🗑 По номерам (1 2 3)" },
                new KeyboardButton[] { "🗑 Все на эту дату" },
                new KeyboardButton[] { "🗑 Все повторяющиеся" },
                new KeyboardButton[] { "🏠 В меню" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
        }

        public static PlanCategory ParseCategory(string text)
        {
            if (text.Contains("Работа")) return PlanCategory.Work;
            if (text.Contains("Семья")) return PlanCategory.Family;
            if (text.Contains("Спорт")) return PlanCategory.Sport;
            if (text.Contains("Здоровье")) return PlanCategory.Health;
            if (text.Contains("Покупки")) return PlanCategory.Shopping;
            if (text.Contains("Учеба")) return PlanCategory.Study;
            return PlanCategory.Other;
        }

        public static PlanPriority ParsePriority(string text)
        {
            if (text.Contains("Высокая")) return PlanPriority.High;
            if (text.Contains("Низкая")) return PlanPriority.Low;
            return PlanPriority.Medium;
        }

        public static int ParseNotificationMinutes(string text)
        {
            if (text.Contains("5")) return 5;
            if (text.Contains("15")) return 15;
            if (text.Contains("30")) return 30;
            if (text.Contains("час")) return 60;
            return 10; // По умолчанию
        }

        public static RecurrenceType ParseRecurrence(string text)
        {
            if (text.Contains("день")) return RecurrenceType.Daily;
            if (text.Contains("неделю")) return RecurrenceType.Weekly;
            if (text.Contains("месяц")) return RecurrenceType.Monthly;
            return RecurrenceType.None;
        }
    }
}