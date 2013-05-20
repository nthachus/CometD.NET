using System;
using System.Collections.Generic;
using System.Globalization;

using CometD.Bayeux;
using CometD.Bayeux.Client;
using CometD.Common;

namespace CometD.Client.Extension
{
	/// <summary>
	/// This client extension allows the client to synchronize message's timestamp
	/// with the server.
	/// </summary>
	[Serializable]
	public sealed class TimeSyncExtension : AdapterExtension, IEquatable<TimeSyncExtension>
	{
		/// <summary>
		/// The message "timesync" field.
		/// </summary>
		public const string ExtensionField = "timesync";

		private volatile int _lag;
		private volatile int _offset;

		#region IExtension Members

		/// <summary>
		/// Callback method invoked every time a meta message is received.
		/// </summary>
		/// <returns>Always true.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		public override bool ReceiveMeta(IClientSession session, IMutableMessage message)
		{
			if (message == null)
				throw new ArgumentNullException("message");

			IDictionary<string, object> ext = message.GetExtension(false);
			object val;
			if (ext != null && ext.TryGetValue(ExtensionField, out val))
			{
				IDictionary<string, object> sync
					= ObjectConverter.ToObject<IDictionary<string, object>>(val);

				if (sync != null// && sync.ContainsKey("a")
					&& sync.ContainsKey("tc") && sync.ContainsKey("ts") && sync.ContainsKey("p"))
				{
					long now = CurrentTimeMillis();

					long tc = ObjectConverter.ToPrimitive<long>(sync["tc"], 0);
					long ts = ObjectConverter.ToPrimitive<long>(sync["ts"], 0);
					int p = ObjectConverter.ToPrimitive<int>(sync["p"], 0);
					//int a = ObjectConverter.ToPrimitive<int>(sync["a"], 0);

					int l2 = (int)((now - tc - p) / 2);
					int o2 = (int)(ts - tc - l2);

					_lag = (_lag == 0) ? l2 : ((_lag + l2) / 2);
					_offset = (_offset == 0) ? o2 : ((_offset + o2) / 2);
				}
			}

			return true;
		}

		/// <summary>
		/// Callback method invoked every time a meta message is being sent.
		/// </summary>
		/// <returns>Always true.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		public override bool SendMeta(IClientSession session, IMutableMessage message)
		{
			if (message == null)
				throw new ArgumentNullException("message");

			long now = CurrentTimeMillis();
			IDictionary<string, object> timesync
				= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
				{
					{ "tc", now },
					{ "l", _lag },
					{ "o", _offset }
				};

			IDictionary<string, object> ext = message.GetExtension(true);
			lock (ext) ext[ExtensionField] = timesync;

			return true;
		}

		#endregion

		/// <summary>
		/// The server timezone offset in milliseconds.
		/// </summary>
		public int Offset
		{
			get { return _offset; }
		}

		/// <summary>
		/// The local timezone lag in milliseconds.
		/// </summary>
		public int Lag
		{
			get { return _lag; }
		}

		/// <summary>
		/// Returns the current UNIX timestamp of the server in milliseconds.
		/// </summary>
		public long ServerTime
		{
			get { return (CurrentTimeMillis() + _offset); }
		}

		/// <summary>
		/// Returns the current UNIX timestamp in milliseconds.
		/// </summary>
		public static long CurrentTimeMillis()
		{
			return (DateTime.UtcNow.Ticks / 10000 - 62135596800000);
		}

		#region IEquatable Members

		/// <summary>
		/// Returns a value indicating whether this extension
		/// is equal to another <see cref="IExtension"/> object.
		/// </summary>
		public override bool Equals(IExtension other)
		{
			return (null == other) ? false : (other is TimeSyncExtension);
		}

		/// <summary>
		/// Returns a value indicating whether this extension
		/// is equal to another <see cref="TimeSyncExtension"/> object.
		/// </summary>
		public bool Equals(TimeSyncExtension other)
		{
			return (null != other
				&& _lag == other._lag && _offset == other._offset);
		}

		/// <summary>
		/// Returns a value indicating whether this extension is equal to a specified object.
		/// </summary>
		public override bool Equals(object obj)
		{
			return (null == obj) ? false : (obj is TimeSyncExtension);
		}

		/// <summary>
		/// Returns the hash code for this extension.
		/// </summary>
		public override int GetHashCode()
		{
			return (((long)_lag << 32) | unchecked((uint)_offset)).GetHashCode();
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture,
				"{{ Lag: {0}, Offset: {1} }}", _lag, _offset);
		}

		#endregion
	}
}
