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

        // Расширенные свойства
        public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;
        public PlanCategory Category { get; set; } = PlanCategory.Other;
        public PlanPriority Priority { get; set; } = PlanPriority.Medium;
        public int NotificationMinutes { get; set; } = 10;
        public string Location { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime? RecurrenceEndDate { get; set; }
        public string ParentRecurrenceId { get; set; } = string.Empty;
    }
}