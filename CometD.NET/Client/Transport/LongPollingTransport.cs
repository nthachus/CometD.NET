using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using CometD.Bayeux;
using CometD.Common;

namespace CometD.Client.Transport
{
	/// <summary>
	/// Represents the long-polling HTTP <see cref="ITransport">Transport</see> of a Bayeux client session.
	/// </summary>
	public class LongPollingTransport : HttpClientTransport
	{
		private readonly IList<LongPollingRequest> _exchanges = new List<LongPollingRequest>();

		private volatile bool _aborted;
		private volatile IDictionary<string, object> _advice;

		/// <summary>
		/// Stores the last transport Timeout value (in milliseconds)
		/// that was contained in the <see cref="IMessage.Advice"/> field
		/// of the last received connecting message, to use for next requests.
		/// </summary>
		public virtual IDictionary<string, object> Advice
		{
			get { return _advice; }
			set { _advice = value; }
		}

		/// <summary>
		/// Removes a specified HTTP request from the requests queue (when it is completed).
		/// </summary>
		public virtual bool RemoveRequest(LongPollingRequest exchange)
		{
			if (null != exchange)
			{
				lock (_exchanges)
				{
					if (!_aborted)
						return _exchanges.Remove(exchange);
					// DEBUG
					//Debug.Print("The HTTP request was removed; Exchanges count: {0}", _exchanges.Count);
				}
			}

			return false;
		}

		// Used to customize the HTTP request before it is sent.
		//private readonly Action<HttpWebRequest> _customize;

		/// <summary>
		/// Initializes a new instance of the <see cref="LongPollingTransport"/> class.
		/// </summary>
		/// <param name="options">The HTTP request (header) options.</param>
		public LongPollingTransport(IDictionary<string, object> options)
			: base("long-polling", options)
		{
			this.OptionPrefix = "long-polling.json";
		}

		/// <summary>
		/// Specifies an option prefix made of string segments separated by the "." character,
		/// used to override more generic configuration entries.
		/// </summary>
		public override sealed string OptionPrefix
		{
			get { return base.OptionPrefix; }
			set { base.OptionPrefix = value; }
		}

		/// <summary>
		/// Accepts all Bayeux versions below 2.3.1.
		/// </summary>
		public override bool Accept(string bayeuxVersion)
		{
			return true;
		}

		/// <summary>
		/// Initializes this client transport.
		/// </summary>
		public override void Init()
		{
			base.Init();

			_aborted = false;
		}

		/// <summary>
		/// Cancels all available HTTP requests in the client transport.
		/// </summary>
		public override void Abort()
		{
			IList<LongPollingRequest> exchanges = null;
			lock (_exchanges)
			{
				if (!_aborted)
				{
					_aborted = true;

					exchanges = new List<LongPollingRequest>(_exchanges);
					_exchanges.Clear();
				}
			}

			if (null != exchanges && exchanges.Count > 0)
			{
				foreach (LongPollingRequest exchange in exchanges)
					exchange.Abort();
			}
		}

		/// <summary>
		/// Resets this client transport.
		/// </summary>
		public override void Reset()
		{
			// TODO: lock (_exchanges) _exchanges.Clear();
		}

		private static readonly Regex uriRegex = new Regex(
			@"^(https?://(([^:/\?#]+)(:(\d+))?))?([^\?#]*)(.*)?$",
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// Sends the specified messages to a Bayeux server asynchronously.
		/// </summary>
		/// <param name="listener">The listener used to process the request response.</param>
		/// <param name="messages">The list of messages will be sent in one HTTP request.</param>
		public override void Send(ITransportListener listener, params IMutableMessage[] messages)
		{
			if (messages == null || messages.Length == 0 || messages[0] == null)
				throw new ArgumentNullException("messages");

			string url = this.Url;

			if (null == url) url = String.Empty;
			else url = url.Trim();

			// Builds the request URL based on the message channel name
			Match uriMatch = uriRegex.Match(url);
			if (uriMatch.Success)
			{
				string afterPath = (uriMatch.Groups.Count > 7) ? uriMatch.Groups[7].Value : null;
				// Append message type into the URL ?
				if ((afterPath == null || afterPath.Trim().Length == 0)
					&& messages.Length == 1 && messages[0].IsMeta)
				{
					string type = messages[0].Channel.Substring(Channel.Meta.Length);
					url = url.TrimEnd('\\', '/') + "/" + type.Trim('\\', '/');
				}
			}

			try
			{
				// Creates a new HttpWebRequest object
				HttpWebRequest request = WebRequest.Create(new Uri(url, UriKind.RelativeOrAbsolute)) as HttpWebRequest;
				request.Method = WebRequestMethods.Http.Post;
				request.Accept = "application/json";
				request.ContentType = request.Accept + ";charset=" + Encoding.UTF8.WebName;
				request.KeepAlive = true;
				request.AllowWriteStreamBuffering = true;	// Is needed for KeepAlive
				request.AllowAutoRedirect = true;
				request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

				// Setups HTTP request headers
				this.ApplyRequestHeaders(request);

				// Setups HTTP request cookies
				this.ApplyRequestCookies(request);

				// Calculates the HTTP request timeout (in milliseconds)
				int maxNetworkDelay = this.GetOption<int>(MaxNetworkDelayOption, request.Timeout);
				if (messages.Length == 1
					&& Channel.MetaConnect.Equals(messages[0].Channel, StringComparison.OrdinalIgnoreCase))
				{
					IDictionary<string, object> advice = messages[0].Advice;
					if (advice == null)
						advice = _advice;

					object val;
					if (advice != null && advice.TryGetValue(Message.TimeoutField, out val))
					{
						long timeout = ObjectConverter.ToPrimitive<long>(val, 0);
						if (timeout != 0)
							maxNetworkDelay += unchecked((int)timeout);
					}
				}
				request.Timeout = maxNetworkDelay;

				//if (null != _customize) _customize(request);

				// Creates a new HTTP Transport Exchange
				LongPollingRequest httpExchange;
				lock (_exchanges)
				{
					if (_aborted)
						throw new InvalidOperationException("The client transport has been aborted.");

					httpExchange = new LongPollingRequest(this, request, listener, messages);
					_exchanges.Add(httpExchange);
				}

				// Processes the HTTP request
				httpExchange.Send();
			}
			catch (Exception ex)
			{
				if (listener != null)
					listener.OnException(ex, messages);
				else
				{
					// DEBUG
					Trace.TraceError("Failed to send messages:{0}{1}{0}--- via transport: {2}{0}{3}",
						Environment.NewLine, ObjectConverter.Serialize(messages), this.ToString(), ex.ToString());
				}
			}
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
#if DEBUG
			StringBuilder sb = new StringBuilder(base.ToString());
			lock (_exchanges)
			{
				foreach (LongPollingRequest exchange in _exchanges)
				{
					sb.Append(Environment.NewLine).Append("    +- ");
					sb.Append(exchange);
				}
			}

			return sb.ToString();
#else
			return base.ToString();
#endif
		}

	}
}
