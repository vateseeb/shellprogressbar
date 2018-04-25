using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;

namespace ShellProgressBar
{
	public abstract class ProgressBarBase
	{
		static ProgressBarBase()
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		private static readonly object CheckLock = new object();
		private static readonly object TimerLock = new object();
		protected readonly DateTime _startDate = DateTime.Now;
		private int _maxTicks;
		private int _currentTick;
		private string _message;
		private Timer _idleTimer;
		private bool _progressStopped;
		protected Action _progressStoppedAction;

		protected ProgressBarBase(int maxTicks, string message, ProgressBarOptions options)
		{
			this._maxTicks = Math.Max(0, maxTicks);
			this._message = message;
			this.Options = options ?? ProgressBarOptions.Default;
			this._idleTimer = new Timer(CheckProgress, null, (int)Options.IdleTimeout.TotalMilliseconds, (int)Options.IdleTimeout.TotalMilliseconds);
		}

		internal ProgressBarOptions Options { get; }
		internal ConcurrentBag<ChildProgressBar> Children { get; } = new ConcurrentBag<ChildProgressBar>();

		protected abstract void DisplayProgress();

		protected virtual void Grow(ProgressBarHeight direction)
		{
		}

		protected virtual void OnDone()
		{
		}

		public DateTime? EndTime { get; protected set; }

		public ConsoleColor ForeGroundColor =>
			EndTime.HasValue ? this.Options.ForegroundColorDone ?? this.Options.ForegroundColor : this.Options.ForegroundColor;

		public int CurrentTick => _currentTick;

		public int MaxTicks
		{
			get => _maxTicks;
			set
			{
				Interlocked.Exchange(ref _maxTicks, value);
				DisplayProgress();
			}
		}

		public string Message
		{
			get => _message;
			set
			{
				Interlocked.Exchange(ref _message, value);
				DisplayProgress();
			}
		}

		public double Percentage
		{
			get
			{
				var percentage = Math.Max(0, Math.Min(100, (100.0 / this._maxTicks) * this._currentTick));
				// Gracefully handle if the percentage is NaN due to division by 0
				if (double.IsNaN(percentage) || percentage < 0) percentage = 100;
				return percentage;
			}
		}

		public bool Collapse => this.EndTime.HasValue && this.Options.CollapseWhenFinished;

		public ChildProgressBar Spawn(int maxTicks, string message, ProgressBarOptions options = null)
		{
			var pbar = new ChildProgressBar(maxTicks, message, DisplayProgress, options ?? Options, this.Grow)
			{
				_progressStoppedAction = this._progressStoppedAction
			};

			lock (TimerLock)
			{
				this._idleTimer?.Dispose();
				this._idleTimer = null;
				pbar.Done += ChildDone;
				this.Children.Add(pbar);
			}

			DisplayProgress();
			return pbar;
		}

		public void Tick(string message = null)
		{
			FinishTick(message);
		}

		public void Tick(int newTickCount, string message = null)
		{
			Interlocked.Exchange(ref _currentTick, newTickCount);

			FinishTick(message);
		}

		internal void CheckProgress(object state)
		{
			lock (CheckLock)
			{
				if (this._progressStopped)
					return;

				this._progressStopped = true;
				this._progressStoppedAction?.Invoke();
			}
		}

		private void ChildDone(object sender, EventArgs e)
		{
			var child = (ChildProgressBar)sender;
			child.Done -= ChildDone;

			lock (TimerLock)
			{
				if (_currentTick < MaxTicks && Children.All(c => c.CurrentTick >= c.MaxTicks))
					this._idleTimer = new Timer(CheckProgress, null, (int)Options.IdleTimeout.TotalMilliseconds, (int)Options.IdleTimeout.TotalMilliseconds);
			}
		}

		private void FinishTick(string message)
		{
			this._progressStopped = false;
			this._idleTimer?.Change((int)Options.IdleTimeout.TotalMilliseconds, (int)Options.IdleTimeout.TotalMilliseconds);
			Interlocked.Increment(ref _currentTick);
			if (message != null)
				Interlocked.Exchange(ref _message, message);

			if (_currentTick >= _maxTicks)
			{
				this.EndTime = DateTime.Now;
				this._idleTimer?.Dispose();
				this._idleTimer = null;
				this.OnDone();
			}
			DisplayProgress();
		}
	}
}
