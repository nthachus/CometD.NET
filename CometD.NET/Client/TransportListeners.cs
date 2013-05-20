using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using CometD.Bayeux;
using CometD.Client.Transport;

namespace CometD.Client
{
	/// <summary>
	/// The implementation of a listener for publishing messages on a <see cref="ITransport"/>.
	/// </summary>
	public class PublishTransportListener : ITransportListener
	{
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
		/// Initializes a new instance of the <see cref="PublishTransportListener"/> class.
		/// </summary>
		/// <param name="session">The Bayeux client session.</param>
		public PublishTransportListener(BayeuxClient session)
		{
			if (null == session)
				throw new ArgumentNullException("session");

			_session = session;
		}

		/// <summary>
		/// Callback method invoked when the given messages have hit the network towards the Bayeux server.
		/// </summary>
		/// <remarks>
		/// The messages may not be modified, and any modification will be useless
		/// because the message have already been sent.
		/// </remarks>
		/// <param name="messages">The messages sent.</param>
		public virtual void OnSending(IMessage[] messages)
		{
			_session.OnSending(messages);
		}

		/// <summary>
		/// Callback method invoke when the given messages have just arrived from the Bayeux server.
		/// </summary>
		/// <param name="messages">The messages arrived.</param>
		public virtual void OnMessages(IList<IMutableMessage> messages)
		{
			_session.OnMessages(messages);

			if (null != messages)
			{
				for (int i = 0; i < messages.Count; i++)
					this.ProcessMessage(messages[i]);
			}
		}

		/// <summary>
		/// Callback method invoked when the given messages have failed to be sent
		/// because of a HTTP connection exception.
		/// </summary>
		/// <param name="ex">The exception that caused the failure.</param>
		/// <param name="messages">The messages being sent.</param>
		public virtual void OnConnectException(Exception ex, IMessage[] messages)
		{
			this.OnFailure(ex, messages);
		}

		/// <summary>
		/// Callback method invoked when the given messages have failed to be sent
		/// because of a Web exception.
		/// </summary>
		/// <param name="ex">The exception that caused the failure.</param>
		/// <param name="messages">The messages being sent.</param>
		public virtual void OnException(Exception ex, IMessage[] messages)
		{
			this.OnFailure(ex, messages);
		}

		/// <summary>
		/// Callback method invoked when the given messages have failed to be sent
		/// because of a HTTP request timeout.
		/// </summary>
		/// <param name="messages">The messages being sent.</param>
		public virtual void OnExpire(IMessage[] messages)
		{
			this.OnFailure(new TimeoutException("The timeout period for the client transport expired."), messages);
		}

		/// <summary>
		/// Callback method invoked when the given messages have failed to be sent
		/// because of an unexpected Bayeux server exception was thrown.
		/// </summary>
		/// <param name="info">Bayeux server error message.</param>
		/// <param name="ex">The exception that caused the failure.</param>
		/// <param name="messages">The messages being sent.</param>
		public virtual void OnProtocolError(string info, Exception ex, IMessage[] messages)
		{
			Exception pex = new ProtocolViolationException(info);
			if (ex != null)
			{
				FieldInfo f = pex.GetType().GetField("_innerException",
					BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.NonPublic);
				if (f != null) f.SetValue(pex, ex);
			}

			this.OnFailure(pex, messages);
		}

		/// <summary>
		/// Receives a message (from the server) and process it.
		/// </summary>
		/// <param name="message">The mutable version of the message received.</param>
		protected virtual void ProcessMessage(IMutableMessage message)
		{
			if (null != message)
				_session.ProcessMessage(message);
		}

		/// <summary>
		/// Callback method invoked when the given messages have failed to be sent.
		/// </summary>
		/// <param name="ex">The exception that caused the failure.</param>
		/// <param name="messages">The messages being sent.</param>
		protected virtual void OnFailure(Exception ex, IMessage[] messages)
		{
			_session.OnFailure(ex, messages);
			_session.FailMessages(ex, messages);
		}
	}

