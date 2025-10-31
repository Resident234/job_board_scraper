# Инструкция по сборке JobBoardScraper

## Требования

- .NET SDK 6.0 или выше
- Установите с: https://dotnet.microsoft.com/download

Проверка установки:
```bash
dotnet --version
```

## Автоматическая сборка

### Windows
Запустите скрипт:
```cmd
build.bat
```

Выберите тип сборки:
1. **Самодостаточная** - включает все зависимости, работает без установленного .NET (~70-100 MB)
2. **С зависимостью от .NET** - требует установленный .NET на целевой машине (~10-20 MB)
3. **Однофайловая** - все в одном .exe файле (~70-100 MB)
4. **Debug** - для разработки и отладки

### Linux/macOS
Сделайте скрипт исполняемым и запустите:
```bash
chmod +x build.sh
./build.sh
```

## Ручная сборка

### Простая сборка
```bash
dotnet build JobBoardScraper/JobBoardScraper.csproj -c Release
```

### Публикация для Windows (самодостаточная)
```bash
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

### Публикация для Linux (самодостаточная)
```bash
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r linux-x64 --self-contained true -o ./publish
```

### Однофайловая публикация
```bash
dotnet publish JobBoardScraper/JobBoardScraper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

## Запуск после сборки

### Windows
```cmd
.\publish\JobBoardScraper.exe
```

### Linux/macOS
```bash
chmod +x ./publish/JobBoardScraper
./publish/JobBoardScraper
```

## Важно

1. **App.config** - убедитесь, что файл `App.config` находится в той же папке, что и исполняемый файл
2. **База данных** - настройте строку подключения в `App.config`:
   ```xml
   <add key="Database:ConnectionString" value="Server=localhost:5432;User Id=postgres;Password=admin;Database=jobs;" />
   ```
3. **Логи** - по умолчанию создаются в папке `./logs`

## Очистка

### Windows
```cmd
rmdir /s /q publish
rmdir /s /q JobBoardScraper\bin
rmdir /s /q JobBoardScraper\obj
```

### Linux/macOS
```bash
rm -rf publish JobBoardScraper/bin JobBoardScraper/obj
```

## Размеры сборок

- **Самодостаточная**: ~70-100 MB (включает .NET Runtime)
- **С зависимостью от .NET**: ~10-20 MB (требует .NET на целевой машине)
- **Однофайловая**: ~70-100 MB (все в одном файле)
- **Debug**: ~5-10 MB (только для разработки)

## Поддерживаемые платформы

- Windows (win-x64, win-x86, win-arm64)
- Linux (linux-x64, linux-arm, linux-arm64)
- macOS (osx-x64, osx-arm64)

Для других платформ замените `-r win-x64` на нужный RID (Runtime Identifier).
