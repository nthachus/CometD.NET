using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

using CometD.Bayeux;
using CometD.Common;
using CometD.Client.Transport;

namespace CometD.Client
{
	/// <summary>
	/// Represents the state of a Bayeux client session.
	/// </summary>
	/// <seealso cref="BayeuxClientStates"/>
	public abstract class BayeuxClientState
	{
		private readonly BayeuxClientStates _type;
		private readonly IDictionary<string, object> _handshakeFields;
		private readonly IDictionary<string, object> _advice;
		private readonly ClientTransport _transport;
		private readonly string _clientId;
		private readonly long _backOff;

		#region Properties

		/// <summary>
		/// This client session state type (PK).
		/// </summary>
		public virtual BayeuxClientStates Type
		{
			get { return _type; }
		}

		/// <summary>
		/// The handshaking message template.
		/// </summary>
		public virtual IDictionary<string, object> HandshakeFields
		{
			get { return _handshakeFields; }
		}

		/// <summary>
		/// The last connecting message advices.
		/// </summary>
		public virtual IDictionary<string, object> Advice
		{
			get { return _advice; }
		}

		/// <summary>
		/// The current Bayeux client session transport.
		/// </summary>
		public virtual ClientTransport Transport
		{
			get { return _transport; }
		}

		/// <summary>
		/// The last connected Bayeux client ID.
		/// </summary>
		public virtual string ClientId
		{
			get { return _clientId; }
		}

		/// <summary>
		/// The scheduler action extra delay time (in milliseconds).
		/// </summary>
		protected virtual long BackOff
		{
			get { return _backOff; }
		}

		/// <summary>
		/// The scheduler action delay time (in milliseconds).
		/// </summary>
		protected virtual long Interval
		{
			get
			{
				object val;
				if (_advice != null && _advice.TryGetValue(Message.IntervalField, out val))
					return ObjectConverter.ToPrimitive<long>(val, 0);

				return 0;
			}
		}

		/// <summary>
		/// The next scheduler action back-off time.
		/// </summary>
		public virtual long NextBackOff
		{
			get
			{
				return Math.Min(this.BackOff + _session.BackOffIncrement, _session.MaxBackOff);
			}
		}


		/// <summary>
		/// Determines whether this client session state is handshaking or not.
		/// </summary>
		public virtual bool IsHandshaking
		{
			get
			{
				return ((_type & (BayeuxClientStates.Handshaking
					| BayeuxClientStates.ReHandshaking)) > 0);
			}
		}

		/// <summary>
		/// Determines whether this client session state is handshook or not.
		/// </summary>
		public virtual bool IsHandshook
		{
			get
			{
				return ((_type & (BayeuxClientStates.Connecting
					| BayeuxClientStates.Connected
					| BayeuxClientStates.Unconnected)) > 0);
			}
		}

		/// <summary>
		/// Determines whether this client session state is connecting or not.
		/// </summary>
		public virtual bool IsConnecting
		{
			get
			{
				return ((_type & BayeuxClientStates.Connecting) > 0);
			}
		}

		/// <summary>
		/// Determines whether this client session state is connected or not.
		/// </summary>
		public virtual bool IsConnected
		{
			get
			{
				return ((_type & BayeuxClientStates.Connected) > 0);
			}
		}

		/// <summary>
		/// Determines whether this client session state is disconnecting or not.
		/// </summary>
		public virtual bool IsDisconnecting
		{
			get
			{
				return ((_type & BayeuxClientStates.Disconnecting) > 0);
			}
		}

		/// <summary>
		/// Determines whether this client session state is disconnected or not.
		/// </summary>
		public virtual bool IsDisconnected
		{
			get
			{
				return ((_type & BayeuxClientStates.Disconnected) > 0);
			}
		}

		#endregion

		[NonSerialized]
		private readonly BayeuxClient _session;

