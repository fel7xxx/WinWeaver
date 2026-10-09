# Безопасность / Security

WinWeaver работает с правами администратора, поэтому к уязвимостям мы относимся серьёзно.
WinWeaver runs with administrator rights, so security reports are taken seriously.

## Как сообщить / How to report

**Не создавай открытую задачу (Issue) для уязвимости.** Используй приватную форму GitHub:
**Do not open a public issue for a vulnerability.** Use GitHub's private form:

https://github.com/fel7xxx/WinWeaver/security/advisories/new

Опиши, что можно сделать, как это повторить и в какой версии. Ответ — обычно в течение недели.
Describe the impact, how to reproduce it and the affected version. Expect a reply within about a week.

## Поддерживаемые версии / Supported versions

Исправления выходят для последней версии. / Fixes are released for the latest version only.

## Что уже сделано / Built-in protections

- Папка данных `C:\ProgramData\WinWeaver` доступна для записи только администраторам и SYSTEM — другой пользователь не может подменить историю отката. / The data folder is writable only by Administrators and SYSTEM, so undo history can't be tampered with.
- История хранит данные, а не код: при откате ничего из файла не выполняется. / History stores data, not code: nothing from the file is executed on undo.
- Скачанные установщики проверяются по цифровой подписи или по закреплённому SHA-256 (7-Zip) и хранятся в защищённой папке. / Downloaded installers are verified by Authenticode signature or a pinned SHA-256 (7-Zip) and kept in a protected folder.
- Задача автозапуска создаётся только для копии в Program Files. / The autostart task is only created for the copy in Program Files.
- Проверка обновлений лишь открывает страницу релиза — программа ничего не скачивает и не обновляет сама. / The update check only opens the release page; nothing is downloaded or installed automatically.
