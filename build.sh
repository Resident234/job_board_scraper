#!/bin/bash
# Скрипт сборки JobBoardScraper для Linux/macOS

echo "========================================"
echo "JobBoardScraper Build Script"
echo "========================================"
echo ""

# Проверка наличия dotnet
if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] .NET SDK не найден! Установите .NET SDK с https://dotnet.microsoft.com/download"
    exit 1
fi

echo "[INFO] Найдена версия .NET:"
dotnet --version
echo ""

# Очистка предыдущей сборки
echo "[INFO] Очистка предыдущей сборки..."
rm -rf publish
rm -rf JobBoardScraper/bin
rm -rf JobBoardScraper/obj
echo ""

# Выбор типа сборки
echo "Выберите тип сборки:"
echo "1. Release (самодостаточная, все зависимости включены) - Linux"
echo "2. Release (требует установленный .NET, меньший размер) - Linux"
echo "3. Release (однофайловая, все в одном исполняемом файле) - Linux"
echo "4. Debug (для разработки)"
echo ""
read -p "Введите номер (1-4): " choice

case $choice in
    1)
        echo ""
        echo "[INFO] Сборка самодостаточной Release версии для Linux..."
        dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r linux-x64 --self-contained true -o ./publish
        ;;
    2)
        echo ""
        echo "[INFO] Сборка Release версии (требует .NET) для Linux..."
        dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r linux-x64 --self-contained false -o ./publish
        ;;
    3)
        echo ""
        echo "[INFO] Сборка однофайловой Release версии для Linux..."
        dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
        ;;
    4)
        echo ""
        echo "[INFO] Сборка Debug версии..."
        dotnet build JobBoardScraper/JobBoardScraper.csproj -c Debug
        if [ $? -eq 0 ]; then
            echo ""
            echo "[SUCCESS] Debug сборка завершена!"
            echo "Файлы находятся в: JobBoardScraper/bin/Debug/"
        else
            echo ""
            echo "[ERROR] Ошибка при сборке!"
            exit 1
        fi
        exit 0
        ;;
    *)
        echo ""
        echo "[ERROR] Неверный выбор!"
        exit 1
        ;;
esac

if [ $? -ne 0 ]; then
    echo ""
    echo "[ERROR] Ошибка при сборке!"
    exit 1
fi

echo ""
echo "[INFO] Копирование конфигурационных файлов..."
cp JobBoardScraper/App.config publish/ 2>/dev/null || true

echo ""
echo "[INFO] Установка прав на выполнение..."
chmod +x publish/JobBoardScraper 2>/dev/null || true

echo ""
echo "[SUCCESS] Сборка завершена успешно!"
echo ""
echo "Файлы находятся в папке: ./publish/"
echo "Запуск: ./publish/JobBoardScraper"
echo ""
echo "[INFO] Содержимое папки publish:"
ls -lh publish/
echo ""
