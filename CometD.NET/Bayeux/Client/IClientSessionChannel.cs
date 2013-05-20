using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;

namespace CometD.Bayeux.Client
{
	/// <summary>
	/// <p>A client side channel representation.</p>
	/// <p>An <see cref="IClientSessionChannel"/> is scoped to a particular <see cref="IClientSession"/>
	/// that is obtained by a call to <see cref="IClientSession.GetChannel(string, bool)"/>.</p>
	/// </summary>
	/// <example>
	/// <p>Typical usage examples are:</p>
	/// <pre>
	///     clientSession.GetChannel("/foo/bar").Subscribe(mySubscriptionListener);
	///     clientSession.GetChannel("/foo/bar").Publish("Hello");
	///     clientSession.GetChannel("/meta/*").AddListener(myMetaChannelListener);
	/// </pre>
	/// </example>
	public interface IClientSessionChannel : IChannel
	{
		/// <summary>
		/// <p>Adds a listener to this channel.</p>
		/// <p>If the listener is a <see cref="IMessageListener"/>, it will be invoked
		/// if a message arrives to this channel.</p>
		/// <p>Adding a listener never involves communication with the server,
		/// differently from <see cref="Subscribe(IMessageListener)"/>.</p>
		/// <p>Listeners are best suited to receive messages from meta channels.</p>
		/// </summary>
		/// <param name="listener">The listener to add.</param>
		/// <seealso cref="RemoveListener(IClientSessionChannelListener)"/>
		void AddListener(IClientSessionChannelListener listener);

		/// <summary>
		/// <p>Removes the given <paramref name="listener"/> from this channel.</p>
		/// <p>Removing a listener never involves communication with the server,
		/// differently from <see cref="Unsubscribe(IMessageListener)"/>.</p>
		/// </summary>
		/// <param name="listener">The listener to remove (null to remove all).</param>
		/// <seealso cref="AddListener(IClientSessionChannelListener)"/>
		void RemoveListener(IClientSessionChannelListener listener);

		/// <summary>
		/// Returns an immutable snapshot of the listeners.
		/// </summary>
		/// <seealso cref="AddListener(IClientSessionChannelListener)"/>
		IList<IClientSessionChannelListener> Listeners { get; }

		/// <summary>
		/// The client session associated with this channel.
		/// </summary>
		IClientSession Session { get; }

		/// <summary>
		/// <p>Publishes the given <paramref name="data"/> onto this channel.</p>
		/// <p>The <paramref name="data"/> published must not be null and can be any object that
		/// can be natively converted to JSON (numbers, strings, arrays, lists, maps),
		/// or objects for which a JSON converter has been registered with the
		/// infrastructure responsible of the JSON conversion.</p>
		/// </summary>
		/// <param name="data">The data to publish.</param>
		/// <seealso cref="Publish(object, string)"/>
		void Publish(object data);

		/// <summary>
		/// Publishes the given <paramref name="data"/> to this channel,
		/// optionally specifying the <paramref name="messageId"/> to set on the publish message.
		/// </summary>
		/// <param name="data">The data to publish.</param>
		/// <param name="messageId">The message id to set on the message,
		/// or null to let the implementation choose the message id.</param>
		/// <seealso cref="IMessage.Id"/>
		void Publish(object data, string messageId);

		/// <summary>
		/// <p>Subscribes the given <paramref name="listener"/> to receive messages sent to this channel.</p>
		/// <p>Subscription involves communication with the server only for the first listener
		/// subscribed to this channel. Listeners registered after the first will not cause a message
		/// being sent to the server.</p>
		/// </summary>
		/// <param name="listener">The listener to register and invoke when a message arrives on this channel.</param>
		/// <seealso cref="Unsubscribe(IMessageListener)"/>
		/// <seealso cref="AddListener(IClientSessionChannelListener)"/>
		void Subscribe(IMessageListener listener);

		/// <summary>
		/// <p>Unsubscribes the given <paramref name="listener"/> from receiving messages sent to this channel.</p>
		/// <p>Unsubscription involves communication with the server only for the last listener
		/// unsubscribed from this channel.</p>
		/// </summary>
		/// <param name="listener">The listener to unsubscribe.</param>
		/// <seealso cref="Subscribe(IMessageListener)"/>
		/// <seealso cref="Unsubscribe()"/>
		void Unsubscribe(IMessageListener listener);

		/// <summary>
		/// Unsubscribes all subscribers registered on this channel.
		/// </summary>
		/// <seealso cref="Unsubscribe(IMessageListener)"/>
		void Unsubscribe();

		/// <summary>
		/// Return an immutable snapshot of the subscribers.
		/// </summary>
		/// <seealso cref="Subscribe(IMessageListener)"/>
		IList<IMessageListener> Subscribers { get; }

		/// <summary>
		/// Notifies the received message to all existing listeners.
		/// </summary>
		void NotifyMessageListeners(IMessage message);

