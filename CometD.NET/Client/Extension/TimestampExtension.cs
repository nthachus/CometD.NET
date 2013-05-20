using System;
using System.Collections.Generic;
using System.Globalization;

using CometD.Bayeux;
using CometD.Bayeux.Client;
using CometD.Common;

namespace CometD.Client.Extension
{
	/// <summary>
	/// This client extension will add the client timestamp into each messages
	/// that will be sent to the server.
	/// </summary>
	[Serializable]
	public sealed class TimestampExtension : AdapterExtension, IEquatable<TimestampExtension>
	{
		#region IExtension Members

		/// <summary>
		/// Callback method invoked every time a normal message is being sent.
		/// </summary>
		/// <returns>Always true.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
		public override bool Send(IClientSession session, IMutableMessage message)
		{
			if (message == null)
				throw new ArgumentNullException("message");

			AddTimestamp(message);
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

			AddTimestamp(message);
			return true;
		}

		#endregion

		private static void AddTimestamp(IMutableMessage message)
		{
			lock (message)
			{
				// RFC 1123 DateTime Format "EEE, dd MMM yyyy HH:mm:ss 'GMT'"
				message[Message.TimestampField]
					= DateTime.Now.ToString("r", CultureInfo.GetCultureInfo(1033));// en-US
			}
		}

		#region IEquatable Members

		/// <summary>
		/// Returns a value indicating whether this extension
		/// is equal to another <see cref="IExtension"/> object.
		/// </summary>
		public override bool Equals(IExtension other)
		{
			return (null == other) ? false : (other is TimestampExtension);
		}

		/// <summary>
		/// Returns a value indicating whether this extension
		/// is equal to another <see cref="TimestampExtension"/> object.
		/// </summary>
		public bool Equals(TimestampExtension other)
		{
			return (null != other);
		}

		/// <summary>
		/// Returns a value indicating whether this extension is equal to a specified object.
		/// </summary>
		public override bool Equals(object obj)
		{
			return (null == obj) ? false : (obj is TimestampExtension);
		}

		/// <summary>
		/// Returns the hash code for this extension.
		/// </summary>
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		#endregion
	}
}
