#!/bin/bash

# Скрипт установки Telegram Planner Bot на Ubuntu 24.04

set -e

echo "═══════════════════════════════════════════════"
echo "  Установка Telegram Planner Bot на Ubuntu"
echo "═══════════════════════════════════════════════"
echo ""

# Проверка root
if [ "$EUID" -ne 0 ]; then
  echo "❌ Запустите скрипт с sudo"
  exit 1
fi

# 1. Установка .NET 8.0
echo "📦 Установка .NET 8.0..."
apt update
apt install -y dotnet-sdk-8.0

# Проверка
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET не установлен!"
    exit 1
fi

echo "✅ .NET установлен: $(dotnet --version)"

# 2. Создание пользователя
echo ""
echo "👤 Создание пользователя telegrambot..."
if id "telegrambot" &>/dev/null; then
    echo "⚠️  Пользователь telegrambot уже существует"
else
    useradd -m -s /bin/bash telegrambot
    echo "✅ Пользователь создан"
fi

# 3. Копирование файлов
echo ""
echo "📂 Настройка проекта..."
PROJECT_DIR="/home/telegrambot/TelegramPlannerBot"

if [ ! -d "$PROJECT_DIR" ]; then
    echo "❌ Директория $PROJECT_DIR не найдена!"
    echo "   Скопируйте файлы проекта в $PROJECT_DIR"
    exit 1
fi

# 4. Проверка appsettings.json
if [ ! -f "$PROJECT_DIR/appsettings.json" ]; then
    echo ""
    echo "⚠️  appsettings.json не найден!"
    echo "   Создайте файл $PROJECT_DIR/appsettings.json"
    echo "   с вашим токеном бота"
    echo ""
    echo "Пример:"
    echo '{'
    echo '  "BotToken": "YOUR_TOKEN_HERE"'
    echo '}'
    exit 1
fi

# 5. Сборка проекта
echo ""
echo "🔨 Сборка проекта..."
cd "$PROJECT_DIR"
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "❌ Ошибка сборки!"
    exit 1
fi

echo "✅ Проект собран"

# 6. Публикация
echo ""
echo "📦 Публикация..."
dotnet publish -c Release -o ./publish

if [ $? -ne 0 ]; then
    echo "❌ Ошибка публикации!"
    exit 1
fi

echo "✅ Проект опубликован"

# 7. Права доступа
echo ""
echo "🔒 Настройка прав доступа..."
chown -R telegrambot:telegrambot "$PROJECT_DIR"
chmod 600 "$PROJECT_DIR/appsettings.json"

# 8. Создание директории для бэкапов
mkdir -p /home/telegrambot/backups
chown telegrambot:telegrambot /home/telegrambot/backups

# 9. Установка systemd сервиса
echo ""
echo "⚙️  Установка systemd сервиса..."

if [ -f "$PROJECT_DIR/telegrambot.service" ]; then
    cp "$PROJECT_DIR/telegrambot.service" /etc/systemd/system/
else
    echo "⚠️  Файл telegrambot.service не найден в проекте"
    echo "   Создайте его вручную в /etc/systemd/system/"
    exit 1
fi

# 10. Запуск сервиса
echo ""
echo "🚀 Запуск бота..."
systemctl daemon-reload
systemctl start telegrambot
systemctl enable telegrambot

# Проверка статуса
sleep 2
if systemctl is-active --quiet telegrambot; then
    echo "✅ Бот запущен и работает!"
else
    echo "❌ Бот не запустился. Проверьте логи:"
    echo "   sudo journalctl -u telegrambot -n 50"
    exit 1
fi

echo ""
echo "═══════════════════════════════════════════════"
echo "  ✅ Установка завершена!"
echo "═══════════════════════════════════════════════"
echo ""
echo "Полезные команды:"
echo "  sudo systemctl status telegrambot   - статус"
echo "  sudo systemctl restart telegrambot  - перезапуск"
echo "  sudo journalctl -u telegrambot -f   - логи"
echo ""
echo "Бэкапы будут сохраняться в /home/telegrambot/backups/"
echo ""