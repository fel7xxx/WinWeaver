using System;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using AT = Avalonia.Threading;

namespace WinWeaver;

/// <summary>Приложение Avalonia без XAML: тёмная Fluent-тема (нужна для TextBox и меню).</summary>
internal sealed class AvaloniaApp : Avalonia.Application
{
	public override void Initialize()
	{
		Styles.Add(new FluentTheme());
		RequestedThemeVariant = ThemeVariant.Dark;
	}

	private static bool _started;

	/// <summary>Запуск платформы Avalonia (окна, рендер, ввод) без собственного главного цикла:
	/// цикл крутит Application.Run / ShowDialog через DispatcherFrame, как в WinForms.</summary>
	internal static void Start()
	{
		if (_started)
		{
			return;
		}
		_started = true;
		AppBuilder.Configure<AvaloniaApp>()
			.UsePlatformDetect()
			.With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Software } })
			.LogToTrace()
			.SetupWithoutStarting();
		AT.Dispatcher.UIThread.UnhandledException += (s, e) =>
		{
			e.Handled = true;
			Application.OnThreadException(e.Exception);
		};
	}

	/// <summary>Для headless-режима (скриншоты на CI/Linux без дисплея).</summary>
	internal static AppBuilder Builder() => AppBuilder.Configure<AvaloniaApp>();
}
