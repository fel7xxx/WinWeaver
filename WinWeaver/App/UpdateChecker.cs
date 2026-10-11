using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace WinWeaver;

internal static class UpdateChecker
{
	public class Result
	{
		public string Tag;

		public string Url;

		public bool Newer;

		public string Error;
	}

	public static Version Parse(string tag)
	{
		Match match = Regex.Match((tag ?? "").Trim(), @"\A[vV]?(\d+\.\d+(?:\.\d+){0,2})\z");
		if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version))
		{
			return new Version(0, 0);
		}
		return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
	}

	public static Result Check()
	{
		Result result = new Result();
		string environmentVariable = Environment.GetEnvironmentVariable("WW_FAKE_UPDATE");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			result.Tag = environmentVariable;
			result.Url = Brand.Releases;
			result.Newer = Parse(environmentVariable) > Parse(Brand.Version);
			return result;
		}
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			HttpWebRequest obj = (HttpWebRequest)WebRequest.Create(Brand.LatestApi);
			obj.UserAgent = Brand.Name + "/" + Brand.Version;
			obj.Accept = "application/vnd.github+json";
			obj.Timeout = 8000;
			obj.ReadWriteTimeout = 8000;
			using WebResponse webResponse = obj.GetResponse();
			using StreamReader streamReader = new StreamReader(webResponse.GetResponseStream());
			string input = streamReader.ReadToEnd();
			Match match = Regex.Match(input, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
			Match match2 = Regex.Match(input, "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+/releases/tag/[^\"]+)\"");
			if (!match.Success)
			{
				result.Error = "no tag";
				return result;
			}
			result.Tag = match.Groups[1].Value;
			result.Url = ((match2.Success && match2.Groups[1].Value.StartsWith("https://github.com/fel7xxx/WinWeaver/", StringComparison.OrdinalIgnoreCase)) ? match2.Groups[1].Value : "https://github.com/fel7xxx/WinWeaver/releases");
			if (Parse(result.Tag) == new Version(0, 0))
			{
				result.Error = "Unsupported release version: " + result.Tag;
				return result;
			}
			result.Newer = Parse(result.Tag) > Parse(Brand.Version);
		}
		catch (Exception ex)
		{
			result.Error = ex.Message;
		}
		return result;
	}

	public static void CheckAsync(Action<Result> done)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			Result obj = Check();
			try
			{
				done(obj);
			}
			catch
			{
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}

	public static bool Due()
	{
		if (!Prefs.UpdateCheck)
		{
			return false;
		}
		if (DateTime.TryParse(Prefs.LastUpdateCheck, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result) && result.ToUniversalTime() <= DateTime.UtcNow && (DateTime.UtcNow - result.ToUniversalTime()).TotalHours < 24.0)
		{
			return Environment.GetEnvironmentVariable("WW_FAKE_UPDATE") != null;
		}
		return true;
	}
}
