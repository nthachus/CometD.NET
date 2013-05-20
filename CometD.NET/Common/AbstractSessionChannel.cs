using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using CometD.Bayeux;
using CometD.Bayeux.Client;

namespace CometD.Common
{
	/// <summary>
	/// A channel scoped to a <see cref="IClientSession"/>.
	/// </summary>
	public abstract class AbstractSessionChannel : IClientSessionChannel
	{
		private readonly ChannelId _id;

		private readonly IDictionary<string, object> _attributes
			= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		private readonly IList<IMessageListener> _subscriptions = new List<IMessageListener>();
		private readonly IList<IClientSessionChannelListener> _listeners = new List<IClientSessionChannelListener>();

		/// <summary>
		/// Initializes a new instance of the <see cref="AbstractSessionChannel"/> class
		/// with the specified <see cref="ChannelId"/>.
		/// </summary>
		/// <param name="id">Holder of a channel id broken into path segments.</param>
		protected AbstractSessionChannel(ChannelId id)
		{
			if (null == id) throw new ArgumentNullException("id");

			_id = id;
		}

		#region IClientSessionChannel Members

		/// <summary>
		/// <p>Adds a listener to this channel.</p>
		/// <p>If the listener is a <see cref="IMessageListener"/>, it will be invoked
		/// if a message arrives to this channel.</p>
		/// <p>Adding a listener never involves communication with the server,
		/// differently from <see cref="Subscribe(IMessageListener)"/>.</p>
		/// <p>Listeners are best suited to receive messages from meta channels.</p>
		/// </summary>
		/// <param name="listener">The listener to add.</param>
		public virtual void AddListener(IClientSessionChannelListener listener)
		{
			if (null != listener)
			{
				lock (_listeners)
				{
					if (!_listeners.Contains(listener))
						_listeners.Add(listener);
				}
			}
		}

		/// <summary>
		/// <p>Removes the given <paramref name="listener"/> from this channel.</p>
		/// <p>Removing a listener never involves communication with the server,
		/// differently from <see cref="Unsubscribe(IMessageListener)"/>.</p>
		/// </summary>
		/// <param name="listener">The listener to remove (null to remove all).</param>
		public virtual void RemoveListener(IClientSessionChannelListener listener)
		{
			lock (_listeners)
			{
				if (null != listener)
				{
					if (_listeners.Contains(listener))
						_listeners.Remove(listener);
				}
				else
					_listeners.Clear();
			}
		}

		/// <summary>
		/// Returns an immutable snapshot of the listeners.
		/// </summary>
		public virtual IList<IClientSessionChannelListener> Listeners
		{
			get
			{
				lock (_listeners)
				{
					return new ReadOnlyCollection<IClientSessionChannelListener>(_listeners);
				}
			}
		}

		/// <summary>
		/// The client session associated with this channel.
		/// </summary>
		public abstract IClientSession Session { get; }

		/// <summary>
		/// <p>Publishes the given <paramref name="data"/> onto this channel.</p>
		/// <p>The <paramref name="data"/> published must not be null and can be any object that
		/// can be natively converted to JSON (numbers, strings, arrays, lists, maps),
		/// or objects for which a JSON converter has been registered with the
		/// infrastructure responsible of the JSON conversion.</p>
		/// </summary>
		/// <remarks>Equivalent to <see cref="Publish(object, string)"/>(data, null).</remarks>
		/// <param name="data">The data to publish.</param>
		/// <seealso cref="Publish(object, string)"/>
		public virtual void Publish(object data)
		{
			this.Publish(data, null);
		}

		/// <summary>
		/// Publishes the given <paramref name="data"/> to this channel,
		/// optionally specifying the <paramref name="messageId"/> to set on the publish message.
		/// </summary>
		/// <param name="data">The data to publish.</param>
		/// <param name="messageId">The message id to set on the message,
		/// or null to let the implementation choose the message id.</param>
		public abstract void Publish(object data, string messageId);

