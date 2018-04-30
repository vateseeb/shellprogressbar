using System;
using System.Collections;
using System.Threading;

namespace ShellProgressBar
{
	internal static class IdleMonitor
	{
		private static Timer idleTimer;
		private static TimeSpan timeout;
		private static Action<double, string> callback;
		private static double currentPercentage;
		private static string currentMessage;

		internal static void Start(Action<double, string> callback, TimeSpan timeout)
		{
			IdleMonitor.timeout = timeout;
			IdleMonitor.callback = callback;
			idleTimer = new Timer(state =>
			{
				IdleMonitor.callback?.Invoke(currentPercentage, currentMessage);
			}, null, timeout, new TimeSpan(-1));
		}

		internal static void Reset(double percentage, string message)
		{
			currentPercentage = percentage;
			currentMessage = message;

			if (idleTimer == null) return;
			if (!idleTimer.Change(timeout, new TimeSpan(-1)))
				callback?.Invoke(currentPercentage, "Reset of timer failed");
		}

		internal static void Stop()
		{
			idleTimer?.Dispose();
			idleTimer = null;
		}
	}
}
