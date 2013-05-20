using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;

using CometD.Bayeux;
using CometD.Bayeux.Client;
using CometD.Common;
using CometD.Client.Transport;

namespace CometD.Client
{
	/// <summary>
	/// <p>This class is the implementation of a client for the Bayeux protocol.</p>
	/// <p>A <see cref="BayeuxClient"/> can receive/publish messages from/to a Bayeux server,
	/// and it is the counterpart in Java of the JavaScript library used in browsers
	/// (and as such it is ideal for Swing applications, load testing tools, etc.).</p>
	/// <p>A <see cref="BayeuxClient"/> handshakes with a Bayeux server
	/// and then subscribes <see cref="IMessageListener"/> to channels in order
	/// to receive messages, and may also publish messages to the Bayeux server.</p>
	/// <p><see cref="BayeuxClient"/> relies on pluggable transports for communication with the Bayeux
	/// server, and the most common transport is <see cref="LongPollingTransport"/>,
	/// which uses HTTP to transport Bayeux messages and it is based on
	/// <a href="http://wiki.eclipse.org/Jetty/Feature/HttpClient">Jetty's HTTP client</a>.</p>
	/// <p>When the communication with the server is finished,
	/// the <see cref="BayeuxClient"/> can be disconnected from the Bayeux server.</p>
	/// </summary>
	/// <example>
	/// <p>Typical usage:</p>
	/// <pre>
	/// // Handshake
	/// string url = "http://localhost:8080/cometd";
	/// BayeuxClient client = new BayeuxClient(url, new LongPollingTransport(null, null));
	/// client.Handshake();
	/// client.WaitFor(1000, BayeuxClientStates.Connected);
	///
	/// // Subscription to channels
	/// IClientSessionChannel channel = client.GetChannel("/foo");
	/// channel.Subscribe(new IMessageListener() {
	///     public void OnMessage(IClientSessionChannel channel, IMessage message) {
	///         // Handle the message
	///     }
	/// });
	///
	/// // Publishing to channels
	/// IDictionary&lt;string, object&gt; data = new Dictionary&lt;string, object&gt;();
	/// data["bar"] = "baz";
	/// channel.Publish(data);
	///
	/// // Disconnecting
	/// client.Disconnect();
	/// client.WaitFor(1000, BayeuxClientStates.Disconnected);
	/// </pre>
	/// </example>
	public partial class BayeuxClient : AbstractClientSession, IBayeux, IDisposable
	{
		#region Constants

		/// <summary>
		/// The Bayeux client option "backOffIncrement".
		/// </summary>
		public const string BackOffIncrementOption = "backOffIncrement";

		/// <summary>
		/// The Bayeux client option "maxBackOff".
		/// </summary>
		public const string MaxBackOffOption = "maxBackOff";

		/// <summary>
		/// Constant representing the Bayeux protocol version "1.0".
		/// </summary>
		public const string BayeuxVersion = "1.0";

		#endregion

