# Установка Sailwind Translator

**Полная инструкция по установке русификатора Sailwind Translator.**

---

## Быстрая установка (для большинства пользователей)

### 1. Определите бэкенд игры

Откройте папку установки Sailwind. В Steam: правый клик по игре → **Управление** → **Просмотреть локальные файлы**.

Проверьте содержимое папки:

| Файл в папке игры | Бэкенд | Пакет для скачивания |
|---|---|---|
| `Sailwind_Data\Managed\Assembly-CSharp.dll` | **Mono** | `sailwind-translator-1.3.3-mono.zip` |
| `GameAssembly.dll` | **IL2CPP** | `sailwind-translator-1.3.3-il2cpp.zip` |

> **Рекомендуется Mono** — стабильнее, полностью протестировано.
> **IL2CPP — экспериментально**, для продвинутых пользователей.

### 2. Скачайте и распакуйте пакет

1. Перейдите на [страницу последнего релиза](https://github.com/ai-pop/sailwind-translator/releases/latest).
2. Скачайте файл `sailwind-translator-1.3.3-mono.zip` (или `-il2cpp.zip`).
3. Распакуйте архив **в корневую папку игры** — туда, где находится `Sailwind.exe`.

### 3. Запустите игру

- При первом запуске **требуется интернет** — плагин переводит незнакомые строки онлайн и кеширует результат.
- В главном меню нажмите **F3** — откроется окно редактора.
- Нажмите **F2** для переключения языка RU ⇄ EN.

---

## Структура после установки

```
Sailwind/
├── Sailwind.exe
├── winhttp.dll                     ← от BepInEx
├── doorstop_config.ini             ← только для IL2CPP
├── .doorstop_version               ← только для IL2CPP
├── dotnet/                         ← только для IL2CPP
└── BepInEx/
    ├── core/                       ← ядро BepInEx
    ├── config/
    │   ├── AutoTranslatorConfig.ini
    │   └── SailwindTranslator.cfg  ← создаётся при первом запуске
    └── plugins/
        ├── SailwindTranslator/
        │   ├── SailwindTranslator.dll
        │   └── translations.json   ← кеш переводов
        ├── XUnity.AutoTranslator/
        └── XUnity.ResourceRedirector/
```

---

## Установка только плагина

Если у вас **уже установлен** BepInEx 5 (Mono) для другой модификации Sailwind, можно установить только сам русификатор:

1. Скачайте `sailwind-translator-1.3.3-plugin.zip`.
2. Распакуйте содержимое в `BepInEx/plugins/SailwindTranslator/`.
3. Запустите игру.

Или используйте отдельный DLL:

1. Скачайте `sailwind-translator-1.3.3.dll`.
2. Поместите его в `BepInEx/plugins/SailwindTranslator/`.
3. Создайте пустой `translations.json` рядом (или скачайте `sailwind-translator-1.3.3.template.json` и переименуйте).

---

## Установка на Linux / Proton (Steam Deck)

1. В свойствах игры в Steam → **Параметры запуска** добавьте:

   ```
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

2. Дальше — стандартная установка (распаковать пакет в папку игры).

---

## Проверка целостности

В релизе приложен файл `SHA256SUMS.txt` с контрольными суммами всех пакетов.

### Windows (PowerShell)
```powershell
Get-FileHash sailwind-translator-1.3.3-mono.zip -Algorithm SHA256
```

### Linux / macOS
```bash
sha256sum sailwind-translator-1.3.3-mono.zip
```

Сравните результат с соответствующей строкой в `SHA256SUMS.txt`.

---

## Первый запуск

1. Откройте игру. Первый запуск может занять 1–5 минут на IL2CPP (BepInEx выполняет unhollowing — не закрывайте консоль).
2. В главном меню вы увидите перевод кнопок («Начать игру», «Настройки», «Выход»).
3. Нажмите **F3**, чтобы открыть редактор переводов.

### Лог загрузки

В файле `BepInEx/LogOutput.log.txt` должна быть строка:
```
[Info:Sailwind Translator] Sailwind Translator v1.3.3 loaded.
```

---

## Настройка шрифта (если текст отображается квадратиками)

1. Поместите любой шрифт `.ttf` с поддержкой кириллицы (например, [Noto Sans](https://fonts.google.com/noto/specimen/Noto+Sans), [Roboto](https://fonts.google.com/specimen/Roboto) или `arial.ttf` из `C:\Windows\Fonts`) в папку `BepInEx/plugins/SailwindTranslator/`.
2. В игре откройте UI мода (**F3**) → вкладка **«Шрифты»**.
3. Нажмите **«Пересканировать папку»** → выберите ваш шрифт из списка.
4. Шрифт применяется **на лету**, без перезапуска.

---

## Управление

| Клавиша | Действие |
|:---:|---|
| **F3** | Открыть/закрыть редактор перевода (переназначаемая) |
| **F2** | Переключить язык RU ⇄ EN (переназначаемая) |

Переназначение — в редакторе, вкладка **«Управление»**.

---

## Устранение неполадок

| Симптом | Решение |
|---|---|
| Перевод не работает | Проверьте лог на наличие строки `Sailwind Translator v1.3.3 loaded`. Убедитесь, что DLL в `BepInEx/plugins/SailwindTranslator/`. |
| Текст квадратиками | Откройте F3 → «Шрифты» → выберите кириллический шрифт. |
| Онлайн-перевод не идёт | Проверьте интернет. В логе ищите `[LIVE]`. |
| UI не открывается | Проверьте `EditorKey = F3` в `BepInEx/config/SailwindTranslator.cfg`. |
| Игра не запускается | Удалите `winhttp.dll` и `BepInEx/` и переустановите с нуля. |

Подробное устранение неполадок — в [README.md](README.md#устранение-неполадок).

---

# Installation (English)

## Quick install

1. **Identify backend**: `Sailwind_Data/Managed/Assembly-CSharp.dll` → Mono; `GameAssembly.dll` → IL2CPP.
2. **Download** `sailwind-translator-1.3.3-mono.zip` (or `-il2cpp.zip`) from [Releases](https://github.com/ai-pop/sailwind-translator/releases/latest).
3. **Extract** into the game folder (next to `Sailwind.exe`).
4. **Launch** the game. Internet required on first run for online translation.
5. Press **F3** to open the editor, **F2** to toggle RU ⇄ EN.

## Linux / Proton

Add to Steam launch options:
```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

## Verify integrity

Compare SHA256 with `SHA256SUMS.txt` (included in the release):
```bash
sha256sum sailwind-translator-1.3.3-mono.zip
```

## Font setup (if text shows as squares)

Place any `.ttf` with Cyrillic support into `BepInEx/plugins/SailwindTranslator/`, open the mod UI (F3) → **Fonts** tab → rescan and select. Applies live, no restart needed.
