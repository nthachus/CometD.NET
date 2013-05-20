using System;
using System.Collections.Generic;

namespace CometD.Bayeux.Client
{
	/// <summary>
	/// <p>This interface represents the client side Bayeux session.</p>
	/// <p>In addition to the common Bayeux session, this
	/// interface provides method to configure extension, access channels
	/// and to initiate the communication with a Bayeux server(s).</p>
	/// </summary>
	public interface IClientSession : ISession
	{
		/// <summary>
		/// Adds a new extension to this session.
		/// </summary>
		/// <param name="extension">The extension to add.</param>
		/// <seealso cref="RemoveExtension(IExtension)"/>
		void AddExtension(IExtension extension);

		/// <summary>
		/// Removes an existing extension from this session.
		/// </summary>
		/// <param name="extension">The extension to remove.</param>
		/// <seealso cref="AddExtension(IExtension)"/>
		void RemoveExtension(IExtension extension);

		/// <summary>
		/// Returns an immutable list of extensions present in this <see cref="IClientSession"/> instance.
		/// </summary>
		/// <seealso cref="AddExtension(IExtension)"/>
		IList<IExtension> Extensions { get; }

		/// <summary>
		/// Equivalent to <see cref="Handshake(IDictionary&lt;string, object&gt;)"/>(null).
		/// </summary>
		void Handshake();

		/// <summary>
		/// <p>Initiates the Bayeux protocol handshake with the server(s).</p>
		/// <p>The handshake initiated by this method is asynchronous and
		/// does not wait for the handshake response.</p>
		/// </summary>
		/// <param name="handshakeFields">Additional fields to add to the handshake message.</param>
		void Handshake(IDictionary<string, object> handshakeFields);

		/// <summary>
		/// <p>Returns a client side channel scoped by this session.</p>
		/// <p>The channel name may be for a specific channel (e.g. "/foo/bar")
		/// or for a wild channel (e.g. "/meta/**" or "/foo/*").</p>
		/// <p>This method will always return a channel, even if the
		/// the channel has not been created on the server side.  The server
		/// side channel is only involved once a publish or subscribe method
		/// is called on the channel returned by this method.</p>
		/// </summary>
		/// <example>
		/// <p>Typical usage examples are:</p>
		/// <pre>
		///     clientSession.GetChannel("/foo/bar").Subscribe(mySubscriptionListener);
		///     clientSession.GetChannel("/foo/bar").Publish("Hello");
		///     clientSession.GetChannel("/meta/*").AddListener(myMetaChannelListener);
		/// </pre>
		/// </example>
		/// <param name="channelId">Specific or wild channel name.</param>
		/// <param name="create">Whether to create the client session channel if it does not exist.</param>
		/// <returns>A channel scoped by this session.</returns>
		IClientSessionChannel GetChannel(string channelId, bool create = true);
	}

	/// <summary>
	/// <p>Extension API for client session.</p>
	/// <p>An extension allows user code to interact with the Bayeux protocol as late
	/// as messages are sent or as soon as messages are received.</p>
	/// <p>Messages may be modified, or state held, so that the extension adds a
	/// specific behavior simply by observing the flow of Bayeux messages.</p>
	/// </summary>
	/// <seealso cref="IClientSession.AddExtension(IExtension)"/>
	public interface IExtension : IEquatable<IExtension>
	{
		/// <summary>
		/// Callback method invoked every time a normal message is received.
		/// </summary>
		/// <param name="session">The session object that is receiving the message.</param>
		/// <param name="message">The message received.</param>
		/// <returns>True if message processing should continue, false if it should stop.</returns>
		bool Receive(IClientSession session, IMutableMessage message);

		/// <summary>
		/// Callback method invoked every time a meta message is received.
		/// </summary>
		/// <param name="session">The session object that is receiving the meta message.</param>
		/// <param name="message">The meta message received.</param>
		/// <returns>True if message processing should continue, false if it should stop.</returns>
		bool ReceiveMeta(IClientSession session, IMutableMessage message);

		/// <summary>
		/// Callback method invoked every time a normal message is being sent.
		/// </summary>
		/// <param name="session">The session object that is sending the message.</param>
		/// <param name="message">The message being sent.</param>
		/// <returns>True if message processing should continue, false if it should stop.</returns>
		bool Send(IClientSession session, IMutableMessage message);

		/// <summary>
		/// Callback method invoked every time a meta message is being sent.
		/// </summary>
		/// <param name="session">The session object that is sending the meta message.</param>
		/// <param name="message">The meta message being sent.</param>
		/// <returns>True if message processing should continue, false if it should stop.</returns>
		bool SendMeta(IClientSession session, IMutableMessage message);
	}

	/// <summary>
	/// Empty implementation of <see cref="IExtension"/>.
	/// </summary>
	public abstract class AdapterExtension : IExtension
	{
		/// <summary>
		/// Callback method invoked every time a normal message is received.
		/// </summary>
		/// <returns>Always true.</returns>
		public virtual bool Receive(IClientSession session, IMutableMessage message)
		{
			return true;
		}

		/// <summary>
		/// Callback method invoked every time a meta message is received.
		/// </summary>
		/// <returns>Always true.</returns>
		public virtual bool ReceiveMeta(IClientSession session, IMutableMessage message)
		{
			return true;
		}

		/// <summary>
		/// Callback method invoked every time a normal message is being sent.
		/// </summary>
		/// <returns>Always true.</returns>
		public virtual bool Send(IClientSession session, IMutableMessage message)
		{
			return true;
		}

		/// <summary>
		/// Callback method invoked every time a meta message is being sent.
		/// </summary>
		/// <returns>Always true.</returns>
		public virtual bool SendMeta(IClientSession session, IMutableMessage message)
		{
			return true;
		}

		/// <summary>
		/// Indicates whether the current <see cref="IExtension"/>
		/// is equal to another <see cref="IExtension"/>.
		/// </summary>
		public abstract bool Equals(IExtension other);
	}

}
