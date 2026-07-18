# Sailwind Translator — Исправленная версия

## Исправления (v1.1.0)

### Проблема
Оригинальная версия вызывала бесконечный цикл ошибок:
```
FileNotFoundException: Could not load file or assembly 'UnityEngine.InputLegacyModule'
```

### Причина
- `EditorMenu.cs` и `LangToggle.cs` использовали `Input.GetKeyDown()` из модуля `UnityEngine.InputLegacyModule`
- DLLки в `lib/` были неправильной версии или несовместимы с игрой

### Решение
- Заменены все вызовы `Input.GetKeyDown()` на `Event.current` (IMGUI-way)
- Удалена зависимость от `UnityEngine.InputLegacyModule` из `.csproj`
- Добавлена защита от спама (cooldown на toggle)

## Управление

| Клавиша | Действие |
|---------|----------|
| **E** | Открыть/закрыть редактор перевода |
| **F2** | Переключить язык RU/EN |

## Установка

1. Скопируй `BepInEx/` в папку с игрой
2. Скопируй `SailwindTranslator.dll` в `BepInEx/plugins/SailwindTranslator/`
3. Скопируй `translations.json` в `BepInEx/plugins/SailwindTranslator/`
4. Запусти игру

## Файлы

```
install_il2cpp/
├── BepInEx/
│   ├── core/           # BepInEx и зависимости
│   └── plugins/
│       └── SailwindTranslator/
│           ├── SailwindTranslator.dll
│           └── translations.json
└── doorstop_config.ini
```

## Сборка

```bash
cd plugin_src
dotnet build -c Release
```

## Структура исходников

```
plugin_src/
├── SailwindTranslator.csproj   # Проект
├── Plugin.cs                   # Точка входа
├── TextPatcher.cs             # Harmony-патчи для текста
├── TranslationManager.cs      # Менеджер переводов
├── EditorMenu.cs              # IMGUI редактор (E)
├── LangToggle.cs              # Переключатель языка (F2)
├── FontAutoFit.cs             # Автоподгон шрифта
├── FontCyrillicResolver.cs    # Кириллические шрифты
└── lib/                       # DLL зависимости
```
