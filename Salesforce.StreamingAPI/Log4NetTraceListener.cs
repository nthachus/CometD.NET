using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

using log4net;

namespace Salesforce.StreamingAPI
{
	/// <summary>
	/// Monitor trace and debug output into log4net adapter.
	/// </summary>
	public sealed class Log4NetTraceListener : TraceListener
	{
		/// <summary>
		/// Emits an error message to the log4net listener.
		/// </summary>
		/// <param name="message">A message to emit.</param>
		public override void Fail(string message)
		{
			Fail(message, String.Empty);
		}

		/// <summary>
		/// Emits an error message to the log4net listener.
		/// </summary>
		/// <param name="message">A message to emit.</param>
		/// <param name="detailMessage">A detailed message to emit.</param>
		public override void Fail(string message, string detailMessage)
		{
			StackTrace stack = new StackTrace();
			StackFrame frame = GetTracingStackFrame(stack);

			ILog log = LogManager.GetLogger(frame.GetMethod().DeclaringType);
			if (!log.IsWarnEnabled) return;

			using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
			{
				message = String.IsNullOrEmpty(detailMessage)
					? message : String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", message, Environment.NewLine, detailMessage);
				log.WarnFormat("[Fail] {0}", message);
			}
		}

		/// <summary>
		/// Writes trace information, a data object and event information to the log4net listener specific output.
		/// </summary>
		public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
		{
			object[] array = new[] { data };
			TraceData(eventCache, source, eventType, id, array);
		}

		/// <summary>
		/// Writes trace information, a data object and event information to the log4net listener specific output.
		/// </summary>
		public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
		{
			if (null == data || 0 == data.Length)
			{
				TraceEvent(eventCache, source, eventType, id);
				return;
			}

#if NET20
            foreach (var datum in data)
            {
                if (TraceException(eventType, datum)) continue;

                object[] array = new[] { datum };
                TraceEvent(eventCache, source, eventType, id, "{0}", array);
            }
#else
			foreach (var datum in data.Where(x => !TraceException(eventType, x)))
			{
				object[] array = new[] { datum };
				TraceEvent(eventCache, source, eventType, id, "{0}", array);
			}
#endif
		}

		/// <summary>
		/// Writes trace and event information to the log4net listener specific output.
		/// </summary>
		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
		{
			StackFrame frame = GetTracingStackFrame(new StackTrace());
			ILog log = LogManager.GetLogger(frame.GetMethod().DeclaringType);
			switch (eventType)
			{
				case TraceEventType.Critical:
					if (!log.IsFatalEnabled) return;

					using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
					{
						log.Fatal(String.Empty);
					}

					break;

				case TraceEventType.Error:
					if (!log.IsFatalEnabled) return;

					using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
					{
						log.Error(String.Empty);
					}

					break;

				case TraceEventType.Information:
					if (!log.IsInfoEnabled) return;

					using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
					{
						log.Info(String.Empty);
					}

					break;

				case TraceEventType.Resume:
				case TraceEventType.Start:
				case TraceEventType.Stop:
				case TraceEventType.Suspend:
				case TraceEventType.Transfer:
				case TraceEventType.Verbose:
					if (!log.IsDebugEnabled) return;

					using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
					{
						log.Debug(String.Empty);
					}

					break;

				case TraceEventType.Warning:
					if (!log.IsWarnEnabled) return;

					using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
					{
						log.Warn(String.Empty);
					}

					break;
			}
		}

		// ReSharper disable MethodOverloadWithOptionalParameter
		/// <summary>
		/// Writes trace and event information to the log4net listener specific output.
		/// </summary>
		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
		{
			if (null == args || 0 == args.Length)
			{
				TraceEvent(eventCache, source, eventType, id);
			}

			StackFrame frame = GetTracingStackFrame(new StackTrace());
			ILog log = LogManager.GetLogger(frame.GetMethod().DeclaringType);
			using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
			{
				switch (eventType)
				{
					case TraceEventType.Critical:
						if (!log.IsFatalEnabled) return;

						log.FatalFormat(CultureInfo.CurrentCulture, format, args);
						break;

					case TraceEventType.Error:
						if (!log.IsFatalEnabled) return;

						log.ErrorFormat(CultureInfo.CurrentCulture, format, args);
						break;

					case TraceEventType.Information:
						if (!log.IsInfoEnabled) return;

						log.InfoFormat(CultureInfo.CurrentCulture, format, args);
						break;

					case TraceEventType.Resume:
					case TraceEventType.Start:
					case TraceEventType.Stop:
					case TraceEventType.Suspend:
					case TraceEventType.Transfer:
					case TraceEventType.Verbose:
						if (!log.IsDebugEnabled) return;

						log.DebugFormat(CultureInfo.CurrentCulture, format, args);
						break;

					case TraceEventType.Warning:
						if (!log.IsWarnEnabled) return;

						log.WarnFormat(CultureInfo.CurrentCulture, format, args);
						break;
				}
			}
		}
		// ReSharper restore MethodOverloadWithOptionalParameter