		/// <summary>
		/// <p>Subscribes the given <paramref name="listener"/> to receive messages sent to this channel.</p>
		/// <p>Subscription involves communication with the server only for the first listener
		/// subscribed to this channel. Listeners registered after the first will not cause a message
		/// being sent to the server.</p>
		/// </summary>
		/// <param name="listener">The listener to register and invoke when a message arrives on this channel.</param>
		/// <seealso cref="Unsubscribe(IMessageListener)"/>
		/// <seealso cref="AddListener(IClientSessionChannelListener)"/>
		public virtual void Subscribe(IMessageListener listener)
		{
			if (null != listener)
			{
				bool hasSubscriptions = false;
				lock (_subscriptions)
				{
					if (!_subscriptions.Contains(listener))
					{
						_subscriptions.Add(listener);

						if (_subscriptions.Count == 1)	// TODO: > 0
							hasSubscriptions = true;
					}
				}

				if (hasSubscriptions)
					this.SendSubscribe();
			}
		}

		/// <summary>
		/// <p>Unsubscribes the given <paramref name="listener"/> from receiving messages sent to this channel.</p>
		/// <p>Unsubscription involves communication with the server only for the last listener
		/// unsubscribed from this channel.</p>
		/// </summary>
		/// <param name="listener">The listener to unsubscribe.</param>
		/// <seealso cref="Subscribe(IMessageListener)"/>
		/// <seealso cref="Unsubscribe()"/>
		public virtual void Unsubscribe(IMessageListener listener)
		{
			if (null != listener)
			{
				bool cleaned = false;
				lock (_subscriptions)
				{
					bool removed = _subscriptions.Remove(listener);
					if (removed)
					{
						if (_subscriptions.Count == 0)
							cleaned = true;
					}
				}

				if (cleaned)
					this.SendUnsubscribe();
			}
		}

		/// <summary>
		/// Unsubscribes all subscribers registered on this channel.
		/// </summary>
		/// <seealso cref="Unsubscribe(IMessageListener)"/>
		public virtual void Unsubscribe()
		{
			bool cleaned = false;
			lock (_subscriptions)
			{
				if (_subscriptions.Count > 0)
				{
					_subscriptions.Clear();
					cleaned = true;
				}
			}

			if (cleaned)
				this.SendUnsubscribe();
		}

		/// <summary>
		/// Return an immutable snapshot of the subscribers.
		/// </summary>
		/// <seealso cref="Subscribe(IMessageListener)"/>
		public virtual IList<IMessageListener> Subscribers
		{
			get
			{
				lock (_subscriptions)
				{
					return new ReadOnlyCollection<IMessageListener>(_subscriptions);
				}
			}
		}

		/// <summary>
		/// Notifies the received message to all existing listeners.
		/// </summary>
		public virtual void NotifyMessageListeners(IMessage message)
		{
			IMessageListener listener;
			for (int i = 0; i < _listeners.Count; i++)
			{
				listener = _listeners[i] as IMessageListener;
				if (listener != null)
					this.NotifyOnMessage(listener, message);
			}

			if (message != null && message.Data != null)
			{
				for (int i = 0; i < _subscriptions.Count; i++)
				{
					listener = _subscriptions[i];
					if (listener != null)
						this.NotifyOnMessage(listener, message);
				}
			}
		}

		#endregion

		/// <summary>
		/// Send subscription message(s) to Bayeux server
		/// to subscribe this session channel with all assigned message listeners.
		/// </summary>
		protected abstract void SendSubscribe();

		/// <summary>
		/// Send un-subscription message(s) to Bayeux server
		/// to un-subscribe all assigned message listeners from this session channel.
		/// </summary>
		protected abstract void SendUnsubscribe();

		/// <summary>
		/// Clears all subscriptions from this session channel.
		/// </summary>
		public virtual void ResetSubscriptions()
		{
			// TODO: lock (_subscriptions) _subscriptions.Clear();
			if (_subscriptions.Count > 0) this.SendSubscribe();
		}

		private void NotifyOnMessage(IMessageListener listener, IMessage message)
		{
			try
			{
				listener.OnMessage(this, message);
			}
			catch (Exception ex)
			{
				// DEBUG
				Trace.TraceInformation("Exception while invoking listener: {1}{0}{2}",
					Environment.NewLine, listener, ex.ToString());
			}
		}

