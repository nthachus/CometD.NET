using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;

using CometD.Bayeux.Client;
using CometD.Bayeux;

namespace CometD.Common
{
	/// <summary>
	/// <p>Partial implementation of <see cref="IClientSession"/>.</p>
	/// <p>It handles extensions and batching, and provides utility methods to be used by subclasses.</p>
	/// </summary>
	public abstract class AbstractClientSession : IClientSession
	{
		private readonly IDictionary<string, object> _attributes
			= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		private readonly IList<IExtension> _extensions = new List<IExtension>();
		private readonly IDictionary<string, AbstractSessionChannel> _channels
			= new Dictionary<string, AbstractSessionChannel>(StringComparer.OrdinalIgnoreCase);

		// Atomic integer
		private volatile int _batch;
		private static long _idGen = 0;

		/// <summary>
		/// Initializes a new instance of the <see cref="AbstractClientSession"/> class.
		/// </summary>
		protected AbstractClientSession() { }

		/// <summary>
		/// Generates a new message id.
		/// </summary>
		public virtual string NewMessageId()
		{
			long newId = Interlocked.Increment(ref _idGen);
			return newId.ToString(CultureInfo.InvariantCulture);
		}

		#region IClientSession Members

		/// <summary>
		/// Adds a new extension to this client session.
		/// </summary>
		public virtual void AddExtension(IExtension extension)
		{
			if (null != extension)
			{
				lock (_extensions)
				{
					if (!_extensions.Contains(extension))
						_extensions.Add(extension);
				}
			}
		}

		/// <summary>
		/// Removes an existing extension from this client session.
		/// </summary>
		public virtual void RemoveExtension(IExtension extension)
		{
			if (null != extension)
			{
				lock (_extensions)
				{
					if (_extensions.Contains(extension))
						_extensions.Remove(extension);
				}
			}
		}

		/// <summary>
		/// Returns an immutable list of extensions present in this <see cref="IClientSession"/> instance.
		/// </summary>
		/// <seealso cref="AddExtension(IExtension)"/>
		public virtual IList<IExtension> Extensions
		{
			get
			{
				lock (_extensions)
				{
					return new ReadOnlyCollection<IExtension>(_extensions);
				}
			}
		}

		/// <summary>
		/// Equivalent to <see cref="Handshake(IDictionary&lt;string, object&gt;)"/>(null).
		/// </summary>
		public abstract void Handshake();

		/// <summary>
		/// Initiates the Bayeux protocol handshake with the server(s).
		/// </summary>
		/// <param name="handshakeFields">Additional fields to add to the handshake message.</param>
		public abstract void Handshake(IDictionary<string, object> handshakeFields);

		/// <summary>
		/// Returns a client side channel scoped by this session.
		/// </summary>
		/// <param name="channelId">Specific or wild channel name.</param>
		/// <param name="create">Whether to create the client session channel if it does not exist.</param>
		public virtual IClientSessionChannel GetChannel(string channelId, bool create = true)
		{
			if (String.IsNullOrEmpty(channelId))
				throw new ArgumentNullException("channelId");

			AbstractSessionChannel channel = null;
			if ((!_channels.TryGetValue(channelId, out channel) || channel == null) && create)
			{
				ChannelId id = this.NewChannelId(channelId);
				channel = this.NewChannel(id);

				lock (_channels) _channels[channelId] = channel;
			}

			return channel;
		}

		#endregion

		/*
		/// <summary>
		/// Returns all available channels of this client session.
		/// </summary>
		protected virtual IDictionary<string, AbstractSessionChannel> Channels
		{
			get { return _channels; }
		}
		*/

