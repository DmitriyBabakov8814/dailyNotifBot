namespace TelegramPlannerBot.Models
{
    public enum RecurrenceType
    {
        None,
        Daily,
        Weekly,
        Monthly
    }

    public enum UserState
    {
        None,
        WaitingForTimezone,

        // Создание плана
        WaitingForDateTime,
        WaitingForTime,
        WaitingForDescription,
        WaitingForNotificationTime,
        WaitingForRecurrence,

        // Редактирование
        WaitingForEditSelection,
        WaitingForEditChoice,
        WaitingForEditValue,

        // Удаление
        WaitingForDeleteConfirmation,

        // Поиск
        WaitingForSearchQuery
    }
}