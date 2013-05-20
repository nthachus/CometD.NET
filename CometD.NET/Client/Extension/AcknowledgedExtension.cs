using System;
using System.Collections.Generic;
using System.Globalization;

using CometD.Bayeux;
using CometD.Bayeux.Client;
using CometD.Common;

namespace CometD.Client.Extension
{
	/// <summary>
	/// This client-side extension enables the client to acknowledge to the server
	/// the messages that the client has received.
	/// For the acknowledgement to work, the server must be configured with the
	/// correspondent server-side ack extension. If both client and server support
	/// the ack extension, then the ack functionality will take place automatically.
	/// By enabling this extension, all messages arriving from the server will arrive
	/// via the long poll, so the comet communication will be slightly chattier.
	/// The fact that all messages will return via long poll means also that the
	/// messages will arrive with total order, which is not guaranteed if messages
	/// can arrive via both long poll and normal response.
	/// Messages are not acknowledged one by one, but instead a group of messages is
	/// acknowledged when long poll returns.
	/// </summary>
	[Serializable]
	public sealed class AcknowledgedExtension : AdapterExtension, IEquatable<AcknowledgedExtension>
	{
		/// <summary>
		/// The message "ack" field.
		/// </summary>
		public const string ExtensionField = "ack";

		private volatile bool _serverSupportsAcks = false;
		private volatile int _ackId = -1;

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

			string channel = message.Channel;
			if (Channel.MetaHandshake.Equals(channel, StringComparison.OrdinalIgnoreCase))
			{
				IDictionary<string, object> ext = message.GetExtension(false);

				object val;
				_serverSupportsAcks = (ext != null
					&& ext.TryGetValue(ExtensionField, out val)
					&& ObjectConverter.ToPrimitive<bool>(val, false));
			}
			else if (_serverSupportsAcks
				&& message.IsSuccessful
				&& Channel.MetaConnect.Equals(channel, StringComparison.OrdinalIgnoreCase))
			{
				IDictionary<string, object> ext = message.GetExtension(false);

				object val;
				if (ext != null && ext.TryGetValue(ExtensionField, out val))
				{
					_ackId = ObjectConverter.ToPrimitive<int>(val, _ackId);
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

			string channel = message.Channel;
			if (Channel.MetaHandshake.Equals(channel, StringComparison.OrdinalIgnoreCase))
			{
				IDictionary<string, object> ext = message.GetExtension(true);
				lock (ext) ext[ExtensionField] = true;

				_ackId = -1;
			}
			else if (_serverSupportsAcks
				&& Channel.MetaConnect.Equals(channel, StringComparison.OrdinalIgnoreCase))
			{
				IDictionary<string, object> ext = message.GetExtension(true);
				lock (ext) ext[ExtensionField] = _ackId;
			}

			return true;
		}

		#endregion

		#region IEquatable Members

		/// <summary>
		/// Returns a value indicating whether this extension
		/// is equal to another <see cref="IExtension"/> object.
		/// </summary>
		public override bool Equals(IExtension other)
		{
			return (null == other) ? false : (other is AcknowledgedExtension);
		}

		/// <summary>
		/// Returns a value indicating whether this extension
		/// is equal to another <see cref="AcknowledgedExtension"/> object.
		/// </summary>
		public bool Equals(AcknowledgedExtension other)
		{
			return (null != other
				&& _serverSupportsAcks == other._serverSupportsAcks
				&& _ackId == other._ackId);
		}

		/// <summary>
		/// Returns a value indicating whether this extension is equal to a specified object.
		/// </summary>
		public override bool Equals(object obj)
		{
			return (null == obj) ? false : (obj is AcknowledgedExtension);
		}

		/// <summary>
		/// Returns the hash code for this extension.
		/// </summary>
		public override int GetHashCode()
		{
			return ((_ackId.GetHashCode() << 1) | (_serverSupportsAcks ? 1 : 0));
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture,
				"{{ ServerSupportsAcks: {0}, AckId: {1} }}",
				_serverSupportsAcks, _ackId);
		}

		#endregion
	}
}