		/// <summary>
		/// The Bayeux client session.
		/// </summary>
		protected virtual BayeuxClient Session
		{
			get { return _session; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BayeuxClientState"/> class
		/// with the specified <see cref="BayeuxClientStates"/> type.
		/// </summary>
		protected BayeuxClientState(
			BayeuxClient session,
			BayeuxClientStates type,
			IDictionary<string, object> handshakeFields,
			IDictionary<string, object> advice,
			ClientTransport transport,
			string clientId,
			long backOff)
		{
			if (session == null)
				throw new ArgumentNullException("session");
			if (type == BayeuxClientStates.None)
				throw new ArgumentOutOfRangeException("type");
			if (transport == null)
				throw new ArgumentNullException("transport");

			_session = session;

			_type = type;
			_handshakeFields = handshakeFields;
			_advice = advice;
			_transport = transport;
			_clientId = clientId;
			_backOff = backOff;
		}

		/// <summary>
		/// Sends the specified messages to a Bayeux server asynchronously.
		/// </summary>
		/// <param name="listener">The listener used to process the request response.</param>
		/// <param name="messages">The list of messages will be sent in one request.</param>
		public virtual void Send(ITransportListener listener, params IMutableMessage[] messages)
		{
			if (messages == null)
				throw new ArgumentNullException("messages");

			List<IMutableMessage> validMessages = new List<IMutableMessage>();
			foreach (IMutableMessage message in messages)
			{
				if (message != null)
				{
					string msgId = message.Id;
					if (String.IsNullOrEmpty(msgId))
					{
						msgId = _session.NewMessageId();
						message.Id = msgId;
					}

					if (!String.IsNullOrEmpty(_clientId))// TODO: && String.IsNullOrEmpty(message.ClientId)
						message.ClientId = _clientId;

					if (_session.ExtendSend(message))
					{
						// Extensions may have modified the messageId, but we need to own
						// the messageId in case of meta messages to link request/response
						// in non request/response transports such as websocket
						message.Id = msgId;

						validMessages.Add(message);
					}
				}
			}

			if (validMessages.Count > 0)
			{
				// DEBUG
				Debug.Print("Sending messages: {0}", ObjectConverter.Serialize(validMessages));

				_transport.Send(listener, validMessages.ToArray());
			}
		}

		/// <summary>
		/// Checks if this client session state can be updated to the specified new state.
		/// </summary>
		public abstract bool IsUpdateableTo(BayeuxClientState newState);

		/// <summary>
		/// Callback invoked when the state changed from the given <paramref name="oldState"/>
		/// to this state (and only when the two states are different).
		/// </summary>
		/// <param name="oldState">The previous state.</param>
		/// <seealso cref="Execute()"/>
		public virtual void Enter(BayeuxClientStates oldState)
		{
		}

		/// <summary>
		/// Callback invoked when this state becomes the new state,
		/// even if the previous state was equal to this state.
		/// </summary>
		/// <seealso cref="Enter(BayeuxClientStates)"/>
		public abstract void Execute();

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
			lock (_transport)
			{
				return String.Format(CultureInfo.InvariantCulture,
					"{{{0}  Type: '{1}',{0}  HandshakeFields: {2},{0}  Advice: {3},{0}  Transport: {4},{0}  ClientId: '{5}',{0}  BackOff: {6}{0}}}",
					Environment.NewLine, _type.ToString(), ObjectConverter.Serialize(_handshakeFields), ObjectConverter.Serialize(_advice),
					_transport.ToString().Replace(Environment.NewLine, Environment.NewLine + "  "), _clientId, _backOff);
			}
		}
	}

	/// <summary>
	/// Represents the disconnected state of a Bayeux client session.
	/// </summary>
	public class DisconnectedState : BayeuxClientState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DisconnectedState"/> class.
		/// </summary>
		public DisconnectedState(BayeuxClient session, ClientTransport transport)
			: base(session, BayeuxClientStates.Disconnected, null, null, transport, null, 0)
		{
		}

		/// <summary>
		/// This state can be updated to <see cref="BayeuxClientStates.Handshaking"/> state only.
		/// </summary>
		public override bool IsUpdateableTo(BayeuxClientState newState)
		{
			return (newState != null
				&& (newState.Type & BayeuxClientStates.Handshaking) > 0);
		}

		/// <summary>
		/// Cancels all queued messages and terminates the Bayeux client session.
		/// </summary>
		public override void Execute()
		{
			this.Transport.Reset();
			this.Session.Terminate();
		}
	}

	/// <summary>
	/// Represents the aborted state of a Bayeux client session.
	/// </summary>
	public class AbortedState : DisconnectedState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AbortedState"/> class.
		/// </summary>
		public AbortedState(BayeuxClient session, ClientTransport transport)
			: base(session, transport) { }

