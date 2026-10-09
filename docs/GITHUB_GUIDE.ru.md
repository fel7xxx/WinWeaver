# Как выложить WinWeaver на GitHub — пошагово

Инструкция для тех, кто ни разу не пользовался GitHub. Понадобится около 20 минут. Командная строка не нужна — всё делается в программе **GitHub Desktop** и на сайте.

> Все ссылки в программе ведут на `https://github.com/fel7xxx/WinWeaver`. Поэтому аккаунт должен называться **fel7xxx**, а репозиторий — **WinWeaver** (с большими W).

---

## Шаг 1. Аккаунт на GitHub

1. Открой https://github.com/signup.
2. Введи почту, пароль и имя пользователя **fel7xxx**.
3. Подтверди почту (придёт код).
4. Рекомендуется сразу включить двухфакторную защиту: аватарка справа сверху → **Settings → Password and authentication → Enable two-factor authentication**.

## Шаг 2. GitHub Desktop

1. Скачай и установи https://desktop.github.com.
2. При первом запуске нажми **Sign in to GitHub.com** — откроется браузер, разреши доступ.
3. На шаге «Configure Git» оставь имя **fel7xxx** и свою почту → **Finish**.

## Шаг 3. Папка проекта

1. Распакуй архив `WinWeaver-1.0.0-source.zip`, например, в `Документы\GitHub\WinWeaver`.
2. Внутри должны лежать `README.md`, `LICENSE`, папки `app`, `.github`, `screenshots` и т. д. (папка `.github` может быть скрытой — это нормально).

## Шаг 4. Создать репозиторий

1. В GitHub Desktop: **File → Add local repository…**
2. Нажми **Choose…** и выбери папку `WinWeaver`.
3. Появится надпись «This directory does not appear to be a Git repository» — нажми ссылку **create a repository**.
4. В окне создания:
   - **Name:** `WinWeaver`
   - **Description:** `Настройка Windows 11 в пару кликов / Windows 11 tweaker`
   - **Initialize this repository with a README** — **не** отмечай (README уже есть).
   - **Git ignore** и **License** — оставь **None** (файлы уже есть).
   - Нажми **Create repository**.
5. Слева появится список файлов. Внизу слева в поле **Summary** напиши `WinWeaver 1.0.0` и нажми **Commit to main**.

## Шаг 5. Опубликовать

1. Сверху нажми **Publish repository**.
2. **Сними** галочку **Keep this code private** — иначе проект никто не увидит.
3. Нажми **Publish repository**.
4. Через минуту проект появится на https://github.com/fel7xxx/WinWeaver. Кнопка **View on GitHub** откроет его в браузере.

## Шаг 6. Настройки на сайте

Открой репозиторий на сайте → вкладка **Settings** (шестерёнка).

1. **General → Features:**
   - отметь **Discussions** — появится раздел «Обсуждения» (на него ведёт кнопка в программе);
   - **Issues** должны быть отмечены (так по умолчанию) — туда приходят сообщения об ошибках;
   - отметь **Sponsorships** — рядом с названием появится кнопка **♥ Sponsor** со ссылкой на Boosty.
2. **Actions → General → Workflow permissions:** выбери **Read and write permissions** → **Save**. Это нужно, чтобы GitHub сам прикладывал собранный exe к релизу.
3. Вернись на главную страницу репозитория. Справа в блоке **About** нажми шестерёнку:
   - **Description:** `Настройка Windows 11 в пару кликов — приватность, оформление, скорость. Every change can be undone.`
   - **Topics:** `windows-11`, `tweaker`, `privacy`, `debloat`, `optimization`, `winforms`
   - **Save changes**.

### Первое обсуждение (по желанию)
Вкладка **Discussions** → **New discussion** → категория **Announcements** → заголовок «Добро пожаловать / Welcome» → коротко: что это за программа и что вопросы задаются здесь, а ошибки — через «Настройки → Сообщить об ошибке». Потом нажми **Pin discussion**, чтобы закрепить.

## Шаг 7. Первый релиз (exe соберётся сам)

1. На странице репозитория справа найди **Releases → Create a new release**.
2. **Choose a tag** → впиши `v1.0.0` → **Create new tag: v1.0.0 on publish**.
3. **Release title:** `WinWeaver 1.0.0`.
4. В описание скопируй раздел `1.0.0` из файла `CHANGELOG.md`.
5. Нажми **Publish release**.
6. Открой вкладку **Actions** — там запустится «Release». Через 3–5 минут к релизу добавятся файлы **WinWeaver.exe** и **WinWeaver.exe.sha256**.
7. Проверь: на https://github.com/fel7xxx/WinWeaver/releases/latest должен быть exe. Эту ссылку и давай людям.

> Если в Actions красный крестик — открой запуск, посмотри, на каком шаге ошибка. Частая причина: тег не совпадает с версией в `app/src/Properties/AssemblyInfo.cs` (для `v1.0.0` там должно быть `1.0.0.0`). Удали релиз и тег (**Releases → … → Delete**, затем **Tags → Delete**) и создай заново.

## Как выпускать обновления

1. Внеси изменения в файлы.
2. Поменяй версию в `app/src/Properties/AssemblyInfo.cs` (обе строки, например `1.0.1.0`) и допиши раздел в `CHANGELOG.md`.
3. В GitHub Desktop: впиши **Summary** (например, `1.0.1: исправлена …`) → **Commit to main** → **Push origin**.
4. На сайте создай релиз с тегом `v1.0.1` (как в шаге 7).
5. Программы у пользователей раз в день проверяют GitHub и покажут «Доступна версия 1.0.1» — со ссылкой на страницу релиза.

После каждого **Push** вкладка **Actions** запускает «Build» — это проверка, что всё собирается. Готовый exe из такой проверки можно скачать внизу страницы запуска (**Artifacts**).

## Что приходит от пользователей

- **Issues** — ошибки и идеи. Формы уже настроены: версия, Windows, шаги, журнал. Отвечай в комментариях, а когда исправишь — нажми **Close issue**. Метки `bug` и `enhancement` ставятся сами.
- **Discussions** — вопросы и разговоры.
- **Security → Advisories** — приватные сообщения об уязвимостях.

Включи уведомления: кнопка **Watch → All Activity** на странице репозитория — письма будут приходить на почту.
