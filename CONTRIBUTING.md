# Как помочь проекту / Contributing

Спасибо, что хочешь помочь! *English below.*

## Сообщить об ошибке или предложить идею
- Ошибка: **Настройки → Сообщить об ошибке** в программе (версия и журнал подставятся сами) или [форма на GitHub](https://github.com/fel7xxx/WinWeaver/issues/new?template=bug_report.yml).
- Идея: [форма идеи](https://github.com/fel7xxx/WinWeaver/issues/new?template=feature_request.yml).
- Вопрос: [Обсуждения](https://github.com/fel7xxx/WinWeaver/discussions).

## Сборка
Нужен [.NET SDK 8](https://dotnet.microsoft.com/download) (собирается под .NET Framework 4.8, который уже есть в Windows 11).

```
dotnet publish WinWeaver/WinWeaver.csproj -f net48 -c Release -o publish
```
или двойной щелчок по `build-windows.bat`. Готовая программа — один файл `publish\WinWeaver.exe` (все библиотеки встроены).

Демо-режим без изменений в системе: `WinWeaver.exe /fake` (всё рисуется на тестовых данных) или на любой ОС `dotnet run --project WinWeaver/WinWeaver.csproj -f net8.0 -- /fake`.

## Устройство
- `WinWeaver/App/*.cs` — код приложения: страницы, карточки, диалоги (C# 12). Тексты интерфейса пишутся на русском — это исходный язык.
- `WinWeaver/Compat/` — слой совместимости WinForms/GDI+ → Avalonia (см. `docs/AVALONIA-PORT.md`).
- `WinWeaver/Resources/engine.ps1` — движок на Windows PowerShell 5.1: чтение и изменение настроек, история, откат. Не используй синтаксис PowerShell 7 (`??`, `?.`, `&&`).
- `WinWeaver/Resources/lang.*.tsv` — встроенные переводы.
- `scripts/` — проверки: `powershell -NoProfile -File scripts/verify.ps1` (сборка, синтаксис движка, откат на заглушках).

## Правила для изменений
1. Каждое изменение системы должно попадать в «Историю» и откатываться.
2. Рискованное — только в экспертном режиме (`.Expert()` у карточки).
3. Ничего не скачивать без проверки подписи или хеша.
4. Новые тексты — сразу с переводом в `WinWeaver/Resources/lang.en.tsv`.
5. В новых файлах — заголовок с лицензией GPL-3.0 (как в существующих).

## Перевод на другой язык
1. Скопируй `WinWeaver/Resources/lang.en.tsv` в файл `<код>.tsv` (например, `uk.tsv`, `de.tsv`).
2. В каждой строке: `русский текст<TAB>перевод`. Меняй только часть после табуляции.
   - `\n` — перенос строки, `{0}`, `{1}` — подставляемые значения (их нужно сохранить).
   - Строки, начинающиеся с `#`, — комментарии.
3. Положи файл в папку `lang` рядом с `WinWeaver.exe` — язык появится в «Настройках». Чтобы встроить его в exe, добавь строку в `WinWeaver.csproj` по примеру `Resources\lang.en.tsv`.
4. Найти непереведённые тексты: запусти `WinWeaver.exe /fake` с переменной окружения `WW_LANG=<код>` и `WW_LANG_MISSING=C:\temp\missing.txt` — пройди по разделам, в файле окажутся тексты без перевода.

---

## English

- **Bugs:** in the app use **Settings → Report a bug**, or the [bug form](https://github.com/fel7xxx/WinWeaver/issues/new?template=bug_report.yml). **Ideas:** [feature form](https://github.com/fel7xxx/WinWeaver/issues/new?template=feature_request.yml). **Questions:** [Discussions](https://github.com/fel7xxx/WinWeaver/discussions).
- **Build:** .NET SDK 8, then `dotnet publish WinWeaver/WinWeaver.csproj -f net48 -c Release -o publish` (targets .NET Framework 4.8; the result is a single `publish\WinWeaver.exe` with all libraries embedded). Demo mode: `WinWeaver.exe /fake`, or on any OS `dotnet run --project WinWeaver/WinWeaver.csproj -f net8.0 -- /fake`.
- **Layout:** `WinWeaver/App` — application code (source texts are Russian); `WinWeaver/Compat` — WinForms/GDI+ → Avalonia layer; `WinWeaver/Resources/engine.ps1` — Windows PowerShell 5.1 engine (no PS7-only syntax); `WinWeaver/Resources/lang.*.tsv` — translations; `scripts/verify.ps1` — checks.
- **Rules:** every system change goes to History and can be undone; risky options only in expert mode; never download without signature/hash checks; add translations for new texts; keep the GPL-3.0 header.
- **Translations:** copy `WinWeaver/Resources/lang.en.tsv` to `<code>.tsv`, translate the part after the TAB (keep `\n` and `{0}`), put it into a `lang` folder next to the exe. Run with `WW_LANG=<code>` and `WW_LANG_MISSING=<file>` to collect untranslated texts.

By contributing you agree that your contribution is licensed under GPL-3.0-or-later.
