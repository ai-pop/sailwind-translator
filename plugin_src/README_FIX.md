# Sailwind Translator — Исправленная версия

## Что это

Русификатор игры **Sailwind** (проверено на **v0.38, Mono**) на базе
BepInEx + XUnity.AutoTranslator + собственный плагин `SailwindTranslator`
для подмены текста, кириллических шрифтов и внутриигрового редактора
перевода.

## Что исправлено в v1.1.0

### 🔴 Главный баг — F2 не работал
Переключатель языка проверял клавишу в `Update()`, где `Event.current`
**всегда `null`**. Из-за этого нажатие F2 просто игнорировалось.
Проверка перенесена в `OnGUI()` — теперь работает корректно.

### История: почему вообще убрали InputLegacy
Оригинальная v1.0.0 сыпала
```
FileNotFoundException: Could not load file or assembly 'UnityEngine.InputLegacyModule'
```
при загрузке. Тогда все `Input.GetKeyDown()` заменили на `Event.current`,
но забыли, что в `Update()` это не работает. v1.1.0 держит подход без
`InputLegacyModule` (чтобы не возвращать ошибку), но переносит проверку
туда, где `Event.current` реально валиден — в `OnGUI()`.

### Прочее
- Версия плагина `1.0.0` → `1.1.0` (и в `Plugin.cs`, и в атрибуте
  `BepInPlugin`, и в метаданных сборки).
- Удалён мёртвый код (`EditorMenu.Update`, неиспользуемые поля
  `_lastEnglish`/`_dummy` в `TextPatcher`).
- Убран устаревший `Application.RegisterLogCallback`.
- Документация хоткеев приведена к коду (F3 + F2).

## Управление

| Клавиша | Действие |
|---------|----------|
| **F3** | Открыть/закрыть редактор перевода |
| **F2** | Переключить язык RU/EN |

> Примечание: редактор вынесен на **F3**, а не на `E` — в Sailwind клавиша
> `E` используется для взаимодействия с предметами/дверями.

## Установка

1. Узнай билд игры (Mono/IL2CPP) — см. `INSTALL_RU.txt`.
2. Скопируй содержимое `install_mono/` (рекомендуется) или
   `install_il2cpp/` в папку с игрой.
3. Запусти игру. В меню нажми **F3** — откроется редактор.

## Сборка

```bash
cd plugin_src
dotnet build -c Release
# результат: plugin_src/bin/Release/netstandard2.0/SailwindTranslator.dll
```

Нужен .NET SDK 8+. Плагин собирается на stub-типах (`TMProStub.cs`,
`UnityEngineUIStub.cs`) — реальные Unity/TMP-сборки не требуются.

## Структура исходников

```
plugin_src/
├── SailwindTranslator.csproj   # Проект
├── Plugin.cs                   # Точка входа (BepInPlugin)
├── TextPatcher.cs              # Harmony-патчи для TMP_Text / UI.Text
├── TranslationManager.cs       # Менеджер переводов + JSON + hot-reload
├── EditorMenu.cs               # IMGUI редактор (F3)  — OnGUI()
├── LangToggle.cs               # Переключатель языка (F2) — OnGUI()
├── FontAutoFit.cs              # Автоподгон шрифта
├── FontCyrillicResolver.cs     # Кириллические шрифты
├── TMProStub.cs                # Stub TMP для компиляции
├── UnityEngineUIStub.cs        # Stub UI.Text для компиляции
└── lib/                        # DLL-референсы (BepInEx, Unity, Harmony)
```

## Куда что кладётся в установленной игре

```
<игра>/BepInEx/plugins/SailwindTranslator/
├── SailwindTranslator.dll
└── translations.json
```