		/// <summary>
		/// Writes trace and event information to the log4net listener specific output.
		/// </summary>
		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
		{
			object[] array = new[] { message };
			// ReSharper disable CoVariantArrayConversion
			TraceEvent(eventCache, source, eventType, id, "{0}", array);
			// ReSharper restore CoVariantArrayConversion
		}

		/// <summary>
		/// Writes trace information, a message, a related activity identity and event
		/// information to the log4net listener specific output.
		/// </summary>
		public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message, Guid relatedActivityId)
		{
			object[] array = new object[] { message, relatedActivityId };
			TraceEvent(eventCache, source, TraceEventType.Transfer, id, "{0}", array);
		}

		/// <summary>
		/// Writes the value of the object's <see cref="Object.ToString()"/> method to the log4net listener.
		/// </summary>
		/// <param name="o">An <see cref="Object"/> whose fully qualified class name you want to write.</param>
		public override void Write(object o)
		{
			WriteLine(o, String.Empty);
		}

		/// <summary>
		/// Writes a category name and the value of the object's <see cref="Object.ToString()"/> method to the log4net listener.
		/// </summary>
		/// <param name="o">An <see cref="Object"/> whose fully qualified class name you want to write.</param>
		/// <param name="category">A category name used to organize the output.</param>
		public override void Write(object o, string category)
		{
			WriteLine(o, category);
		}

		/// <summary>
		/// Writes the specified message to the log4net listener.
		/// </summary>
		/// <param name="message">A message to write.</param>
		public override void Write(string message)
		{
			WriteLine(message, String.Empty);
		}

		/// <summary>
		/// Writes a category name and a message to the log4net listener.
		/// </summary>
		/// <param name="message">A message to write.</param>
		/// <param name="category">A category name used to organize the output.</param>
		public override void Write(string message, string category)
		{
			WriteLine((object)message, category);
		}

		/// <summary>
		/// Writes the value of the object's <see cref="Object.ToString()"/> method to the log4net listener,
		/// followed by a line terminator.
		/// </summary>
		/// <param name="o">An <see cref="Object"/> whose fully qualified class name you want to write.</param>
		public override void WriteLine(object o)
		{
			WriteLine(o, String.Empty);
		}

		/// <summary>
		/// Writes a category name and the value of the object's <see cref="Object.ToString()"/>
		/// method to the log4net listener, followed by a line terminator.
		/// </summary>
		/// <param name="o">An <see cref="Object"/> whose fully qualified class name you want to write.</param>
		/// <param name="category">A category name used to organize the output.</param>
		public override void WriteLine(object o, string category)
		{
			StackTrace stack = new StackTrace();
			StackFrame frame = GetTracingStackFrame(stack);

			ILog log = LogManager.GetLogger(frame.GetMethod().DeclaringType);
			if (!log.IsInfoEnabled) return;

			using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
			{
				if (String.IsNullOrEmpty(category))
				{
					log.Debug(o);
				}
				else
				{
					log.DebugFormat("[{0}] {1}", category, o);
				}
			}
		}

		/// <summary>
		/// Writes a category name and a message to the log4net listener, followed by a line terminator.
		/// </summary>
		/// <param name="message">A message to write.</param>
		/// <param name="category">A category name used to organize the output.</param>
		public override void WriteLine(string message, string category)
		{
			WriteLine((object)message, category);
		}

		/// <summary>
		/// Writes a message to the log4net listener, followed by a line terminator.
		/// </summary>
		/// <param name="message">A message to write.</param>
		public override void WriteLine(string message)
		{
			WriteLine(message, String.Empty);
		}

		private static StackFrame GetTracingStackFrame(StackTrace stack)
		{
			for (var i = 0; i < stack.FrameCount; i++)
			{
				StackFrame frame = stack.GetFrame(i);
				System.Reflection.MethodBase method = frame.GetMethod();
				if (null == method) continue;

				// ReSharper disable PossibleNullReferenceException
				if ("System.Diagnostics".Equals(method.DeclaringType.Namespace, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				// ReSharper restore PossibleNullReferenceException

				if ("System.Threading".Equals(method.DeclaringType.Namespace, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if ("Log4NetTraceListener".Equals(method.DeclaringType.Name, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				return stack.GetFrame(i);
			}

			return null;
		}

		private static bool TraceException(TraceEventType eventType, object datum)
		{
			if (TraceEventType.Critical != eventType && TraceEventType.Error != eventType)
			{
				return false;
			}

			Exception exception = datum as Exception;
			if (null == exception) return false;

			StackFrame frame = GetTracingStackFrame(new StackTrace());
			ILog log = LogManager.GetLogger(frame.GetMethod().DeclaringType);
			switch (eventType)
			{
				case TraceEventType.Critical:
					if (log.IsFatalEnabled)
					{
						using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
						{
							log.Fatal(exception.Message, exception);
						}
					}

					break;

				case TraceEventType.Error:
					if (log.IsErrorEnabled)
					{
						using (ThreadContext.Stacks["signature"].Push(frame.GetMethod().Name))
						{
							log.Error(exception.Message, exception);
						}
					}

					break;
			}

			return true;
		}
	}
}
