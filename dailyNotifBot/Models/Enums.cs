namespace TelegramPlannerBot.Models
{
    public enum RecurrenceType
    {
        None,
        Daily,
        Weekly,
        Monthly
    }

    public enum PlanCategory
    {
        Work,      // 💼 Работа
        Family,    // 👨‍👩‍👧 Семья
        Sport,     // 🏃 Спорт
        Health,    // 🏥 Здоровье
        Shopping,  // 🛒 Покупки
        Study,     // 📚 Учеба
        Other      // 📌 Другое
    }

    public enum PlanPriority
    {
        Low,       // 🟢 Низкая
        Medium,    // 🟡 Средняя
        High       // 🔴 Высокая
    }

    public enum UserState
    {
        None,
        WaitingForTimezone,

        // Создание плана
        WaitingForDateTime,
        WaitingForTime,
        WaitingForDescription,
        WaitingForCategory,
        WaitingForPriority,
        WaitingForNotificationTime,
        WaitingForRecurrence,
        WaitingForLocation,
        WaitingForNotes,

        // Редактирование
        WaitingForEditSelection,
        WaitingForEditChoice,
        WaitingForEditValue,

        // Удаление
        WaitingForDeleteConfirmation,

        // Поиск
        WaitingForSearchQuery,

        // Шаблоны
        WaitingForTemplateSelection,
        WaitingForTemplateName
    }
}