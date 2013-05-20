using System;

namespace CometD.Client
{
	/// <summary>
	/// The states that a <see cref="BayeuxClient"/> may assume.
	/// </summary>
	[Flags]
	public enum BayeuxClientStates : int
	{
		/// <summary>
		/// Invalid Bayeux client state.
		/// </summary>
		None = 0,

		/// <summary>
		/// State assumed after the handshake when the connection is broken.
		/// </summary>
		Unconnected = 1,

		/// <summary>
		/// State assumed when the handshake is being sent.
		/// </summary>
		Handshaking = 2,

		/// <summary>
		/// State assumed when a first handshake failed and the handshake is retried,
		/// or when the Bayeux server requests a re-handshake.
		/// </summary>
		ReHandshaking = 4,

		/// <summary>
		/// State assumed when the connect is being sent for the first time.
		/// </summary>
		Connecting = /*Handshaking | */8,

		/// <summary>
		/// State assumed when this <see cref="BayeuxClient"/> is connected to the Bayeux server.
		/// </summary>
		Connected = /*Connecting | */16,

		/// <summary>
		/// State assumed when the disconnect is being sent.
		/// </summary>
		Disconnecting = 32,

		/// <summary>
		/// State assumed before the handshake and when the disconnect is completed.
		/// </summary>
		Disconnected = /*Disconnecting | */64
	}

}
