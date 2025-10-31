@echo off
REM Скрипт сборки JobBoardScraper для Windows

echo ========================================
echo JobBoardScraper Build Script
echo ========================================
echo.

REM Проверка наличия dotnet
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK не найден! Установите .NET SDK с https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [INFO] Найдена версия .NET:
dotnet --version
echo.

REM Очистка предыдущей сборки
echo [INFO] Очистка предыдущей сборки...
if exist publish rmdir /s /q publish
if exist JobBoardScraper\bin rmdir /s /q JobBoardScraper\bin
if exist JobBoardScraper\obj rmdir /s /q JobBoardScraper\obj
echo.

REM Выбор типа сборки
echo Выберите тип сборки:
echo 1. Release (самодостаточная, все зависимости включены)
echo 2. Release (требует установленный .NET, меньший размер)
echo 3. Release (однофайловая, все в одном .exe)
echo 4. Debug (для разработки)
echo.
set /p choice="Введите номер (1-4): "

if "%choice%"=="1" goto build_self_contained
if "%choice%"=="2" goto build_framework_dependent
if "%choice%"=="3" goto build_single_file
if "%choice%"=="4" goto build_debug
goto invalid_choice

:build_self_contained
echo.
echo [INFO] Сборка самодостаточной Release версии...
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained true -o ./publish
goto post_build

:build_framework_dependent
echo.
echo [INFO] Сборка Release версии (требует .NET)...
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained false -o ./publish
goto post_build

:build_single_file
echo.
echo [INFO] Сборка однофайловой Release версии...
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
goto post_build

:build_debug
echo.
echo [INFO] Сборка Debug версии...
dotnet build JobBoardScraper/JobBoardScraper.csproj -c Debug
echo.
echo [SUCCESS] Debug сборка завершена!
echo Файлы находятся в: JobBoardScraper\bin\Debug\
pause
exit /b 0

:post_build
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Ошибка при сборке!
    pause
    exit /b 1
)

echo.
echo [INFO] Копирование конфигурационных файлов...
copy JobBoardScraper\App.config publish\ >nul 2>nul

echo.
echo [SUCCESS] Сборка завершена успешно!
echo.
echo Файлы находятся в папке: .\publish\
echo Запуск: .\publish\JobBoardScraper.exe
echo.
echo [INFO] Содержимое папки publish:
dir /b publish
echo.
pause
exit /b 0

:invalid_choice
echo.
echo [ERROR] Неверный выбор!
pause
exit /b 1
