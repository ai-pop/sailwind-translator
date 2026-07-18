<div align="center">

# Sailwind Translator

**Русификатор игры Sailwind с переводом в реальном времени**

[![Версия](https://img.shields.io/badge/версия-1.3.3-5b9aa0.svg)](https://github.com/ai-pop/sailwind-translator/releases/latest)
[![Sailwind](https://img.shields.io/badge/Sailwind-0.38-21252b.svg)](https://store.steampowered.com/app/1764530/Sailwind/)
[![Unity](https://img.shields.io/badge/Unity-2019.1.10-21252b.svg)](https://unity.com/)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4.23-21252b.svg)](https://github.com/BepInEx/BepInEx)
[![Лицензия](https://img.shields.io/badge/license-MIT-5b9aa0.svg)](LICENSE)

*Russian localization mod for the game Sailwind with real-time translation engine*

</div>

---

## Описание

**Sailwind Translator** — мод для игры [Sailwind](https://store.steampowered.com/app/1764530/Sailwind/), выполняющий локализацию интерфейса и внутриигровых надписей на русский язык. Мод не требует ручного наполнения словаря: незнакомый текст переводится автоматически через онлайн-сервисы с кешированием результата.

**Ключевое отличие от классических русификаторов** — перевод работает в реальном времени: при появлении нового текста в сцене он отправляется в фоновый переводчик, результат сохраняется в кеш и применяется «на лету» без перезапуска игры.

---

## Возможности

### Движок перевода
- **Перехват TextMesh** — Harmony-патч на сеттер `UnityEngine.TextMesh.text` перехватывает весь текстовый поток игры (Sailwind использует TextMesh, а не TextMeshPro).
- **Активный сканер сцены** — перевод зашитого в префабы текста, который движок устанавливает в обход C#-сеттера.
- **RichTextTranslator** — перевод с сохранением разделителей форматирования (`\t`, `\n`, `\r`, `\f`, `\v`): форматированные подписи вроде `Walk\tForward` переводятся как `Идти\tВперёд` без поломки вёрстки.
- **LiveTranslator** — фоновый онлайн-перевод незнакомых строк через Google Translate (основной провайдер) и MyMemory (fallback), с параллелизмом (по умолчанию 4 потока) и персистентным кешем в `translations.json`.
- **Фильтрация технического текста** — клавиши (`W`, `F1`, `Space`), числа, короткие строки без латиницы не переводятся, что исключает мусорные результаты вроде `F1 → Ф1`.

### Шрифты и кириллица
- **FontManager** — выбор кириллического шрифта через UI (как выбор шейдера в OptiFine): список всех `.ttf`/`.otf` из папки плагина + системных шрифтов, применяется **на лету** без перезапуска.
- **FontCyrillicResolver** — динамическая загрузка шрифтов с проверкой кириллицы; подмена только при необходимости (если у текущего шрифта нет кириллицы).

### Интерфейс мода
- **GUISkin-минимализм** — тёмная тема, приглушённый teal-акцент (`#5b9aa0`), без эмодзи, чистая типографика.
- **4 вкладки**: Переводы, Настройки, Шрифты, Управление.
- **Жёсткая изоляция ввода** — пока UI открыт, Harmony-патчи глушат `GoPointer.DoRaycast`, `MainButtonDown`/`AltButtonDown`/`AltButtonHeld`, `MouseLook.Update`. Игровой UI гарантированно не реагирует на мышь.
- **Перетаскивание и resize окна** — шапка для drag, правый нижний угол для изменения размера. Минимум 560×400, контент адаптируется.
- **Переназначение клавиш** — `EditorKey` и `ToggleKey` настраиваются из UI.

---

## Системные требования

| Компонент | Версия | Примечание |
|-----------|--------|------------|
| **Sailwind** | 0.38 | Проверено на этой версии |
| **Unity** | 2019.1.10f1 | Версия движка игры |
| **BepInEx** | 5.4.23.5 (Mono) | Рекомендуемый бэкенд |
| **BepInEx** | 6.0.0-pre.2 (IL2CPP) | Экспериментально |
| **.NET** | netstandard2.0 | Целевой фреймворк плагина |
| **ОС** | Windows 7+ | Linux через Proton (с `WINEDLLOVERRIDES`) |
| **Интернет** | Опционально | Нужен только при первом запуске для живого перевода; далее работает из кеша |

---

## Быстрая установка

### Шаг 1. Определить бэкенд игры

Открой папку установки Sailwind (Steam → ПКМ → Manage → Browse local files):

- Есть `Sailwind_Data/Managed/Assembly-CSharp.dll` → **Mono** (рекомендуется)
- Есть `GameAssembly.dll` → **IL2CPP**

### Шаг 2. Скачать и распаковать пакет

Скачай `sailwind-translator-1.3.3-mono.zip` (или `-il2cpp.zip`) с [релиза](https://github.com/ai-pop/sailwind-translator/releases/latest) и распакуй содержимое **в корень папки игры** (рядом с `Sailwind.exe`).

Структура после установки:

```
Sailwind/
├── Sailwind.exe
├── winhttp.dll                  ← от BepInEx
├── doorstop_config.ini          ← только IL2CPP
├── BepInEx/
│   ├── core/
│   ├── config/
│   │   └── AutoTranslatorConfig.ini
│   └── plugins/
│       ├── SailwindTranslator/
│       │   ├── SailwindTranslator.dll
│       │   └── translations.json
│       ├── XUnity.AutoTranslator/
│       └── XUnity.ResourceRedirector/
```

### Шаг 3. Запуск

Запусти игру. В главном меню нажми **F3** — откроется окно редактора. **F2** — переключение RU ⇄ EN.

> При первом запуске нужен интернет: плагин переводит незнакомые строки онлайн и кеширует результат. Дальше работает офлайн.

---

## Конфигурация

Файл: `BepInEx/config/SailwindTranslator.cfg`

### General

| Параметр | По умолчанию | Описание |
|----------|:---:|----------|
| `Enable` | `true` | Глобально включает/выключает подмену текста. |
| `LiveTranslate` | `true` | Онлайн-перевод незнакомых строк. Выключи, если не нужен интернет-перевод. |
| `LiveWorkers` | `4` | Количество фоновых потоков перевода (1–8). Больше = быстрее, но выше нагрузка на сеть. |
| `DumpUntranslated` | `false` | Записывать непереведённые строки в `untranslated.csv` (для отладки). |
| `Language` | `ru` | Активный язык: `ru` / `en`. Переключается клавишей F2. |

### UI

| Параметр | По умолчанию | Описание |
|----------|:---:|----------|
| `EditorKey` | `F3` | Клавиша открытия/закрытия редактора. |
| `ToggleKey` | `F2` | Клавиша переключения RU ⇄ EN. |
| `GameFont` | (пусто) | Шрифт игры: `disk:filename.ttf`, `os:Arial` или пусто (авто). |
| `UiFont` | (пусто) | Отдельный шрифт UI мода (пусто = как у игры). |

---

## Архитектура

```
┌──────────────────────────────────────────────────────────────────┐
│                        Plugin.Awake()                            │
│  Регистрация: Manager, FontResolver, Harmony, Components, Live   │
└────────────────────────────┬─────────────────────────────────────┘
                             │
        ┌────────────────────┼────────────────────┐
        ▼                    ▼                    ▼
┌───────────────┐  ┌──────────────────┐  ┌─────────────────┐
│ TextMeshPatcher│  │ SceneTranslator  │  │   LiveTranslator│
│ (Harmony)      │  │ (MonoBehaviour)  │  │  (N потоков)    │
│                │  │                  │  │                 │
│ Prefix на      │  │ Каждые 0.5 сек   │  │ Google +        │
│ TextMesh.text  │  │ обходит все      │  │ MyMemory API    │
│ setter         │  │ TextMesh в сцене │  │                 │
│                │  │                  │  │ Очередь строк → │
│ Перевод +      │  │ Перевод +        │  │ кеш в JSON →    │
│ RichText       │  │ FontResolver     │  │ NeedsRescan     │
└───────┬───────┘  └────────┬─────────┘  └────────┬────────┘
        │                   │                     │
        └───────────────────┼─────────────────────┘
                            ▼
              ┌───────────────────────────┐
              │   RichTextTranslator      │
              │                           │
              │   Токенизация по          │
              │   разделителям (\t \n …)  │
              │         │                 │
              │   Перевод сегмента через  │
              │   TranslationManager      │
              │   (кеш) или LiveTranslator│
              │         │                 │
              │   Сборка обратно с        │
              │   сохранением разделителей│
              └───────────────────────────┘
                            │
                            ▼
              ┌───────────────────────────┐
              │   FontCyrillicResolver    │
              │                           │
              │   ApplyTo(TextMesh):      │
              │   подмена шрифта на       │
              │   кириллический при       │
              │   необходимости           │
              └───────────────────────────┘
```

### Поток данных

1. Игра устанавливает текст в `TextMesh.text` (сеттер или движок).
2. Harmony-prefix перехватывает значение → `RichTextTranslator.Translate()`.
3. RichTextTranslator разбивает по разделителям, каждый сегмент ищется в кеше (`TranslationManager`).
4. Незнакомые сегменты ставятся в очередь `LiveTranslator`.
5. LiveTranslator переводит онлайн (фон), сохраняет в `translations.json`, взводит `NeedsRescan`.
6. SceneTranslator подхватывает флаг и переприменяет переводы к TextMesh.
7. FontResolver подменяет шрифт, если у текущего нет кириллицы.

---

## Сборка из исходников

### Требования

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- Git

### Сборка

```bash
git clone https://github.com/ai-pop/sailwind-translator.git
cd sailwind-translator/plugin_src
dotnet build -c Release
```

Результат: `plugin_src/bin/Release/netstandard2.0/SailwindTranslator.dll`

### Структура исходников

```
plugin_src/
├── SailwindTranslator.csproj    # Проект (netstandard2.0)
├── Plugin.cs                    # Точка входа BepInEx
├── TextMeshPatcher.cs           # Harmony-патч TextMesh.text
├── RichTextTranslator.cs        # Перевод с разделителями
├── LiveTranslator.cs            # Онлайн-перевод (Google+MyMemory)
├── SceneTranslator.cs           # Активный сканер сцены
├── TranslationManager.cs        # Кеш + JSON + hot-reload
├── FontCyrillicResolver.cs      # Кириллический шрифт для TextMesh
├── FontManager.cs               # Выбор шрифта (OptiFine-style)
├── InputIsolation.cs            # Harmony-патчи изоляции ввода
├── MouseController.cs           # Управление курсором
├── ModUI.cs                     # GUISkin-минимализм UI
├── LangToggle.cs                # Переключатель RU/EN
├── TMProStub.cs                 # Стаб TMPro для компиляции
├── UnityEngineUIStub.cs         # Стаб UI.Text для компиляции
└── lib/                         # DLL-референсы (BepInEx, Unity, Harmony)
```

Плагин собирается на стаб-типах (`TMProStub.cs`, `UnityEngineUIStub.cs`) — реальные Unity/TMP-сборки не требуются. Сборка самодостаточна.

---

## Управление

| Клавиша | По умолчанию | Действие | Переназначаемая |
|:---:|:---:|---|:---:|
| Editor | `F3` | Открыть/закрыть редактор перевода | Да |
| Toggle | `F2` | Переключить язык RU ⇄ EN | Да |

Переназначение — во вкладке «Управление» в UI мода.

---

## Устранение неполадок

### Перевод не работает

1. Проверь лог `BepInEx/LogOutput.log.txt`.
2. Найди строку `Sailwind Translator v1.3.3 loaded` — если её нет, плагин не загрузился.
3. Проверь, что `SailwindTranslator.dll` в `BepInEx/plugins/SailwindTranslator/`.
4. Убедись, что `Language = ru` в конфиге (или нажми F2).

### Текст квадратиками

Шрифт игры не содержит кириллицы. Открой UI (F3) → вкладка «Шрифты» → выбери кириллический шрифт (например, Arial). Или положи `.ttf` с кириллицей в `BepInEx/plugins/SailwindTranslator/` и выбери его.

### Онлайн-перевод не идёт

1. Проверь интернет-соединение.
2. В логе ищи `[LIVE]` — если есть ошибки `[LIVE] Google не ответил`, провайдер временно недоступен.
3. Увеличь `LiveWorkers` в конфиге (до 8).
4. Уже переведённые строки остаются в кеше — после первого запуска интернет не нужен.

### UI не открывается

1. Проверь, что клавиша `EditorKey = F3` в конфиге.
2. Некоторые клавиши могут перехватываться игрой — переназначь на другую (например, F8).

### Курсор не освобождается

Мод вызывает штатный `MouseLook.ToggleMouseLookAndCursor(false)`. Если курсор всё ещё захвачен, возможно, игра в режиме, где этот механизм не срабатывает. Открой UI из главного меню или настроек игры, где курсор уже свободен.

---

## Совместимость и ограничения

- **Sailwind 0.38** — проверено. Другие версии могут требовать адаптации (особенно если разработчик изменил имена классов).
- **IL2CPP-билд** — экспериментальная поддержка через BepInEx 6.0.0-pre.2. Может быть нестабильной.
- **Proton/Linux** — нужен `WINEDLLOVERRIDES="winhttp=n,b" %command%` в параметрах запуска Steam.
- **Текст в кодеках/видео** — не переводится (это не Unity-текст).
- **VR-режим** — UI мода оптимизирован под мышь/клавиатуру; в VR может работать некорректно.

---

## Благодарности

- [BepInEx](https://github.com/BepInEx/BepInEx) — фреймворк для моддинга Unity.
- [HarmonyX](https://github.com/BepInEx/HarmonyX) — runtime-патчинг методов.
- [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator) — инфраструктура перевода.
- [MyMemory API](https://mymemory.translated.net/) — бесплатный API перевода.
- [Google Translate](https://translate.google.com/) — неофициальный endpoint `gtx`.

---

## Лицензия

MIT License. См. [LICENSE](LICENSE).

Исходный код мода открыт. Файлы BepInEx, XUnity.AutoTranslator и их зависимости распространяются под собственными лицензиями.

---

# For English speakers

**Sailwind Translator** is a Russian localization mod for the game [Sailwind](https://store.steampowered.com/app/1764530/Sailwind/) with a real-time translation engine.

### What it does
- Patches `UnityEngine.TextMesh.text` via Harmony to intercept all in-game text.
- Translates unknown strings on-the-fly using Google Translate (primary) and MyMemory (fallback), caching results in `translations.json`.
- Preserves formatting delimiters (`\t`, `\n`) so layouts like `Walk\tForward` → `Идти\tВперёд` stay intact.
- Provides a minimalist in-game UI (F3) with tabs: Translations, Settings, Fonts, Controls.
- Lets you pick a Cyrillic font from a list (OptiFine-style) and applies it live.

### Quick install
1. Identify your game backend: `Sailwind_Data/Managed/Assembly-CSharp.dll` → Mono; `GameAssembly.dll` → IL2CPP.
2. Download `sailwind-translator-1.3.3-mono.zip` (or `-il2cpp.zip`) from [Releases](https://github.com/ai-pop/sailwind-translator/releases/latest).
3. Extract into the game folder (next to `Sailwind.exe`).
4. Launch. Press **F3** to open the editor, **F2** to toggle RU ⇄ EN.

Internet is required only on first launch for online translation; afterwards everything works from cache.

### Build from source
```bash
git clone https://github.com/ai-pop/sailwind-translator.git
cd sailwind-translator/plugin_src
dotnet build -c Release
```

### License
MIT. See [LICENSE](LICENSE).
