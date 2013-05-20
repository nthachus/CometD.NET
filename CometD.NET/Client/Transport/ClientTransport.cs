using System;
using System.Collections.Generic;

using CometD.Bayeux;
using CometD.Common;

namespace CometD.Client.Transport
{
	/// <summary>
	/// Represents the base client <see cref="ITransport">Transport</see> of a Bayeux client session.
	/// </summary>
	public abstract class ClientTransport : AbstractTransport
	{
		#region Constants

		/// <summary>
		/// The polling duration (timeout for all transports).
		/// </summary>
		[Obsolete("The Timeout option has not been used yet.")]
		public const string TimeoutOption = "timeout";

		/// <summary>
		/// The polling interval.
		/// </summary>
		[Obsolete("The Interval option has not been used yet.")]
		public const string IntervalOption = "interval";

		/// <summary>
		/// The HTTP request timeout option.
		/// </summary>
		public const string MaxNetworkDelayOption = "maxNetworkDelay";

		#endregion

		/// <summary>
		/// Initializes a new instance of the <see cref="ClientTransport"/> class.
		/// </summary>
		protected ClientTransport(string name, IDictionary<string, object> options)
			: base(name, options) { }

		/// <summary>
		/// Initializes this client transport.
		/// </summary>
		public virtual void Init()
		{
		}

		/// <summary>
		/// Cancels all available HTTP requests in the client transport.
		/// </summary>
		public abstract void Abort();

		/// <summary>
		/// Resets this client transport.
		/// </summary>
		public virtual void Reset()
		{
		}

		/// <summary>
		/// Terminates this client transport.
		/// </summary>
		public virtual void Terminate()
		{
		}

		/// <summary>
		/// Checks if this client transport supports the specified Bayeux version.
		/// </summary>
		public abstract bool Accept(string bayeuxVersion);

		/// <summary>
		/// Sends the specified messages to a Bayeux server asynchronously.
		/// </summary>
		/// <param name="listener">The listener used to process the request response.</param>
		/// <param name="messages">The list of messages will be sent in one request.</param>
		public abstract void Send(ITransportListener listener, params IMutableMessage[] messages);
	}
}