		/*public virtual bool Release()
		{
			if (_released)
				return false;

			if (_subscriptions.Count == 0 && _listeners.Count == 0)
			{
				bool removed = AbstractClientSession.Channels.Remove(this.Id, this);
				_released = removed;

				return removed;
			}

			return false;
		}

		public virtual bool IsReleased
		{
			get { return _released; }
		}*/

		#region IChannel Members

		/// <summary>
		/// The channel id as a string.
		/// </summary>
		public virtual string Id
		{
			get { return _id.ToString(); }
		}

		/// <summary>
		/// The channel ID as a <see cref="ChannelId"/>.
		/// </summary>
		public virtual ChannelId ChannelId
		{
			get { return _id; }
		}

		/// <summary>
		/// Tells whether the channel is a meta channel,
		/// that is if its id starts with <code>"/meta/"</code>.
		/// </summary>
		public virtual bool IsMeta
		{
			get { return _id.IsMeta; }
		}

		/// <summary>
		/// Tells whether the channel is a service channel,
		/// that is if its id starts with <code>"/service/"</code>.
		/// </summary>
		public virtual bool IsService
		{
			get { return _id.IsService; }
		}

		/// <summary>
		/// A broadcasting channel is a channel that is neither a
		/// meta channel nor a service channel.
		/// </summary>
		public virtual bool IsBroadcast
		{
			get { return _id.IsBroadcast; }
		}

		/// <summary>
		/// Tells whether a channel contains the wild character '*',
		/// for example <code>"/foo/*"</code> or if it is <see cref="IsDeepWild"/>.
		/// </summary>
		public virtual bool IsWild
		{
			get { return _id.IsWild; }
		}

		/// <summary>
		/// Tells whether a channel contains the deep wild characters '**',
		/// for example <code>"/foo/**"</code>.
		/// </summary>
		public virtual bool IsDeepWild
		{
			get { return _id.IsDeepWild; }
		}

		/// <summary>
		/// Sets a named channel attribute value.
		/// </summary>
		public virtual void SetAttribute(string name, object value)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			lock (_attributes) _attributes[name] = value;
		}

		/// <summary>
		/// Retrieves the value of named channel attribute.
		/// </summary>
		public virtual object GetAttribute(string name)
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
		/// The list of channel attribute names.
		/// </summary>
		public virtual ICollection<string> AttributeNames
		{
			get { return _attributes.Keys; }
		}

		/// <summary>
		/// Removes a named channel attribute.
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

		#endregion

		#region IEquatable Members

		/*
		/// <summary>
		/// Returns the hash code for this session channel.
		/// </summary>
		public override int GetHashCode()
		{
			return _id.GetHashCode();
		}

		/// <summary>
		/// Returns a value indicating whether this session channel
		/// is equal to a specified object.
		/// </summary>
		public override bool Equals(object obj)
		{
			IChannel ch = obj as IChannel;
			if (ch != null)
				return _id.Equals(ch.ChannelId);

			return base.Equals(obj);
		}
		*/

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
#if DEBUG
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat(CultureInfo.InvariantCulture, "{{{0}  Id: {1}", Environment.NewLine, _id);

			sb.Append(Environment.NewLine).Append("  Listeners: [");
			for (int i = 0; i < _listeners.Count; i++)
			{
				sb.Append(Environment.NewLine).Append("    +- ");
				sb.Append(_listeners[i]);
			}

			sb.AppendFormat(CultureInfo.InvariantCulture, "{0}  ]{0}  Subscriptions: [", Environment.NewLine);
			for (int i = 0; i < _subscriptions.Count; i++)
			{
				sb.Append(Environment.NewLine).Append("    +- ");
				sb.Append(_subscriptions[i]);
			}

			sb.AppendFormat(CultureInfo.InvariantCulture, "{0}  ]{0}}}", Environment.NewLine);
			return sb.ToString();
#else
			return _id.ToString();
#endif
		}

		#endregion
	}
}
