using System;
using System.Collections.Generic;

namespace WinWeaver;

internal sealed class SettingMeta
{
	public int Impact = -1;

	public int Risk = -1;

	public int Reboot = -1;

	public string Tech;

	public string[] Writes;

	private static Dictionary<string, SettingMeta> _all;

	public static readonly string[] ImpactNames = new string[6] { "не влияет", "мало", "немного", "заметно", "сильно", "максимум" };

	public static readonly string[] RiskNames = new string[4] { "нет", "низкий", "средний", "высокий" };

	public static readonly string[] RebootNames = new string[3] { "не нужна", "выход", "нужна" };

	public static readonly int[] RiskDots = new int[4] { 0, 2, 3, 5 };

	public static readonly int[] RebootDots = new int[3] { 0, 2, 5 };

	private const string HKP = "HKLM:\\SOFTWARE\\Policies\\Microsoft";

	public static string ImpactLabel(string cat)
	{
		switch (cat)
		{
		case "privacy":
			return "Приватность";
		case "game":
		case "perf":
			return "Скорость";
		case "net":
			return "Сеть";
		case "update":
			return "Контроль";
		case "defender":
		case "security":
		case "protect":
			return "Защита";
		case "ui":
			return "Удобство";
		case "clean":
			return "Место";
		case "restore":
			return "Надёжность";
		default:
			return "Удобство";
		}
	}

	public static SettingMeta For(string title)
	{
		if (_all == null)
		{
			Build();
		}
		if (title == null || !_all.TryGetValue(title, out var value))
		{
			return null;
		}
		return value;
	}

	private static void Add(string title, int impact, int risk, int reboot, string tech = null, params string[] writes)
	{
		_all[title] = new SettingMeta
		{
			Impact = impact,
			Risk = risk,
			Reboot = reboot,
			Tech = tech,
			Writes = ((writes != null && writes.Length != 0) ? writes : null)
		};
	}

