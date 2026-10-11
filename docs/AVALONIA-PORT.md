# Переход интерфейса на Avalonia

Начиная с версии 1.0.2 интерфейс WinWeaver работает на **Avalonia 11.2.3** (Skia) вместо GDI+/User32. Логика приложения, страницы, диалоги, анимации и PowerShell-движок перенесены целиком.

## Как устроено

```
WinWeaver/
  App/      — код приложения (MainForm, страницы, карточки, диалоги, движок, Theme, L …)
  Compat/   — слой совместимости WinForms/GDI+ → Avalonia
  Assets/   — шрифты Inter (AvaloniaResource)
  Resources/— engine.ps1, картинки, lang.en.tsv, иконка, app.manifest
```

Исходное приложение почти полностью рисует интерфейс само (свои Card, SettingCard, Hero, Sidebar,
TabStrip, переключатели, кнопки…). Чтобы сохранить внешний вид пиксель-в-пиксель и не переписывать
~30 тыс. строк логики, сделан тонкий слой `Compat/`, повторяющий нужную часть API WinForms/GDI+
поверх Avalonia:

| WinForms / GDI+ | Avalonia |
|---|---|
| `Control` | `ControlView : Avalonia.Controls.Control` (раскладка по Bounds, ввод, фокус, z-order) |
| `Form` | `Avalonia.Controls.Window` (рамка, прозрачность, TopMost, модальность через `DispatcherFrame`) |
| `Graphics`, `Pen`, `Brush`, `GraphicsPath`, `Region` | `DrawingContext`, `ImmutablePen`, кисти, `StreamGeometry` |
| `TextRenderer`, `Font` | `TextLayout`, `Typeface` (кэш раскладок) |
| `Bitmap` | `RenderTargetBitmap` / `Bitmap` (с учётом масштаба HiDPI) |
| `TextBox` | настоящий `Avalonia.Controls.TextBox` |
| `ContextMenuStrip` (DarkMenu) | `Avalonia.Controls.ContextMenu` в тёмной теме |
| `Timer`, `BeginInvoke`, `Application.Run` | `DispatcherTimer`, `Dispatcher.UIThread`, `DispatcherFrame` |
| `Clipboard`, `Save/OpenFileDialog`, `Screen`, `Cursor` | `IClipboard`, `StorageProvider`, `Screens`, курсоры Avalonia |

Платформенный код переписан: Win32-окно уведомлений (`UpdateLayeredWindow`) → прозрачное
окно Avalonia; «вуаль» перехода через `PrintWindow` → снимок контрола через `RenderTargetBitmap`;
vsync-поток на `DwmFlush` → таймер кадров диспетчера; `RedrawWindow`, `EM_SETCUEBANNER`,
`SetWindowTheme`, `PrivateFontCollection` и пр. → средства Avalonia. Тёмный заголовок и скругление
окон через DWM на Windows сохранены (используют настоящий HWND окна Avalonia).


## Ограничения

* Порт собран и проверен на Linux (обе сборки компилируются без ошибок; интерфейс net8.0 проверен
  скриншотами всех 12 разделов и 8 диалогов, клики и прокрутка). **На реальной Windows с
  PowerShell-движком запуск не проверялся** — перед релизом прогоните автонастройку и откаты на тестовой машине/ВМ.
* Значки Segoe Fluent Icons берутся из системы — на Windows 11 они есть, на Linux вместо них пусто.
* Windows-сборка — один `WinWeaver.exe` (~18 МБ): управляемые библиотеки загружаются из ресурсов через `AssemblyResolve`, нативные (`libSkiaSharp`, `libHarfBuzzSharp`, `av_libglesv2`) распаковываются в `%ProgramData%\WinWeaver\runtime\<версия>` и проверяются по SHA-256 (`App/Embedded.cs`, цель `WWEmbedDependencies` в `WinWeaver.csproj`). Файл `.exe.config` с binding redirects больше не используется — версии сборок разрешаются по имени из встроенных ресурсов.
* Анимации идут от таймера ~60 Гц, а не от DWM vsync.