		/// <summary>
		/// Cancels all available HTTP requests and terminates the Bayeux client session.
		/// </summary>
		public override void Execute()
		{
			this.Transport.Abort();
			base.Execute();
		}
	}

	/// <summary>
	/// Represents the handshaking state of a Bayeux client session.
	/// </summary>
	public class HandshakingState : BayeuxClientState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HandshakingState"/> class
		/// with the specified handshaking message template: <paramref name="handshakeFields"/>.
		/// </summary>
		public HandshakingState(
			BayeuxClient session,
			IDictionary<string, object> handshakeFields,
			ClientTransport transport)
			: base(session, BayeuxClientStates.Handshaking, handshakeFields, null, transport, null, 0)
		{
		}

		/// <summary>
		/// This state can be updated to <see cref="BayeuxClientStates.Connecting"/>,
		/// or <see cref="BayeuxClientStates.ReHandshaking"/>, or <see cref="BayeuxClientStates.Disconnected"/> state only.
		/// </summary>
		public override bool IsUpdateableTo(BayeuxClientState newState)
		{
			return (newState != null
				&& (newState.Type & (BayeuxClientStates.Connecting
					| BayeuxClientStates.ReHandshaking
					| BayeuxClientStates.Disconnected)) > 0);
		}

		/// <summary>
		/// Always reset the subscriptions when a handshake has been requested.
		/// </summary>
		public override void Enter(BayeuxClientStates oldState)
		{
			// DEBUG
			Trace.TraceInformation("Subscriptions will be cleaned when old-state '{0}' -enter-> Handshaking state", oldState.ToString());

			this.Session.ResetSubscriptions();
		}

		/// <summary>
		/// The state could change between now and when <see cref="BayeuxClient.SendHandshake()"/> runs;
		/// in this case the handshake message will not be sent and will not be failed,
		/// because most probably the client has been disconnected.
		/// </summary>
		public override void Execute()
		{
			this.Session.SendHandshake();
		}
	}

	/// <summary>
	/// Represents the re-handshaking state of a Bayeux client session.
	/// </summary>
	public class ReHandshakingState : BayeuxClientState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ReHandshakingState"/> class
		/// with the specified handshaking message template: <paramref name="handshakeFields"/>.
		/// </summary>
		public ReHandshakingState(
			BayeuxClient session,
			IDictionary<string, object> handshakeFields,
			ClientTransport transport,
			long backOff)
			: base(session, BayeuxClientStates.ReHandshaking, handshakeFields, null, transport, null, backOff)
		{
		}

		/// <summary>
		/// This state can be updated to <see cref="BayeuxClientStates.Connecting"/>,
		/// or <see cref="BayeuxClientStates.ReHandshaking"/>, or <see cref="BayeuxClientStates.Disconnected"/> state only.
		/// </summary>
		public override bool IsUpdateableTo(BayeuxClientState newState)
		{
			return (newState != null
				&& (newState.Type & (BayeuxClientStates.Connecting
					| BayeuxClientStates.ReHandshaking
					| BayeuxClientStates.Disconnected)) > 0);
		}

		/// <summary>
		/// Reset the subscriptions if this is not a failure from a requested handshake.
		/// Subscriptions may be queued after requested handshakes.
		/// </summary>
		public override void Enter(BayeuxClientStates oldState)
		{
			if ((oldState & BayeuxClientStates.Handshaking) == 0)
			{
				// DEBUG
				Trace.TraceInformation("Subscriptions will be cleaned when old-state '{0}' -enter-> ReHandshaking state", oldState.ToString());

				this.Session.ResetSubscriptions();
			}
		}

		/// <summary>
		/// Try to re-handshake to the Bayeux server after the delay:
		/// <see cref="BayeuxClientState.Interval"/> + <see cref="BayeuxClientState.BackOff"/>.
		/// </summary>
		public override void Execute()
		{
			this.Session.ScheduleHandshake(this.Interval, this.BackOff);
		}
	}

	/// <summary>
	/// Represents the connecting state of a Bayeux client session.
	/// </summary>
	public class ConnectingState : BayeuxClientState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectingState"/> class
		/// with the specified handshaking message template: <paramref name="handshakeFields"/>,
		/// and the last received information from a Bayeux server like: <paramref name="advice"/>, <paramref name="clientId"/>.
		/// </summary>
		public ConnectingState(
			BayeuxClient session,
			IDictionary<string, object> handshakeFields,
			IDictionary<string, object> advice,
			ClientTransport transport,
			string clientId)
			: base(session, BayeuxClientStates.Connecting, handshakeFields, advice, transport, clientId, 0)
		{
		}

		/// <summary>
		/// This state can be updated to <see cref="BayeuxClientStates.Connected"/>,
		/// or <see cref="BayeuxClientStates.Unconnected"/>, or <see cref="BayeuxClientStates.ReHandshaking"/>,
		/// or <see cref="BayeuxClientStates.Disconnecting"/>, or <see cref="BayeuxClientStates.Disconnected"/> state only.
		/// </summary>
		public override bool IsUpdateableTo(BayeuxClientState newState)
		{
			return (newState != null
				&& (newState.Type & (BayeuxClientStates.Connected
					| BayeuxClientStates.Unconnected
					| BayeuxClientStates.ReHandshaking
					| BayeuxClientStates.Disconnecting
					| BayeuxClientStates.Disconnected)) > 0);
		}

		/// <summary>
		/// Send the messages that may have queued up before the handshake completed.
		/// </summary>
		public override void Execute()
		{
			this.Session.SendBatch();
			this.Session.ScheduleConnect(this.Interval, this.BackOff);
		}
	}

	/// <summary>
	/// Represents the connected state of a Bayeux client session.
	/// </summary>
	public class ConnectedState : BayeuxClientState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectedState"/> class
		/// with the specified handshaking message template: <paramref name="handshakeFields"/>,
		/// and the last received information from a Bayeux server like: <paramref name="advice"/>, <paramref name="clientId"/>.
		/// </summary>
		public ConnectedState(
			BayeuxClient session,
			IDictionary<string, object> handshakeFields,
			IDictionary<string, object> advice,
			ClientTransport transport,
			string clientId)
			: base(session, BayeuxClientStates.Connected, handshakeFields, advice, transport, clientId, 0)
		{
		}

		/// <summary>
		/// This state can be updated to <see cref="BayeuxClientStates.Connected"/>,
		/// or <see cref="BayeuxClientStates.Unconnected"/>, or <see cref="BayeuxClientStates.ReHandshaking"/>,
		/// or <see cref="BayeuxClientStates.Disconnecting"/>, or <see cref="BayeuxClientStates.Disconnected"/> state only.
		/// </summary>
		public override bool IsUpdateableTo(BayeuxClientState newState)
		{
			return (newState != null
				&& (newState.Type & (BayeuxClientStates.Connected
					| BayeuxClientStates.Unconnected
					| BayeuxClientStates.ReHandshaking
					| BayeuxClientStates.Disconnecting
					| BayeuxClientStates.Disconnected)) > 0);
		}

		/// <summary>
		/// Schedule re-connect to the Bayeux server after the delay:
		/// <see cref="BayeuxClientState.Interval"/> + <see cref="BayeuxClientState.BackOff"/>
		/// to keep the connection persistently.
		/// </summary>
		public override void Execute()
		{
			// http://www.salesforce.com/us/developer/docs/api_streaming/Content/limits.htm
			// TODO: Timeout to reconnect after successful connection (Keep-Alive) = 40 seconds
			this.Session.ScheduleConnect(this.Interval, this.BackOff);
		}
	}

	/// <summary>
	/// Represents the unconnected (re-connecting) state of a Bayeux client session.
	/// </summary>
	public class UnconnectedState : BayeuxClientState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="UnconnectedState"/> class
		/// with the specified handshaking message template: <paramref name="handshakeFields"/>,
		/// and the last received information from a Bayeux server like: <paramref name="advice"/>, <paramref name="clientId"/>.
		/// </summary>
		public UnconnectedState(
			BayeuxClient session,
			IDictionary<string, object> handshakeFields,
			IDictionary<string, object> advice,
			ClientTransport transport,
			string clientId,
			long backOff)
			: base(session, BayeuxClientStates.Unconnected, handshakeFields, advice, transport, clientId, backOff)
		{
		}

		/// <summary>
		/// This state can be updated to <see cref="BayeuxClientStates.Connected"/>,
		/// or <see cref="BayeuxClientStates.Unconnected"/>, or <see cref="BayeuxClientStates.ReHandshaking"/>,
		/// or <see cref="BayeuxClientStates.Disconnected"/> state only.
		/// </summary>
		public override bool IsUpdateableTo(BayeuxClientState newState)
		{
			return (newState != null
				&& (newState.Type & (BayeuxClientStates.Connected
					| BayeuxClientStates.Unconnected
					| BayeuxClientStates.ReHandshaking
					| BayeuxClientStates.Disconnected)) > 0);
		}

		/// <summary>
		/// Try to re-connect to the Bayeux server after the delay:
		/// <see cref="BayeuxClientState.Interval"/> + <see cref="BayeuxClientState.BackOff"/>.
		/// </summary>
		public override void Execute()
		{
			this.Session.ScheduleConnect(this.Interval, this.BackOff);
		}
	}

	/// <summary>
	/// Represents the disconnecting state of a Bayeux client session.
	/// </summary>
	public class DisconnectingState : BayeuxClientState
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DisconnectingState"/> class
		/// for the specified <paramref name="clientId"/>.
		/// </summary>
		public DisconnectingState(BayeuxClient session, ClientTransport transport, string clientId)
			: base(session, BayeuxClientStates.Disconnecting, null, null, transport, clientId, 0) { }

		/// <summary>
		/// This state can be updated to <see cref="BayeuxClientStates.Disconnected"/> state only.
		/// </summary>
		public override bool IsUpdateableTo(BayeuxClientState newState)
		{
			return (newState != null
				&& (newState.Type & BayeuxClientStates.Disconnected) > 0);
		}

		/// <summary>
		/// Sends a new disconnecting command to the Bayeux server.
		/// </summary>
		public override void Execute()
		{
			IMutableMessage message = this.Session.NewMessage();
			message.Channel = Channel.MetaDisconnect;

			this.Send(this.Session.DisconnectListener, message);
		}
	}

}