	private static void Build()
	{
		_all = new Dictionary<string, SettingMeta>(StringComparer.Ordinal);
		Add("Отключить телеметрию", 5, 0, 0, "Ставит политику AllowTelemetry=0 (уровень «Безопасность»), выключает CEIP, инвентаризацию и отчёты об ошибках (WER), рекламный идентификатор и журнал действий. Службы DiagTrack и dmwappushservice останавливаются и отключаются.", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection|AllowTelemetry|0", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection|AllowDeviceNameInTelemetry|0", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows|CEIPEnable|0", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppCompat|AITEnable|0", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting|Disabled|1", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo|DisabledByGroupPolicy|1");
		Add("Отключить Copilot и Recall", 5, 0, 1, "Политики TurnOffWindowsCopilot (для компьютера и пользователя), DisableAIDataAnalysis и AllowRecallEnablement=0, кнопка Copilot убирается с панели задач (ShowCopilotButton=0). Пакет Copilot удаляется, компонент Recall выключается — для него нужна перезагрузка.", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot|TurnOffWindowsCopilot|1", "HKCU:\\Software\\Policies\\Microsoft\\Windows\\WindowsCopilot|TurnOffWindowsCopilot|1", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI|DisableAIDataAnalysis|1", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI|AllowRecallEnablement|0");
		Add("Отключить Bing в поиске", 3, 0, 1, "DisableSearchBoxSuggestions=1 и BingSearchEnabled=0 для пользователя, политики DisableWebSearch=1 и ConnectedSearchUseWeb=0. Проводник перезапускается, чтобы поиск подхватил изменения.", "HKCU:\\Software\\Policies\\Microsoft\\Windows\\Explorer|DisableSearchBoxSuggestions|1", "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search|BingSearchEnabled|0", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search|DisableWebSearch|1", "HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search|ConnectedSearchUseWeb|0");
		Add("Отключить определение местоположения", 3, 0, 0, null);
		Add("Отключить персональные советы", 4, 0, 0, null);
		Add("Отключить онлайн-распознавание речи", 3, 0, 0, null);
		Add("Отключить сбор рукописного и набранного текста", 3, 0, 0, null);
		Add("Отключить журнал действий", 3, 0, 0, null);
		Add("Отключить запросы отзывов", 1, 0, 0, null);
		Add("Отключить рекламу и рекомендации", 3, 0, 1, "Выключает значения ContentDeliveryManager (SubscribedContent, SilentInstalledApps, PreInstalledApps и другие), рекомендации в Пуске (Start_IrisRecommendations), уведомления OneDrive в Проводнике и экран «Давайте завершим настройку». Политики CloudContent запрещают тихую установку спонсорских приложений.");
		Add("Отключить советы на экране блокировки", 1, 0, 0, null);
		Add("Отключить «Главное в поиске»", 1, 0, 1, null);
		Add("Отключить «Давайте завершим настройку»", 1, 0, 0, null);
		Add("Отключить рекламу в Проводнике", 1, 0, 1, null);
		Add("Остаться на текущей версии Windows", 4, 1, 0, "Политики TargetReleaseVersion и TargetReleaseVersionInfo фиксируют текущий выпуск (например 25H2). Ежемесячные обновления безопасности продолжают приходить, переход на следующий выпуск — только когда ты сам снимешь галочку.");
		Add("Не раздавать обновления другим ПК", 2, 0, 0, null);
		Add("Не перезагружать ПК, пока я в системе", 3, 0, 0, null);
		Add("Обновлять приложения Store только вручную", 2, 1, 0, null);
		Add("Тёмная тема", 3, 0, 0, null);
		Add("Прозрачность", 1, 0, 0, null);
		Add("Классическое контекстное меню", 4, 0, 1, null);
		Add("Макеты при наведении на «Развернуть»", 1, 0, 0, null);
		Add("Панель задач слева", 2, 0, 0, null);
		Add("Убрать виджеты", 2, 0, 1, null);
		Add("Скрыть кнопку Chat (Teams)", 1, 0, 1, null);
		Add("Кнопка «Представление задач»", 1, 0, 1, null);
		Add("«Завершить задачу» в меню панели задач", 3, 0, 0, null);
		Add("Секунды в часах", 1, 0, 1, null);
		Add("«Свернуть все окна» в правом углу", 1, 0, 1, null);
		Add("Убрать рекомендации в Пуске", 3, 0, 1, null);
		Add("Скрыть недавние файлы в Пуске", 2, 0, 1, null);
		Add("Больше закреплённых в Пуске", 2, 0, 1, null);
		Add("Показывать расширения файлов", 4, 0, 0, null);
		Add("Показывать скрытые файлы", 2, 1, 0, null);
		Add("Открывать «Этот компьютер»", 2, 0, 0, null);
		Add("Компактный вид Проводника", 1, 0, 0, null);
		Add("Флажки для выбора файлов", 1, 0, 0, null);
		Add("Не показывать недавнее в быстром доступе", 2, 0, 0, null);
		Add("Значок «Этот компьютер» на рабочем столе", 1, 0, 0, null);
		Add("Без задержки автозапуска", 2, 0, 0, null);
		Add("Быстрые меню", 1, 0, 0, null);
		Add("Запретить фоновые приложения", 2, 1, 0, null);
		Add("Отключить Power Throttling", 2, 1, 2, null);
		Add("Игровой режим", 3, 0, 0, null);
		Add("Фоновая запись игр (Game DVR)", 3, 0, 0, null);
		Add("Ускорение мыши", 3, 0, 0, null);
		Add("Аппаратное планирование GPU (HAGS)", 3, 1, 2, null);
		Add("Оптимизация оконных игр", 3, 0, 0, null);
		Add("Отключить оптимизацию во весь экран", 2, 1, 0, null);
		Add("Отключить запрос залипания клавиш", 1, 0, 0, null);
		Add("Game Bar с кнопки Xbox на геймпаде", 1, 0, 0, null);
		Add("Целостность памяти (HVCI)", 4, 3, 2, null);
		Add("Быстрый запуск и гибернация", 2, 1, 2, null);
		Add("Отключить SysMain (Superfetch)", 2, 1, 0, null);
		Add("Отключить индексирование поиска", 2, 2, 0, null);
		Add("Создавать точки без лимита", 2, 0, 0, null);
		Add("Отключить алгоритм Нейгла", 3, 0, 2, null);
		Add("Снять ограничение сети для мультимедиа", 2, 0, 2, null);
		Add("Распределять сеть по ядрам (RSS)", 2, 0, 0, null);
		Add("ECN — уведомление о перегрузке", 1, 1, 0, null);
		Add("Резерв канала QoS — 0%", 1, 0, 2, null);
		Add("Не усыплять сетевую карту", 2, 0, 0, null);
		Add("IPv4 в приоритете над IPv6", 1, 1, 2, null);
		Add("Шифрованный DNS (DoH)", 4, 1, 0, null);
		Add("Отключить LLMNR", 2, 1, 0, null);
		Add("Отключить NetBIOS через TCP/IP", 2, 1, 0, null);
		Add("Не подключаться к открытым Wi-Fi сами", 3, 0, 0, null);
		Add("Отключить Teredo", 1, 1, 0, null);
		Add("SmartScreen для приложений и файлов", 4, 0, 0, null);
		Add("Блокировать нежелательные программы (PUA)", 3, 0, 0, null);
		Add("Отключить автозапуск со съёмных дисков", 3, 0, 0, null);
		Add("Отключить удалённого помощника", 2, 0, 0, null);
		Add("Отключить устаревший протокол SMBv1", 4, 1, 2, null);
		Add("Не отправлять образцы файлов в Microsoft", 3, 1, 0, null);
		Add("Контроль учётных записей (UAC)", 4, 2, 2, null);
	}
}
