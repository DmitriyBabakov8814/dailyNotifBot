using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TelegramPlannerBot.Services
{
    public class VoiceCommandParser
    {
        public static bool TryParseVoiceCommand(string text, DateTime currentDate, out DateTime dateTime, out string description)
        {
            dateTime = DateTime.MinValue;
            description = string.Empty;

            try
            {
                text = text.ToLower().Trim();

                // Паттерны для распознавания времени
                var timePattern = @"в\s+(\d{1,2}):(\d{2})|в\s+(\d{1,2})\s+час";
                var timeMatch = Regex.Match(text, timePattern);

                TimeSpan time = TimeSpan.Zero;
                if (timeMatch.Success)
                {
                    if (!string.IsNullOrEmpty(timeMatch.Groups[1].Value))
                    {
                        // Формат ЧЧ:ММ
                        int hours = int.Parse(timeMatch.Groups[1].Value);
                        int minutes = int.Parse(timeMatch.Groups[2].Value);
                        time = new TimeSpan(hours, minutes, 0);
                    }
                    else if (!string.IsNullOrEmpty(timeMatch.Groups[3].Value))
                    {
                        // Формат "в 15 час"
                        int hours = int.Parse(timeMatch.Groups[3].Value);
                        time = new TimeSpan(hours, 0, 0);
                    }
                }

                // Определение даты
                DateTime targetDate = currentDate;

                if (text.Contains("сегодня"))
                {
                    targetDate = currentDate.Date;
                }
                else if (text.Contains("завтра"))
                {
                    targetDate = currentDate.Date.AddDays(1);
                }
                else if (text.Contains("послезавтра"))
                {
                    targetDate = currentDate.Date.AddDays(2);
                }
                else
                {
                    // Попытка найти дату в формате ДД.ММ
                    var datePattern = @"(\d{1,2})\.(\d{1,2})";
                    var dateMatch = Regex.Match(text, datePattern);
                    if (dateMatch.Success)
                    {
                        int day = int.Parse(dateMatch.Groups[1].Value);
                        int month = int.Parse(dateMatch.Groups[2].Value);
                        int year = currentDate.Year;

                        // Если месяц уже прошёл, берём следующий год
                        if (month < currentDate.Month || (month == currentDate.Month && day < currentDate.Day))
                        {
                            year++;
                        }

                        targetDate = new DateTime(year, month, day);
                    }
                }

                // Извлечение описания
                // Убираем временные метки и даты из текста
                description = text;
                description = Regex.Replace(description, timePattern, "");
                description = Regex.Replace(description, @"\d{1,2}\.\d{1,2}", "");
                description = Regex.Replace(description, @"сегодня|завтра|послезавтра", "");
                description = description.Trim();

                // Убираем лишние пробелы
                description = Regex.Replace(description, @"\s+", " ");

                if (string.IsNullOrWhiteSpace(description))
                {
                    return false;
                }

                // Делаем первую букву заглавной
                if (description.Length > 0)
                {
                    description = char.ToUpper(description[0]) + description.Substring(1);
                }

                dateTime = targetDate.Date + time;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}