		/// <summary>Used to debug.</summary>
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BayeuxClient));

		private readonly TransportRegistry _transportRegistry = new TransportRegistry();
		private readonly IDictionary<string, object> _options
			= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		private volatile BayeuxClientState _bayeuxClientState;// readonly AtomicReference<BayeuxClientState>
		private readonly IList<IMutableMessage> _messagesQueue = new List<IMutableMessage>(32);
		private readonly CookieCollection _cookieProvider = new CookieCollection();

		#region Transport Listeners

		private readonly ITransportListener _handshakeListener;
		private readonly ITransportListener _connectListener;
		private readonly ITransportListener _disconnectListener;
		private readonly ITransportListener _publishListener;

		/// <summary>
		/// Refers to an instance of the class <see cref="DisconnectTransportListener"/>.
		/// </summary>
		public virtual ITransportListener DisconnectListener
		{
			get { return _disconnectListener; }
		}

		#endregion

		private /*volatile*/ long _backOffIncrement;
		private /*volatile*/ long _maxBackOff;

		/// <summary>
		/// <p>Creates a <see cref="BayeuxClient"/> that will connect to the Bayeux server
		/// at the given URL and with the given transport(s).</p>
		/// <p>This constructor allocates a new scheduler; it is recommended that
		/// when creating a large number of <see cref="BayeuxClient"/>s a shared scheduler is used.</p>
		/// </summary>
		/// <param name="url">The Bayeux server URL to connect to.</param>
		/// <param name="transports">The default (mandatory) and additional optional transports to use.</param>
		public BayeuxClient(string url, params ClientTransport[] transports)
		{
			if (transports == null || transports.Length == 0 || transports[0] == null)
				throw new ArgumentNullException("transports");

			foreach (ClientTransport t in transports)
				_transportRegistry.Add(t);

			HttpClientTransport clientTransport;
			foreach (string transportName in _transportRegistry.KnownTransports)
			{
				clientTransport = _transportRegistry.GetTransport(transportName) as HttpClientTransport;
				if (clientTransport != null)
				{
					if (!String.IsNullOrEmpty(url))
						clientTransport.Url = url;
					clientTransport.CookieProvider = _cookieProvider;
				}
			}

			_handshakeListener = new HandshakeTransportListener(this);
			_connectListener = new ConnectTransportListener(this);
			_disconnectListener = new DisconnectTransportListener(this);
			_publishListener = new PublishTransportListener(this);

#pragma warning disable 0420
			Interlocked.Exchange<BayeuxClientState>(ref _bayeuxClientState, new DisconnectedState(this, transports[0]));
#pragma warning restore 0420
		}

		/// <summary>
		/// Returns the period of time that increments the pause to wait before trying to reconnect
		/// after each failed attempt to connect to the Bayeux server.
		/// </summary>
		/// <seealso cref="MaxBackOff"/>
		public virtual long BackOffIncrement
		{
			get { return Interlocked.Read(ref _backOffIncrement); }
		}

		/// <summary>
		/// Returns the maximum pause to wait before trying to reconnect after each failed attempt
		/// to connect to the Bayeux server.
		/// </summary>
		/// <seealso cref="BackOffIncrement"/>
		public virtual long MaxBackOff
		{
			get { return Interlocked.Read(ref _maxBackOff); }
		}

		/// <summary>
		/// Returns the options that configure with <see cref="BayeuxClient"/>.
		/// </summary>
		public virtual IDictionary<string, object> Options
		{
			get { return _options; }// TODO: UnmodifiableMap
		}

		#region Cookies Management

		/// <summary>
		/// Retrieves the cookie with the given name, if available.
		/// </summary>
		/// <remarks>Note that currently only HTTP transports support cookies.</remarks>
		/// <param name="name">The cookie name.</param>
		/// <returns>The cookie value.</returns>
		/// <seealso cref="SetCookie(string, string)"/>
		public virtual string GetCookie(string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			Cookie cookie = _cookieProvider[name];
			if (cookie != null)
				return cookie.Value;

			return null;
		}

		/// <summary>
		/// Sets a cookie that never expires.
		/// </summary>
		/// <param name="name">The cookie name.</param>
		/// <param name="value">The cookie value.</param>
		/// <seealso cref="SetCookie(string, string, int)"/>
		public virtual void SetCookie(string name, string value)
		{
			this.SetCookie(name, value, -1);
		}

		/// <summary>
		/// Sets a cookie with the given max age in seconds.
		/// </summary>
		/// <param name="name">The cookie name.</param>
		/// <param name="value">The cookie value.</param>
		/// <param name="maxAge">The max age of the cookie, in seconds, before expiration.</param>
		public virtual void SetCookie(string name, string value, int maxAge)
		{
			Cookie cookie = new Cookie(name, value, null, null);
			if (maxAge >= 0)
				cookie.Expires = DateTime.Now.AddSeconds(maxAge);

			lock (_cookieProvider) _cookieProvider.Add(cookie);
		}

		#endregion

		#region IBayeux Members

		/// <summary>
		/// Returns unmodifiable collection of known transports.
		/// </summary>
		public virtual ICollection<string> KnownTransportNames
		{
			get { return _transportRegistry.KnownTransports; }
		}

		/// <summary>
		/// Returns a registered <see cref="ITransport"/> within this <see cref="BayeuxClient"/>.
		/// </summary>
		/// <param name="transport">The transport name.</param>
		/// <returns>Return null if the <paramref name="transport"/> did not registered.</returns>
		public virtual ITransport GetTransport(string transport)
		{
			if (String.IsNullOrEmpty(transport))
				throw new ArgumentNullException("transport");

			return _transportRegistry.GetTransport(transport);
		}

		/// <summary>
		/// Returns unmodifiable list of allowed transports.
		/// </summary>
		public virtual ICollection<string> AllowedTransports
		{
			get { return _transportRegistry.AllowedTransports; }
		}

		/// <summary>
		/// Gets the configuration option with the given <paramref name="qualifiedName"/>.
		/// </summary>
		/// <param name="qualifiedName">The configuration option name.</param>
		/// <seealso cref="SetOption(string, object)"/>
		/// <seealso cref="OptionNames"/>
		public virtual object GetOption(string qualifiedName)
		{
			if (String.IsNullOrEmpty(qualifiedName))
				throw new ArgumentNullException("qualifiedName");

			object val;
			return _options.TryGetValue(qualifiedName, out val) ? val : null;
		}

		/// <summary>
		/// Sets the specified configuration option with the given <paramref name="value"/>.
		/// </summary>
		/// <param name="qualifiedName">The configuration option name.</param>
		/// <param name="value">The configuration option value.</param>
		/// <seealso cref="GetOption(string)"/>
		public virtual void SetOption(string qualifiedName, object value)
		{
			if (String.IsNullOrEmpty(qualifiedName))
				throw new ArgumentNullException("qualifiedName");

			lock (_options) _options[qualifiedName] = value;
		}

		/// <summary>
		/// The set of configuration options.
		/// </summary>
		/// <seealso cref="GetOption(string)"/>
		public virtual ICollection<string> OptionNames
		{
			get { return _options.Keys; }
		}

		#endregion

		/// <summary>
		/// Returns a list of negotiated allowed transports.
		/// </summary>
		public virtual IList<ClientTransport> NegotiateAllowedTransports()
		{
			return _transportRegistry.Negotiate(this.AllowedTransports, BayeuxVersion);
		}

		#region AbstractClientSession Implementation Methods

		/// <summary>
		/// Equivalent to <see cref="Handshake(IDictionary&lt;string, object&gt;)"/>(null).
		/// </summary>
		public override void Handshake()
		{
			this.Handshake(null);
		}

		/// <summary>
		/// Initiates the Bayeux protocol handshake with the server(s).
		/// </summary>
		/// <param name="handshakeFields">Additional fields to add to the handshake message.</param>
		public override void Handshake(IDictionary<string, object> handshakeFields)
		{
			this.Initialize();

			// Pick the first transport for the handshake, it will renegotiate if not right
			IList<ClientTransport> clientTransports = this.NegotiateAllowedTransports();
			ClientTransport initialTransport = (null != clientTransports && clientTransports.Count > 0) ? clientTransports[0] : null;
			if (initialTransport != null)
			{
				initialTransport.Init();
				// DEBUG
				if (logger.IsDebugEnabled)
				{
					logger.DebugFormat("Using initial transport '{0}' from {1}",
						initialTransport.Name, ObjectConverter.Serialize(this.AllowedTransports));
				}

				this.UpdateBayeuxClientState(oldState =>
					{
						return new HandshakingState(this, handshakeFields, initialTransport);
					});
			}
		}

		/// <summary>
		/// Initiates BackOff properties for this client session.
		/// </summary>
		protected virtual void Initialize()
		{
			long bi = ObjectConverter.ToPrimitive<long>(this.GetOption(BackOffIncrementOption), 0);
			Interlocked.Exchange(ref _backOffIncrement, (bi > 0) ? bi : 1000L);

			long mb = ObjectConverter.ToPrimitive<long>(this.GetOption(MaxBackOffOption), 0);
			Interlocked.Exchange(ref _maxBackOff, (mb > 0) ? mb : 30000L);
		}

		/// <summary>
		/// Creates a new <see cref="ChannelId"/> object from the specified channel id string.
		/// </summary>
		protected override ChannelId NewChannelId(string channelId)
		{
			if (String.IsNullOrEmpty(channelId))
				throw new ArgumentNullException("channelId");

			// Save some parsing by checking if there is already one
			IClientSessionChannel channel = this.GetChannel(channelId, false);
			return (channel == null) ? ChannelId.Create(channelId) : channel.ChannelId;
		}

		/// <summary>
		/// Creates a new <see cref="AbstractSessionChannel"/> object from the specified channel id.
		/// </summary>
		protected override AbstractSessionChannel NewChannel(ChannelId channelId)
		{
			if (null == channelId)
				throw new ArgumentNullException("channelId");

			return new BayeuxClientChannel(this, channelId);
		}

		/// <summary>
		/// The client id of the session.
		/// </summary>
		public override string Id
		{
			get
			{
				BayeuxClientState state = _bayeuxClientState;
				return state.ClientId;
			}
		}

		/// <summary>
		/// A connected session is a session where the link between the client and the server
		/// has been established.
		/// </summary>
		public override bool IsConnected
		{
			get
			{
				BayeuxClientState state = _bayeuxClientState;
				return state.IsConnected;
			}
		}

		/// <summary>
		/// A handshook session is a session where the handshake has successfully completed.
		/// </summary>
		public override bool IsHandshook
		{
			get
			{
				BayeuxClientState state = _bayeuxClientState;
				return state.IsHandshook;
			}
		}

		/// <summary>
		/// Disconnects this session, ending the link between the client and the server peers.
		/// </summary>
		/// <seealso cref="Disconnect(int)"/>.
		public override void Disconnect()
		{
			this.UpdateBayeuxClientState(oldState =>
				{
					if (oldState.IsConnecting || oldState.IsConnected)
						return new DisconnectingState(this, oldState.Transport, oldState.ClientId);
					else if (oldState.IsDisconnecting)
						return new DisconnectingState(this, oldState.Transport, oldState.ClientId);

					return new DisconnectedState(this, oldState.Transport);
				});
		}

		/// <summary>
		/// Sends all existing messages at the end of the batch.
		/// </summary>
		public override void SendBatch()
		{
			if (this.CanSend)
			{
				IMutableMessage[] messages = this.TakeMessages();
				if (messages != null && messages.Length > 0)
					this.SendMessages(messages);
			}
		}

		/// <summary>
		/// Sends the specified messages via the current <see cref="BayeuxClientState"/>.
		/// </summary>
		protected virtual bool SendMessages(params IMutableMessage[] messages)
		{
			BayeuxClientState currState = _bayeuxClientState;
			if (currState.IsConnecting || currState.IsConnected)
			{
				currState.Send(_publishListener, messages);
				return true;
			}

			this.FailMessages(null, messages);
			return false;
		}

		/// <summary>
		/// Multiple threads can call this method concurrently (for example
		/// a batched Publish() is executed exactly when a message arrives
		/// and a listener also performs a batched Publish() in response to
		/// the message).
		/// The queue must be drained atomically, otherwise we risk that the
		/// same message is drained twice.
		/// </summary>
		public virtual IMutableMessage[] TakeMessages()
		{
			IMutableMessage[] messages;
			lock (_messagesQueue)
			{
				messages = new IMutableMessage[_messagesQueue.Count];
				_messagesQueue.CopyTo(messages, 0);

				_messagesQueue.Clear();
			}

			return messages;
		}

		#endregion

		/// <summary>
		/// Returns whether this <see cref="BayeuxClient"/> is disconnecting or disconnected.
		/// </summary>
		public virtual bool IsDisconnected
		{
			get
			{
				BayeuxClientState state = _bayeuxClientState;
				return (state.IsDisconnecting || state.IsDisconnected);
			}
		}

		/// <summary>
		/// Returns the current state of this <see cref="BayeuxClient"/>.
		/// </summary>
		protected virtual BayeuxClientStates CurrentState
		{
			get
			{
				BayeuxClientState state = _bayeuxClientState;
				return state.Type;
			}
		}

		/// <summary>
		/// Returns the <see cref="ClientTransport"/> of the current session state.
		/// </summary>
		public virtual ClientTransport CurrentTransport
		{
			get
			{
				BayeuxClientState currState = _bayeuxClientState;
				return (currState == null) ? null : currState.Transport;
			}
		}

		#region Handshake Overload Methods

		/// <summary>
		/// <p>Performs the handshake and waits at most the given time for the handshake to complete.</p>
		/// <p>When this method returns, the handshake may have failed (for example because the Bayeux
		/// server denied it), so it is important to check the return value to know whether the handshake
		/// completed or not.</p>
		/// </summary>
		/// <param name="waitMilliseconds">The time to wait for the handshake to complete.</param>
		/// <seealso cref="Handshake(IDictionary&lt;string, object&gt;, int)"/>
		public virtual bool Handshake(int waitMilliseconds)
		{
			return this.Handshake(null, waitMilliseconds);
		}

		/// <summary>
		/// <p>Performs the handshake with the given template and waits at most the given time
		/// for the handshake to complete.</p>
		/// <p>When this method returns, the handshake may have failed (for example because the Bayeux
		/// server denied it), so it is important to check the return value to know whether the handshake
		/// completed or not.</p>
		/// </summary>
		/// <param name="template">The template object to be merged with the handshake message.</param>
		/// <param name="waitMilliseconds">The time to wait for the handshake to complete.</param>
		/// <seealso cref="Handshake(int)"/>
		public virtual bool Handshake(IDictionary<string, object> template, int waitMilliseconds)
		{
			this.Handshake(template);

			return this.WaitFor(waitMilliseconds,
				BayeuxClientStates.Connecting
				| BayeuxClientStates.Connected
				| BayeuxClientStates.Disconnected);
		}

		#endregion

		private /*volatile*/ int _stateUpdaters = 0;
		private readonly AutoResetEvent _stateUpdated = new AutoResetEvent(false);
		//private static readonly object _syncRoot = new object();

		/// <summary>
		/// Waits for this <see cref="BayeuxClient"/> to reach the given state(s) within the given time.
		/// </summary>
		/// <param name="waitMilliseconds">The time to wait to reach the given state(s).</param>
		/// <param name="states">The primary and alternative states to reach.</param>
		/// <returns>True if one of the state(s) has been reached within the given time, false otherwise.</returns>
		public virtual bool WaitFor(int waitMilliseconds, BayeuxClientStates states)
		{
			if (states == BayeuxClientStates.None)
				throw new ArgumentNullException("states");

			DateTime stop = DateTime.Now.AddMilliseconds(waitMilliseconds);
			long duration;
			BayeuxClientState currState;

			//lock (_syncRoot)
			//{
			try
			{
				while (((currState = _bayeuxClientState).Type & states) == 0
					&& (duration = unchecked((long)(stop - DateTime.Now).TotalMilliseconds)) > 0
					&& _stateUpdated.WaitOne(unchecked((int)duration), false))
				{
					// This check is needed to avoid that we return from WaitFor() too early,
					// when the state has been set, but its effects (like notifying listeners)
					// are not completed yet (COMETD-212).
					// Transient states (like CONNECTING or DISCONNECTING) may "miss" the
					// wake up in this way:
					// * T1 goes in wait - releases lock
					// * T2 finishes update to CONNECTING - notifies lock
					// * T3 starts a state update to CONNECTED - releases lock
					// * T1 wakes up, takes lock, but sees update in progress, waits - releases lock
					// * T3 finishes update to CONNECTED - notifies lock
					// * T1 wakes up, takes lock, sees status == CONNECTED - CONNECTING has been "missed"
					// To avoid this, we use BayeuxClientStates.Implies()
					/*if (_stateUpdaters == 0)
					{
					currState = _bayeuxClientState;
					if ((currState.Type & states) > 0) return true;
					}*/
				}
			}
			catch (ObjectDisposedException) { }
			catch (ThreadInterruptedException/*AbandonedMutexException*/)
			{
				Thread.CurrentThread.Interrupt();
			}
			//}

			currState = _bayeuxClientState;
			return ((currState.Type & states) > 0);
		}

		#region IDisposable Members

		private volatile bool _disposed = false;

		/// <summary>
		/// Releases the unmanaged resources and disposes of the managed resources used by the <see cref="BayeuxClient" />.
		/// </summary>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="BayeuxClient" /> and optionally disposes of the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to releases only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing && !_disposed)
			{
				_disposed = true;

				_stateUpdated.Close();
			}
		}

		#endregion

		#region Disconnect Overload Methods

		private static void WaitForNoneReconnection(
			IClientSessionChannel channel, IMessage message, ManualResetEvent latch)
		{
			if (null != message && null != latch)
			{
				string action = GetAdviceAction(message.Advice, Message.ReconnectNoneValue);
				if (Message.ReconnectNoneValue.Equals(action, StringComparison.OrdinalIgnoreCase))
				{
					latch.Set();// Signal()
					// DEBUG
					logger.InfoFormat("None reconnection message was found, signal Latch#{0}", latch.GetHashCode());
				}
			}
		}

		/// <summary>
		/// <p>Performs a <see cref="Disconnect()"/> and uses the given <paramref name="timeout"/>
		/// to wait for the disconnect to complete.</p>
		/// <p>When a disconnect is sent to the server, the server also wakes up the long
		/// poll that may be outstanding, so that a connect reply message may arrive to
		/// the client later than the disconnect reply message.</p>
		/// <p>This method waits for the given <paramref name="timeout"/> for the disconnect reply, but also
		/// waits the same timeout for the last connect reply; in the worst case the
		/// maximum time waited will therefore be twice the given <paramref name="timeout"/> parameter.</p>
		/// <p>This method returns true if the disconnect reply message arrived within the
		/// given <paramref name="timeout"/> parameter, no matter if the connect reply message arrived or not.</p>
		/// </summary>
		/// <param name="timeout">The timeout to wait for the disconnect to complete.</param>
		/// <returns>True if the disconnect completed within the given timeout.</returns>
		public virtual bool Disconnect(int timeout)
		{
			BayeuxClientState currState = _bayeuxClientState;
			if (currState.IsDisconnected) return true;

			bool disconnected;
			using (ManualResetEvent latch = new ManualResetEvent(false))// TODO: CountdownEvent(1)
			{
				IMessageListener lastConnectListener
					= new CallbackMessageListener<ManualResetEvent>(WaitForNoneReconnection, latch);

				IClientSessionChannel ch = this.GetChannel(Channel.MetaConnect);
				if (ch != null) ch.AddListener(lastConnectListener);

				this.Disconnect();
				disconnected = this.WaitFor(timeout,
					BayeuxClientStates.Disconnected
					// TODO: | BayeuxClientStates.Disconnecting
					);

				// There is a possibility that we are in the window where the server
				// has returned the long poll and the client has not issued it again,
				// so wait for the timeout, but do not complain if the latch does not trigger.
				if (ch != null)
				{
					try
					{
						latch.WaitOne(timeout, false);// Wait(timeout)
					}
					catch (ThreadInterruptedException/*AbandonedMutexException*/)
					{
						Thread.CurrentThread.Interrupt();
					}
					finally
					{
						ch.RemoveListener(lastConnectListener);
					}
				}
			}

			currState = _bayeuxClientState;
			if (currState.IsDisconnected && currState.Transport != null)
				currState.Transport.Terminate();

			return disconnected;
		}

		/// <summary>
		/// <p>Interrupts abruptly the communication with the Bayeux server.</p>
		/// <p>This method may be useful to simulate network failures.</p>
		/// </summary>
		/// <seealso cref="Disconnect()"/>
		public virtual void Abort()
		{
			this.UpdateBayeuxClientState(oldState =>
				{
					return (oldState == null) ? null : new AbortedState(this, oldState.Transport);
				});
		}

		#endregion

		#region Messages Processing

		/// <summary>
		/// Sends a new handshaking command to the Bayeux server.
		/// </summary>
		public virtual bool SendHandshake()
		{
			if (_terminated) return false;

			BayeuxClientState currState = _bayeuxClientState;
			if (currState.IsHandshaking)
			{
				// Generates a new meta handshake message
				IMutableMessage message = this.NewMessage();

				// Adds extra fields into the created message
				if (currState.HandshakeFields != null)
				{
					lock (currState.HandshakeFields)
					{
						foreach (KeyValuePair<string, object> field in currState.HandshakeFields)
						{
							if (!String.IsNullOrEmpty(field.Key))
								message[field.Key] = field.Value;
						}
					}
				}

				message.Channel = Channel.MetaHandshake;

				// Gets the supported connection types
				IList<ClientTransport> transports = this.NegotiateAllowedTransports();
				if (null != transports && transports.Count > 0)
				{
					IList<string> transportNames = new List<string>(transports.Count);
					foreach (ClientTransport t in transports)
						transportNames.Add(t.Name);

					message[Message.SupportedConnectionTypesField] = transportNames;
				}

				message[Message.VersionField] = BayeuxClient.BayeuxVersion;
				//if (String.IsNullOrEmpty(message.Id))
				//	message.Id = this.NewMessageId();

				// DEBUG
				if (logger.IsDebugEnabled)
				{
					logger.DebugFormat(CultureInfo.InvariantCulture,
						"Handshaking with transport: {0}, extra fields: {1}",
						currState.Transport, ObjectConverter.Serialize(currState.HandshakeFields));
				}

				currState.Send(_handshakeListener, message);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Sends a new handshaking command to the Bayeux server.
		/// </summary>
		public virtual bool SendConnect()
		{
			if (_terminated) return false;

			BayeuxClientState currState = _bayeuxClientState;
			if (currState.IsHandshook)
			{
				// Generates a new meta connect message
				IMutableMessage message = this.NewMessage();

				message.Channel = Channel.MetaConnect;
				message[Message.ConnectionTypeField] = currState.Transport.Name;

				if (currState.IsConnecting || (currState.Type & BayeuxClientStates.Unconnected) > 0)
				{
					// First connect after handshake or after failure, add advice
					IDictionary<string, object> advice = message.GetAdvice(true);
					advice[Message.TimeoutField] = 0;
				}

				// DEBUG
				if (logger.IsDebugEnabled)
					logger.DebugFormat("Connecting, transport: {0}", currState.Transport);

				currState.Send(_connectListener, message);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Processes a handshaking message have just arrived.
		/// </summary>
		public virtual void ProcessHandshake(IMutableMessage handshake)
		{
			if (null == handshake)
				throw new ArgumentNullException("handshake");

			// DEBUG
			if (logger.IsDebugEnabled)
				logger.DebugFormat("Processing meta handshake: {0}", handshake);

			if (handshake.IsSuccessful)
			{
				object field;
				object[] serverTransports = handshake.TryGetValue(Message.SupportedConnectionTypesField, out field)
					? ObjectConverter.ToObject<object[]>(field) : null;

				IList<ClientTransport> negotiatedTransports
					= (serverTransports != null && serverTransports.Length > 0)
					? _transportRegistry.Negotiate(Array.ConvertAll<object, string>(
						serverTransports, o => (null == o) ? null : o.ToString()), BayeuxVersion) : null;

				ClientTransport newTransport = (negotiatedTransports != null && negotiatedTransports.Count > 0)
					? negotiatedTransports[0] : null;
				if (newTransport == null)
				{
					// Signal the failure
					string error = String.Format(CultureInfo.InvariantCulture, "405:c{0},s{1}:No transport",
						ObjectConverter.Serialize(this.AllowedTransports), ObjectConverter.Serialize(serverTransports));

					handshake.IsSuccessful = false;
					handshake[Message.ErrorField] = error;
					// TODO: Also update the advice with reconnect=none for listeners ?

					this.UpdateBayeuxClientState(oldState =>
						{
							return (oldState == null) ? null : new DisconnectedState(this, oldState.Transport);
						},
						() =>
						{
							this.Receive(handshake);
						});
				}
				else	// Has a valid transport ?
				{
					this.UpdateBayeuxClientState(oldState =>
						{
							if (oldState != null)
							{
								if (!newTransport.Equals(oldState.Transport))
								{
									oldState.Transport.Reset();
									newTransport.Init();
								}

								string action = GetAdviceAction(handshake.Advice, Message.ReconnectRetryValue);
								if (Message.ReconnectRetryValue.Equals(action, StringComparison.OrdinalIgnoreCase))
								{
									return new ConnectingState(this, oldState.HandshakeFields,
										handshake.Advice, newTransport, handshake.ClientId);
								}
								else if (Message.ReconnectNoneValue.Equals(action, StringComparison.OrdinalIgnoreCase))
								{
									return new DisconnectedState(this, oldState.Transport);
								}
							}

							return null;
						},
						() =>
						{
							this.Receive(handshake);
						});
				}
			}
			else	// Try to re-handshake when an error message was arrived
			{
				this.UpdateBayeuxClientState(oldState =>
					{
						if (oldState != null)
						{
							string action = GetAdviceAction(handshake.Advice, Message.ReconnectHandshakeValue);
							if (Message.ReconnectHandshakeValue.Equals(action, StringComparison.OrdinalIgnoreCase)
								|| Message.ReconnectRetryValue.Equals(action, StringComparison.OrdinalIgnoreCase))
							{
								return new ReHandshakingState(this, oldState.HandshakeFields,
									oldState.Transport, oldState.NextBackOff);
							}
							else if (Message.ReconnectNoneValue.Equals(action, StringComparison.OrdinalIgnoreCase))
							{
								return new DisconnectedState(this, oldState.Transport);
							}
						}

						return null;
					},
					() =>
					{
						this.Receive(handshake);
					});
			}
		}

		/// <summary>
		/// Processes a connecting message have just arrived.
		/// </summary>
		public virtual void ProcessConnect(IMutableMessage connect)
		{
			if (null == connect)
				throw new ArgumentNullException("connect");
			// TODO: Split "connected" state into ConnectSent + ConnectReceived ?
			// It may happen that the server replies to the meta connect with a delay
			// that exceeds the maxNetworkTimeout (for example because the server is
			// busy and the meta connect reply thread is starved).
			// In this case, it is possible that we issue 2 concurrent connects, one
			// for the response arrived late, and one from the unconnected state.
			// We should avoid this, although it is a very rare case.

			// DEBUG
			if (logger.IsDebugEnabled)
				logger.DebugFormat("Processing meta connect: {0}", connect);

			this.UpdateBayeuxClientState(oldState =>
				{
					if (oldState != null)
					{
						IDictionary<string, object> advice = connect.Advice;
						if (advice == null)
							advice = oldState.Advice;

						string action = GetAdviceAction(advice, Message.ReconnectRetryValue);
						if (connect.IsSuccessful)
						{
							if (Message.ReconnectRetryValue.Equals(action, StringComparison.OrdinalIgnoreCase))
							{
								return new ConnectedState(this, oldState.HandshakeFields,
									advice, oldState.Transport, oldState.ClientId);
							}
							else if (Message.ReconnectNoneValue.Equals(action, StringComparison.OrdinalIgnoreCase))
							{
								// This case happens when the connect reply arrives after a disconnect
								// We do not go into a disconnected state to allow normal processing of the disconnect reply
								return new DisconnectingState(this, oldState.Transport, oldState.ClientId);
							}
						}
						else	// Try to re-handshake / re-connect when an error message was arrived
						{
							if (Message.ReconnectHandshakeValue.Equals(action, StringComparison.OrdinalIgnoreCase))
							{
								return new ReHandshakingState(this, oldState.HandshakeFields, oldState.Transport, 0);
							}
							else if (Message.ReconnectRetryValue.Equals(action, StringComparison.OrdinalIgnoreCase))
							{
								return new UnconnectedState(this, oldState.HandshakeFields,
									advice, oldState.Transport, oldState.ClientId, oldState.NextBackOff);
							}
							else if (Message.ReconnectNoneValue.Equals(action, StringComparison.OrdinalIgnoreCase))
							{
								return new DisconnectedState(this, oldState.Transport);
							}
						}
					}

					return null;
				},
				() =>
				{
					this.Receive(connect);
				});
		}

		/// <summary>
		/// Processes a disconnecting message have just arrived.
		/// </summary>
		public virtual void ProcessDisconnect(IMutableMessage disconnect)
		{
			if (null == disconnect)
				throw new ArgumentNullException("disconnect");

			// DEBUG
			if (logger.IsDebugEnabled)
				logger.DebugFormat("Processing meta disconnect: {0}", disconnect);

			this.UpdateBayeuxClientState(oldState =>
				{
					return (oldState == null) ? null : new DisconnectedState(this, oldState.Transport);
				},
				() =>
				{
					this.Receive(disconnect);
				});
		}

		/// <summary>
		/// Receives a normal message.
		/// </summary>
		public virtual void ProcessMessage(IMutableMessage message)
		{
			if (null == message)
				throw new ArgumentNullException("message");

			// DEBUG
			if (logger.IsDebugEnabled)
				logger.DebugFormat("Processing message: {0}", message);

			this.Receive(message);
		}

		#endregion

		private static string GetAdviceAction(IDictionary<string, object> advice, string defaultResult)
		{
			object action;
			string result;

			if (null != advice
				&& advice.TryGetValue(Message.ReconnectField, out action)
				&& null != action
				&& (result = action.ToString().Trim()).Length > 0)
			{
				return result;
			}

			return defaultResult;
		}

		#region Scheduled Actions

		/// <summary>
		/// Try to re-handshake after the given delay (<paramref name="interval"/> + <paramref name="backOff"/>).
		/// </summary>
		public virtual bool ScheduleHandshake(long interval, long backOff)
		{
			return ScheduleAction(this.SendHandshake, interval, backOff);
		}

		/// <summary>
		/// Try to re-connect after the given delay (<paramref name="interval"/> + <paramref name="backOff"/>).
		/// </summary>
		public virtual bool ScheduleConnect(long interval, long backOff)
		{
			return ScheduleAction(this.SendConnect, interval, backOff);
		}

		/// <summary>
		/// Executes a one-shot action that becomes enabled after the given delay.
		/// </summary>
		private static bool ScheduleAction(Func<bool> action, long interval, long backOff)
		{
			// Prevent NPE in case of concurrent disconnect
			System.Timers.Timer timer = new System.Timers.Timer();
			timer.Interval = Math.Max(interval + backOff, 1);// TODO: MinInterval is 1Ms ?

			timer.Elapsed += new System.Timers.ElapsedEventHandler((source, e) =>
				{
					// Stop the Timer
					try
					{
						timer.Enabled = false;
						timer.Close();
						// DEBUG
						//logger.InfoFormat("The Timer #{0} has been disposed.", timer.GetHashCode());
					}
					catch (ObjectDisposedException) { }
					finally
					{
						action();
					}
				});
			timer.AutoReset = false;
			timer.Enabled = true;	// Start the Timer

			return true;
		}

		#endregion

		/// <summary>
		/// Used to shutdown scheduler.
		/// </summary>
		private volatile bool _terminated = false;

		/// <summary>
		/// Terminates this client session before disconnected.
		/// </summary>
		public virtual void Terminate()
		{
			if (!_terminated)
			{
				_terminated = true;

				IMutableMessage[] messages = this.TakeMessages();
				this.FailMessages(null, messages);

				lock (_cookieProvider)
				{
					// TODO: _cookieProvider.Clear();
					foreach (Cookie c in _cookieProvider)
					{
						c.Expired = true;
						c.Discard = true;
					}
				}
			}
		}

		/// <summary>
		/// Processes all sent failed messages
		/// by invoking of the receiving callback with a message that was generated from failed messages.
		/// </summary>
		public virtual void FailMessages(Exception ex, params IMessage[] messages)
		{
			if (null != messages && messages.Length > 0)
			{
				foreach (IMessage m in messages)
				{
					if (null != m)
					{
						IMutableMessage failed = this.NewMessage();
						failed.Id = m.Id;
						failed.IsSuccessful = false;
						failed.Channel = m.Channel;

						failed[Message.MessageField] = m;
						if (null != ex)
							failed[Message.ExceptionField] = ex;

						ClientTransport transport = this.CurrentTransport;
						if (null != transport)
							failed[Message.ConnectionTypeField] = transport.Name;

						this.Receive(failed);
					}
				}
			}
		}

		#region Methods used for BayeuxClientChannel

		/// <summary>
		/// Generates a new <see cref="IMutableMessage"/> with <see cref="DictionaryMessage"/>.
		/// </summary>
		public virtual IMutableMessage NewMessage()
		{
			return new DictionaryMessage();
		}

		/// <summary>
		/// En-queues or sends a channel message.
		/// </summary>
		public virtual void EnqueueSend(IMutableMessage message)
		{
			if (null == message) return;

			if (this.CanSend)
			{
				bool sent = this.SendMessages(message);
				// DEBUG
				if (logger.IsDebugEnabled)
					logger.DebugFormat("{0} message: {1}", sent ? "Sent" : "Failed", message);
			}
			else
			{
				bool found = false;
				lock (_messagesQueue)
				{
					// Check existence of the message before enqueue
					object field1, field2;
					foreach (IMutableMessage m in _messagesQueue)
					{
						if (String.Compare(m.Channel, message.Channel, StringComparison.OrdinalIgnoreCase) == 0
							&& ((m.TryGetValue(Message.SubscriptionField, out field1)
									&& message.TryGetValue(Message.SubscriptionField, out field2)
									&& field1 != null && field2 != null && field1.Equals(field2))
								|| (m.Data != null && message.Data != null && m.Data.Equals(message.Data)))
							)
						{
							found = true;
							break;
						}
					}

					// Ignores duplicate messages
					if (!found)
						_messagesQueue.Add(message);
				}

				// DEBUG
				if (!found && logger.IsDebugEnabled)
					logger.DebugFormat("Enqueued message {0} (batching: {1})", message, this.IsBatching);
			}
		}

		private bool CanSend
		{
			get
			{
				BayeuxClientState state = _bayeuxClientState;
				return (!this.IsBatching && !state.IsHandshaking);
			}
		}

		#endregion

		#region Message Listeners

		/// <summary>
		/// <p>Callback method invoked when the given messages have hit the network towards the Bayeux server.</p>
		/// <p>The messages may not be modified, and any modification will be useless because the message have
		/// already been sent.</p>
		/// </summary>
		/// <param name="messages">The messages sent.</param>
		public virtual void OnSending(IMessage[] messages)
		{
		}

		/// <summary>
		/// <p>Callback method invoke when the given messages have just arrived from the Bayeux server.</p>
		/// <p>The messages may be modified, but it's suggested to use <see cref="IExtension"/>s instead.</p>
		/// <p>Extensions will be processed after the invocation of this method.</p>
		/// </summary>
		/// <param name="messages">The messages arrived.</param>
		public virtual void OnMessages(IList<IMutableMessage> messages)
		{
		}

		/// <summary>
		/// <p>Callback method invoked when the given messages have failed to be sent.</p>
		/// <p>The default implementation logs the failure at ERROR level.</p>
		/// </summary>
		/// <param name="ex">The exception that caused the failure.</param>
		/// <param name="messages">The messages being sent.</param>
		public virtual void OnFailure(Exception ex, IMessage[] messages)
		{
			// DEBUG
			logger.Error("Messages failed: " + ObjectConverter.Serialize(messages), ex);
		}

		#endregion

		/// <summary>
		/// Updates the state of this Bayeux client session
		/// with the specified <see cref="BayeuxClientState"/> creation callback.
		/// </summary>
		public virtual void UpdateBayeuxClientState(
			Func<BayeuxClientState, BayeuxClientState> create, Action postCreate = null)
		{
			if (null == create)
				throw new ArgumentNullException("create");

			// Increase how many threads are updating the state.
			// This is needed so that in WaitFor() we can check
			// the state being sure that nobody is updating it.
			//lock (_syncRoot)
			//#pragma warning disable 0420
			Interlocked.Increment(ref _stateUpdaters);
			//#pragma warning restore 0420

			// State update is non-blocking
			try
			{
				BayeuxClientState origState, newState = null;
				bool updated = false;

				BayeuxClientState oldState = _bayeuxClientState;
				while (!updated)
				{
					newState = create(oldState);
					if (newState == null)
						throw new InvalidOperationException("Newly created Bayeux client state must be not null.");

					if (oldState != null && !oldState.IsUpdateableTo(newState))
					{
						// DEBUG
						logger.WarnFormat("State not updateable: {0} -> {1}", oldState, newState);
						break;
					}

#pragma warning disable 0420
					origState = Interlocked.CompareExchange<BayeuxClientState>(
						ref _bayeuxClientState, newState, oldState);// CompareAndSet(oldState, newState)
#pragma warning restore 0420

					// TODO: Object.ReferenceEquals()
					updated = ((oldState == null && origState == null)
						|| (oldState != null && origState != null && oldState.Equals(origState)));
					// DEBUG
					if (logger.IsDebugEnabled)
					{
						logger.DebugFormat(CultureInfo.InvariantCulture, "State update: {0} -> {1}{2}",
							oldState, newState, updated ? String.Empty : " failed (concurrent update)");
					}

					if (!updated)
					{
						Thread.Sleep(1);	// TODO: Thread.Sleep(1) ?
						oldState = _bayeuxClientState;
					}
				}

				if (postCreate != null)
					postCreate();

				if (updated)
				{
					if (oldState != null && oldState.Type != newState.Type)
						newState.Enter(oldState.Type);

					newState.Execute();
				}
			}
			finally
			{
				// Notify threads waiting in WaitFor()
				//lock (_syncRoot)
				//{
				//#pragma warning disable 0420
				if (Interlocked.Decrement(ref _stateUpdaters) == 0)
				//#pragma warning restore 0420
				{
					try { _stateUpdated.Set(); }
					catch (ObjectDisposedException) { }
				}
				//}
			}
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(base.ToString());
#if DEBUG
			if (sb[sb.Length - 1] == '}') sb.Length--;
			sb.Append("  TransportRegistry: ");
			lock (_transportRegistry)
			{
				sb.Append(_transportRegistry.ToString().Replace(Environment.NewLine, Environment.NewLine + "  "));
			}

			sb.Append(Environment.NewLine).Append("  Options: ");
			lock (_options)
			{
				sb.Append(ObjectConverter.Serialize(_options).Replace("\r", String.Empty).Replace("\n", Environment.NewLine + "  "));
			}

			sb.Append(Environment.NewLine).Append("  BayeuxClientState: ");
			BayeuxClientState currState = _bayeuxClientState;
			if (currState != null)
			{
				lock (currState)
					sb.Append(currState.ToString().Replace(Environment.NewLine, Environment.NewLine + "  "));
			}

			sb.Append(Environment.NewLine).Append("  MessagesQueue: ");
			lock (_messagesQueue)
			{
				sb.Append(ObjectConverter.Serialize(_messagesQueue).Replace("\r", String.Empty).Replace("\n", Environment.NewLine + "  "));
			}

			sb.Append(Environment.NewLine).Append("  CookiesProvider: ");
			lock (_cookieProvider)
			{
				sb.Append(ObjectConverter.Serialize(_cookieProvider).Replace("\r", String.Empty).Replace("\n", Environment.NewLine + "  "));
			}

			sb.AppendFormat(CultureInfo.InvariantCulture,
				"{0}  BackOffIncrement: {1},{0}  MaxBackOff: {2},{0}  StateUpdaters: {3}{0}}}",
				Environment.NewLine, Interlocked.Read(ref _backOffIncrement), Interlocked.Read(ref _maxBackOff), _stateUpdaters);
#endif

			return sb.ToString();
		}

	}
}