		/// <summary>
		/// Sends the specified mutable message with each existing session extensions.
		/// </summary>
		public virtual bool ExtendSend(IMutableMessage message)
		{
			if (null == message) return false;

			if (message.IsMeta)
			{
				for (int i = 0; i < _extensions.Count; i++)
				{
					if (!_extensions[i].SendMeta(this, message))
						return false;
				}
			}
			else
			{
				for (int i = 0; i < _extensions.Count; i++)
				{
					if (!_extensions[i].Send(this, message))
						return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Receives the specified mutable message with each existing session extensions.
		/// </summary>
		public virtual bool ExtendReceive(IMutableMessage message)
		{
			if (null == message) return false;

			if (message.IsMeta)
			{
				for (int i = 0; i < _extensions.Count; i++)
				{
					if (!_extensions[i].ReceiveMeta(this, message))
						return false;
				}
			}
			else
			{
				for (int i = 0; i < _extensions.Count; i++)
				{
					if (!_extensions[i].ReceiveMeta(this, message))
						return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Creates a new <see cref="ChannelId"/> object from the specified channel id string.
		/// </summary>
		protected abstract ChannelId NewChannelId(string channelId);

		/// <summary>
		/// Creates a new <see cref="AbstractSessionChannel"/> object from the specified channel id.
		/// </summary>
		protected abstract AbstractSessionChannel NewChannel(ChannelId channelId);

		#region ISession Members

		/// <summary>
		/// The client id of the session.
		/// </summary>
		public abstract string Id { get; }

		/// <summary>
		/// A connected session is a session where the link between the client and the server
		/// has been established.
		/// </summary>
		public abstract bool IsConnected { get; }

		/// <summary>
		/// A handshook session is a session where the handshake has successfully completed.
		/// </summary>
		public abstract bool IsHandshook { get; }

		/// <summary>
		/// Disconnects this session, ending the link between the client and the server peers.
		/// </summary>
		public abstract void Disconnect();

		/// <summary>
		/// Sets a named session attribute value.
		/// </summary>
		public virtual void SetAttribute(string name, object value)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			lock (_attributes) _attributes[name] = value;
		}

		/// <summary>
		/// Retrieves the value of named session attribute.
		/// </summary>
		public object GetAttribute(string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			lock (_attributes)
			{
				if (_attributes.ContainsKey(name))
					return _attributes[name];
			}

			return null;
		}

		/// <summary>
		/// Returns the list of session attribute names.
		/// </summary>
		public virtual ICollection<string> AttributeNames
		{
			get { return _attributes.Keys; }
		}

		/// <summary>
		/// Removes a named session attribute.
		/// </summary>
		public virtual object RemoveAttribute(string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			lock (_attributes)
			{
				if (_attributes.ContainsKey(name))
				{
					object old = _attributes[name];
					_attributes.Remove(name);

					return old;
				}
			}

			return null;
		}

		/// <summary>
		/// Executes the given command in a batch so that any Bayeux message sent
		/// by the command (via the Bayeux API) is queued up until the end of the
		/// command and then all messages are sent at once.
		/// </summary>
		/// <param name="batch">The Runnable to run as a batch.</param>
		public virtual void Batch(Action batch)
		{
			if (null != batch)
			{
				this.StartBatch();
				try
				{
					batch();
				}
				finally
				{
					this.EndBatch();
				}
			}
		}

		/// <summary>
		/// Starts a batch, to be ended with <see cref="EndBatch()"/>.
		/// </summary>
		public virtual void StartBatch()
		{
#pragma warning disable 0420
			Interlocked.Increment(ref _batch);
#pragma warning restore 0420
		}

		/// <summary>
		/// Ends a batch started with <see cref="StartBatch()"/>.
		/// </summary>
		public virtual bool EndBatch()
		{
#pragma warning disable 0420
			if (Interlocked.Decrement(ref _batch) == 0)
#pragma warning restore 0420
			{
				this.SendBatch();
				return true;
			}

			return false;
		}

		#endregion

		/// <summary>
		/// Sends all existing messages at the end of the batch.
		/// </summary>
		public abstract void SendBatch();

		/// <summary>
		/// Whether batching is executing or not.
		/// </summary>
		protected virtual bool IsBatching
		{
			get { return (_batch > 0); }
		}

		/// <summary>
		/// Clears all subscriptions from each channels of this client session.
		/// </summary>
		public virtual void ResetSubscriptions()
		{
			lock (_channels)
			{
				foreach (AbstractSessionChannel ch in _channels.Values)
					ch.ResetSubscriptions();
			}
		}

		/// <summary>
		/// <p>Receives a message (from the server) and process it.</p>
		/// <p>Processing the message involves calling the receive extensions and the channel listeners.</p>
		/// </summary>
		/// <param name="message">The mutable version of the message received.</param>
		public virtual void Receive(IMutableMessage message)
		{
			if (null == message)
				throw new ArgumentNullException("message");

			string id = message.Channel;
			if (String.IsNullOrEmpty(id))
				throw new ArgumentException("Bayeux messages must have a channel, " + message);

			if (!this.ExtendReceive(message))
				return;

			ChannelId channelId;
			IClientSessionChannel channel = this.GetChannel(id, false);
			if (null != channel)
			{
				channelId = channel.ChannelId;
				channel.NotifyMessageListeners(message);
			}
			else
				channelId = message.ChannelId;

			foreach (string wildChannelName in channelId.Wilds)
			{
				//channelIdPattern = this.NewChannelId(channelPattern);
				//if (channelIdPattern != null && channelIdPattern.Matches(channelId))
				//{
				channel = this.GetChannel(wildChannelName, false);// Wild channel
				if (channel != null)
					channel.NotifyMessageListeners(message);
				//}
			}
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
#if DEBUG
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat(CultureInfo.InvariantCulture, "{{{0}  Id: {1}", Environment.NewLine, this.Id);

			sb.Append(Environment.NewLine).Append("  Channels: [");
			lock (_channels)
			{
				foreach (KeyValuePair<string, AbstractSessionChannel> child in _channels)
				{
					sb.AppendFormat(CultureInfo.InvariantCulture,
						"{0}    +- '{1}': ", Environment.NewLine, child.Key);

					sb.Append((null == child.Value) ? "null"
						: child.Value.ToString().Replace(Environment.NewLine, Environment.NewLine + "       "));
				}
			}

			sb.AppendFormat(CultureInfo.InvariantCulture, "{0}  ]{0}}}", Environment.NewLine);
			return sb.ToString();
#else
			return this.Id;
#endif
		}

	}
}
