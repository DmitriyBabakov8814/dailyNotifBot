using System;

namespace TelegramPlannerBot.Models
{
    public class PlanTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public long ChatId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TimeSpan Time { get; set; }
        public PlanCategory Category { get; set; } = PlanCategory.Other;
        public PlanPriority Priority { get; set; } = PlanPriority.Medium;
        public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;
        public int NotificationMinutes { get; set; } = 10;
        public string Location { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}