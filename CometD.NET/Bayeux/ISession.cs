using System;
using System.Collections.Generic;

namespace CometD.Bayeux
{
	/// <summary>
	/// <p>A Bayeux session represents a connection between a Bayeux client and a Bayeux server.</p>
	/// <p>This interface is the common base interface for both the server side and the client side
	/// representations of a session:</p>
	/// <ul>
	/// <li>if the remote client is not a Java client, then only a <code>Server.IServerSession</code>
	/// instance will exist on the server and represents the remote client.</li>
	/// <li>if the remote client is a Java client, then a <see cref="Client.IClientSession"/>
	/// instance will exist on the client and a <code>Server.IServerSession</code>
	/// instance will exist on the server, linked by the same client id.</li>
	/// <li>if the client is a Java client, but it is located in the server, then the
	/// <see cref="Client.IClientSession"/> instance will be an instance
	/// of <code>Server.ILocalSession</code> and will be associated
	/// with a <code>Server.IServerSession</code> instance.</li>
	/// </ul>
	/// </summary>
	public interface ISession
	{
		/// <summary>
		/// <p>The client id of the session.</p>
		/// <p>This would more correctly be called a "sessionId", but for
		/// backwards compatibility with the Bayeux protocol,
		/// it is a field called "clientId" that identifies a session.</p>
		/// </summary>
		/// <value>The id of this session.</value>
		string Id { get; }

		/// <summary>
		/// A connected session is a session where the link between the client and the server
		/// has been established.
		/// </summary>
		/// <value>Whether the session is connected.</value>
		/// <seealso cref="Disconnect()"/>
		bool IsConnected { get; }

		/// <summary>
		/// A handshook session is a session where the handshake has successfully completed.
		/// </summary>
		/// <value>Whether the session is handshook.</value>
		bool IsHandshook { get; }

		/// <summary>
		/// Disconnects this session, ending the link between the client and the server peers.
		/// </summary>
		/// <seealso cref="IsConnected"/>
		void Disconnect();

		/// <summary>
		/// <p>Sets a named session attribute value.</p>
		/// <p>Session attributes are convenience data that allows arbitrary
		/// application data to be associated with a session.</p>
		/// </summary>
		/// <param name="name">The attribute name.</param>
		/// <param name="value">The attribute value.</param>
		void SetAttribute(string name, object value);

		/// <summary>
		/// Retrieves the value of named session attribute.
		/// </summary>
		/// <param name="name">The name of the attribute.</param>
		/// <returns>The attribute value or null if the attribute is not present.</returns>
		object GetAttribute(string name);

		/// <summary>
		/// Returns the list of session attribute names.
		/// </summary>
		ICollection<string> AttributeNames { get; }

		/// <summary>
		/// Removes a named session attribute.
		/// </summary>
		/// <param name="name">The name of the attribute.</param>
		/// <returns>The value of the attribute.</returns>
		object RemoveAttribute(string name);

		/// <summary>
		/// Executes the given command in a batch so that any Bayeux message sent
		/// by the command (via the Bayeux API) is queued up until the end of the
		/// command and then all messages are sent at once.
		/// </summary>
		/// <param name="batch">The Runnable to run as a batch.</param>
		void Batch(Action batch);

		/// <summary>
		/// <p>Starts a batch, to be ended with <see cref="EndBatch()"/>.</p>
		/// <p>The <see cref="Batch(Action)"/> method should be preferred since it automatically
		/// starts and ends a batch without relying on a try/finally block.</p>
		/// <p>This method is to be used in the cases where the use of <see cref="Batch(Action)"/>
		/// is not possible or would make the code more complex.</p>
		/// </summary>
		/// <seealso cref="EndBatch()"/>
		/// <seealso cref="Batch(Action)"/>
		void StartBatch();

		/// <summary>
		/// Ends a batch started with <see cref="StartBatch()"/>.
		/// </summary>
		/// <returns>True if the batch ended and there were messages to send.</returns>
		/// <seealso cref="StartBatch()"/>
		bool EndBatch();
	}
}