		/*
		/// <summary>
		/// <p>Releases this channel from its <see cref="IClientSession"/>.</p>
		/// <p>If the release is successful, subsequent invocations of <see cref="IClientSession.GetChannel(string, bool)"/>
		/// will return a new, different, instance of a <see cref="IClientSessionChannel"/>.</p>
		/// <p>The release of a <see cref="IClientSessionChannel"/> is successful only if no listeners and no
		/// subscribers are present at the moment of the release.</p>
		/// </summary>
		/// <returns>True if the release was successful, false otherwise.</returns>
		/// <seealso cref="IsReleased"/>
		bool Release();

		/// <summary>
		/// Returns whether this channel has been released.
		/// </summary>
		/// <seealso cref="Release()"/>
		bool IsReleased { get; }
		*/
	}

	/// <summary>
	/// <p>Represents a listener on a <see cref="IClientSessionChannel"/>.</p>
	/// <p>Sub-interfaces specify the exact semantic of the listener.</p>
	/// </summary>
	public interface IClientSessionChannelListener : IBayeuxListener, IEquatable<IClientSessionChannelListener>
	{
	}

	/// <summary>
	/// A listener for messages on a <see cref="IClientSessionChannel"/>.
	/// </summary>
	public interface IMessageListener : IClientSessionChannelListener, IEquatable<IMessageListener>
	{
		/// <summary>
		/// Callback invoked when a message is received on the given <paramref name="channel"/>.
		/// </summary>
		/// <param name="channel">The channel that received the message.</param>
		/// <param name="message">The message received.</param>
		void OnMessage(IClientSessionChannel channel, IMessage message);
	}

	/// <summary>
	/// This is the implementation of a listener for messages on a <see cref="IClientSessionChannel"/>
	/// via an <see cref="Action&lt;IClientSessionChannel, IMessage&gt;"/> callback delegation.
	/// </summary>
	[Serializable]
	public sealed class CallbackMessageListener<T> : IMessageListener, IEquatable<CallbackMessageListener<T>>
	{
		private readonly Action<IClientSessionChannel, IMessage, T> _callback;
		private readonly T _stateObject;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:CallbackMessageListener&lt;T&gt;"/> class.
		/// </summary>
		public CallbackMessageListener(
			Action<IClientSessionChannel, IMessage, T> callback, T state)
		{
			if (null == callback)
				throw new ArgumentNullException("callback");

			_callback = callback;
			_stateObject = state;
		}

		#region IMessageListener Members

		/// <summary>
		/// Callback invoked when a message is received on the given <paramref name="channel"/>.
		/// </summary>
		public void OnMessage(IClientSessionChannel channel, IMessage message)
		{
			// Specify what method to call when callbackAction completes
			_callback.BeginInvoke(channel, message, _stateObject, ExecAsyncCallback, null);
		}

		/// <summary>
		/// All calls to BeginInvoke must be matched with calls to EndInvoke according to the MSDN documentation.
		/// </summary>
		private static void ExecAsyncCallback(IAsyncResult asyncResult)
		{
			// Retrieve the delegate
			AsyncResult result = asyncResult as AsyncResult;
			if (null != result)
			{
				Action<IClientSessionChannel, IMessage, T> caller
					= result.AsyncDelegate as Action<IClientSessionChannel, IMessage, T>;
				if (null != caller)
				{
					// Call EndInvoke to retrieve the results
					caller.EndInvoke(asyncResult);
					// DEBUG
					//System.Diagnostics.Debug.Print("Done calling: {0}#{1}", caller, caller.GetHashCode());
				}
			}
		}

		#endregion

		#region IEquatable Members

		/// <summary>
		/// Returns the hash code for this instance.
		/// </summary>
		public override int GetHashCode()
		{
			return _callback.GetHashCode();
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
			return _callback.ToString() + "#" + _callback.GetHashCode();
		}

		/// <summary>
		/// Determines whether this callback listener
		/// and the specified <see cref="Object"/> are equal.
		/// </summary>
		public override bool Equals(object obj)
		{
			CallbackMessageListener<T> other = obj as CallbackMessageListener<T>;
			if (other != null)
				return this.Equals(other);

			return base.Equals(obj);
		}

		/// <summary>
		/// Determines whether this callback listener
		/// and the specified <see cref="IClientSessionChannelListener"/> are equal.
		/// </summary>
		public bool Equals(IClientSessionChannelListener other)
		{
			return this.Equals(other as CallbackMessageListener<T>);
		}

		/// <summary>
		/// Determines whether this callback listener
		/// and the specified <see cref="IMessageListener"/> are equal.
		/// </summary>
		public bool Equals(IMessageListener other)
		{
			return this.Equals(other as CallbackMessageListener<T>);
		}

		/// <summary>
		/// Determines whether this callback listener
		/// is equal to another <see cref="T:CallbackMessageListener&lt;T&gt;"/> object.
		/// </summary>
		public bool Equals(CallbackMessageListener<T> other)
		{
			// TODO: Object.ReferenceEquals()
			return (other != null && _callback.Equals(other._callback)
				&& ((_stateObject == null && other._stateObject == null)
				|| (_stateObject != null && other._stateObject != null && _stateObject.Equals(other._stateObject))));
		}

		#endregion
	}
}