	/// <summary>
	/// The implementation of a listener for handshaking messages on a <see cref="ITransport"/>.
	/// </summary>
	public class HandshakeTransportListener : PublishTransportListener
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HandshakeTransportListener"/> class.
		/// </summary>
		public HandshakeTransportListener(BayeuxClient session) : base(session) { }

		/// <summary>
		/// Try to re-handshake on failure.
		/// </summary>
		protected override void OnFailure(Exception ex, IMessage[] messages)
		{
			this.Session.UpdateBayeuxClientState(oldState =>
			{
				if (null != oldState)
				{
					IList<ClientTransport> transports = this.Session.NegotiateAllowedTransports();
					if (null == transports || transports.Count == 0)
					{
						return new DisconnectedState(this.Session, oldState.Transport);
					}
					else
					{
						ClientTransport newTransport = transports[0];
						if (newTransport != null && !newTransport.Equals(oldState.Transport))
						{
							if (null != oldState.Transport)
								oldState.Transport.Reset();
							newTransport.Init();
						}

						return new ReHandshakingState(this.Session, oldState.HandshakeFields, newTransport, oldState.NextBackOff);
					}
				}

				return null;
			});

			base.OnFailure(ex, messages);
		}

		/// <summary>
		/// Processes the handshaking message have just arrived.
		/// </summary>
		protected override void ProcessMessage(IMutableMessage message)
		{
			if (message != null
				&& Channel.MetaHandshake.Equals(message.Channel, StringComparison.OrdinalIgnoreCase))
			{
				this.Session.ProcessHandshake(message);
			}
			else
			{
				base.ProcessMessage(message);
			}
		}
	}

	/// <summary>
	/// The implementation of a listener for connecting messages on a <see cref="ITransport"/>.
	/// </summary>
	public class ConnectTransportListener : PublishTransportListener
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectTransportListener"/> class.
		/// </summary>
		public ConnectTransportListener(BayeuxClient session) : base(session) { }

		/// <summary>
		/// Try to re-connect on failure.
		/// </summary>
		protected override void OnFailure(Exception ex, IMessage[] messages)
		{
			this.Session.UpdateBayeuxClientState(oldState =>
			{
				return (null == oldState) ? null
					: new UnconnectedState(this.Session, oldState.HandshakeFields,
						oldState.Advice, oldState.Transport, oldState.ClientId, oldState.NextBackOff);
			});

			base.OnFailure(ex, messages);
		}

		/// <summary>
		/// Processes the connecting message have just arrived.
		/// </summary>
		protected override void ProcessMessage(IMutableMessage message)
		{
			if (message != null
				&& Channel.MetaConnect.Equals(message.Channel, StringComparison.OrdinalIgnoreCase))
			{
				this.Session.ProcessConnect(message);
			}
			else
			{
				base.ProcessMessage(message);
			}
		}
	}

	/// <summary>
	/// The implementation of a listener for disconnecting messages on a <see cref="ITransport"/>.
	/// </summary>
	public class DisconnectTransportListener : PublishTransportListener
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DisconnectTransportListener"/> class.
		/// </summary>
		public DisconnectTransportListener(BayeuxClient session) : base(session) { }

		/// <summary>
		/// Terminates the Bayeux client session.
		/// </summary>
		protected override void OnFailure(Exception ex, IMessage[] messages)
		{
			this.Session.UpdateBayeuxClientState(oldState =>
			{
				return (null == oldState) ? null : new DisconnectedState(this.Session, oldState.Transport);
			});

			base.OnFailure(ex, messages);
		}

		/// <summary>
		/// Processes the disconnecting message have just arrived.
		/// </summary>
		protected override void ProcessMessage(IMutableMessage message)
		{
			if (message != null
				&& Channel.MetaDisconnect.Equals(message.Channel, StringComparison.OrdinalIgnoreCase))
			{
				this.Session.ProcessDisconnect(message);
			}
			else
			{
				base.ProcessMessage(message);
			}
		}
	}

}
