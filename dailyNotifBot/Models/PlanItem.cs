using System;

namespace TelegramPlannerBot.Models
{
    public class PlanItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public long ChatId { get; set; }
        public DateTime DateTime { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsNotified { get; set; } = false;

        // Основные свойства
        public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;
        public int NotificationMinutes { get; set; } = 10;
        public DateTime? RecurrenceEndDate { get; set; }
        public string ParentRecurrenceId { get; set; } = string.Empty;
    }
}