using System.Collections.Generic;

namespace TelegramPlannerBot.Models
{
    public class UserSession
    {
        public UserState State { get; set; } = UserState.None;
        public string TimeZoneId { get; set; } = "Russian Standard Time";

        // Временные данные для создания/редактирования плана
        public PlanItem? CurrentPlan { get; set; }
        public List<PlanItem> TempPlansList { get; set; } = new List<PlanItem>();
        public string EditField { get; set; } = string.Empty;
        public string TempValue { get; set; } = string.Empty;
    }
}