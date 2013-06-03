using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using CometD.Bayeux;
using CometD.Common;

namespace CometD.Client.Transport
{
	/// <summary>
	/// The wrapper class of <see cref="HttpWebRequest"/>, used to make a HTTP request to a URI.
	/// </summary>
	public /*sealed*/ class LongPollingRequest
	{
		[NonSerialized]
		private readonly LongPollingTransport _transport;
		private readonly HttpWebRequest _request;
		private readonly ITransportListener _listener;
		private readonly IMessage[] _messages;

		/// <summary>
		/// Initializes a new instance of the <see cref="LongPollingRequest"/> class.
		/// </summary>
		public LongPollingRequest(
			LongPollingTransport transport,
			HttpWebRequest request,
			ITransportListener listener,
			params IMessage[] messages)
		{
			if (null == transport)
				throw new ArgumentNullException("transport");
			if (null == request)
				throw new ArgumentNullException("request");
			if (messages == null || messages.Length == 0 || messages[0] == null)
				throw new ArgumentNullException("messages");

			_transport = transport;
			_request = request;
			_listener = listener;
			_messages = messages;
		}

		private volatile bool _isSending = false;
		private volatile bool _aborted = false;

		/// <summary>
		/// Cancels this HTTP Web request.
		/// </summary>
		public virtual void Abort()
		{
			if (_isSending && !_aborted)
			{
				_aborted = true;

				_request.Abort();
				if (null != _listener)
					_listener.OnException(new OperationCanceledException("The HTTP request has been aborted."), _messages);
			}
		}

		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(LongPollingRequest));

		/// <summary>
		/// Begins this HTTP Web request.
		/// </summary>
		public virtual void Send()
		{
			try
			{
				// Start the asynchronous operation
				_request.BeginGetRequestStream(new AsyncCallback(GetRequestStreamCallback), this);

				_isSending = true;
			}
			catch (Exception ex)
			{
				if (_listener != null)
					_listener.OnConnectException(ex, _messages);
				else
					logger.Error(ex);// DEBUG

				_transport.RemoveRequest(this);
			}
		}

		// From http://msdn.microsoft.com/en-us/library/system.net.httpwebrequest.begingetrequeststream.aspx
		private static void GetRequestStreamCallback(IAsyncResult asyncResult)
		{
			LongPollingRequest exchange = asyncResult.AsyncState as LongPollingRequest;
			try
			{
				string content = ObjectConverter.Serialize(exchange._messages);
				// DEBUG
				//if (logger.IsDebugEnabled)
				//	logger.DebugFormat(CultureInfo.InvariantCulture, "Sending message(s): {0}", content);

				// Convert the string into a byte array
				byte[] buffer = Encoding.UTF8.GetBytes(content);

				// End the operation
				using (Stream postStream = exchange._request.EndGetRequestStream(asyncResult))
				{
					// Write to the request stream
					postStream.Write(buffer, 0, buffer.Length);
				}

				// On request committed
				if (null != exchange._listener)
					exchange._listener.OnSending(exchange._messages);

				int exchangeTimeout = exchange._request.Timeout;
				// TODO: Restore request timeout (default: 100 seconds)
				//exchange._request.Timeout = exchange._transport.GetOption<int>(ClientTransport.MaxNetworkDelayOption, 100000);
				// DEBUG
				if (logger.IsDebugEnabled)
				{
					logger.DebugFormat(CultureInfo.InvariantCulture,
						"Begin get response from URL: '{0}'  with exchange timeout: {1}",
						exchange._request.RequestUri, exchangeTimeout);
				}

				// Start the asynchronous operation to get the response
				IAsyncResult result = exchange._request.BeginGetResponse(new AsyncCallback(GetResponseCallback), exchange);

				// This line implements the timeout, if there is a timeout, the callback fires and the request becomes aborted
				ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle,
					new WaitOrTimerCallback(TimeoutCallback), exchange, exchangeTimeout, true);
			}
			catch (Exception ex)
			{
				exchange._isSending = false;

				// TODO: OnConnectException
				if (null != exchange._listener && !exchange._aborted)
					exchange._listener.OnException(ex, exchange._messages);
				else
					logger.Error(ex);// DEBUG

				exchange._transport.RemoveRequest(exchange);
			}
		}

		private static void GetResponseCallback(IAsyncResult asyncResult)
		{
			LongPollingRequest exchange = asyncResult.AsyncState as LongPollingRequest;
			try
			{
				string responseString;
				HttpStatusCode responseStatus;

				// End the operation
				HttpWebResponse response;
				Exception error = null;
				try
				{
					response = exchange._request.EndGetResponse(asyncResult) as HttpWebResponse;
				}
				catch (WebException wex)
				{
					if (wex.Status == WebExceptionStatus.RequestCanceled)
						throw;

					response = wex.Response as HttpWebResponse;
					if (null == response) throw;

					error = wex;
				}

				using (response)
				{
					responseStatus = response.StatusCode;

					using (StreamReader streamRead = new StreamReader(response.GetResponseStream(), Encoding.UTF8, true))
					{
						responseString = streamRead.ReadToEnd();
						// DEBUG
						if (logger.IsDebugEnabled)
							logger.DebugFormat(CultureInfo.InvariantCulture, "Received message(s): {0}", responseString);
					}

					// Stores the transport Cookies to use for next requests
					if (response.Cookies != null && response.Cookies.Count > 0)
					{
						foreach (Cookie cookie in response.Cookies)
						{
							if (null != cookie && (!cookie.Discard || !cookie.Expired))
								exchange._transport.SetCookie(cookie);
						}
					}
				}

				if (responseStatus != HttpStatusCode.OK)// TODO: error != null
				{
					if (null != exchange._listener)
					{
						exchange._listener.OnProtocolError(String.Format(CultureInfo.InvariantCulture,
							"Unexpected response {0}: {1}", (int)responseStatus, responseString), error, exchange._messages);
					}
					else
					{
						// DEBUG
						logger.Error(String.Format(CultureInfo.InvariantCulture,
							"Unexpected response {0}: {1}", (int)responseStatus, responseString), error);
					}
				}
				else if (responseString != null && (responseString = responseString.Trim()).Length > 0)
				{
					// TODO: responseString.Replace('"', '\'')
					IList<IMutableMessage> messages = DictionaryMessage.ParseMessages(responseString);

					// Backups the transport Timeout value (in milliseconds) to use for next requests
					foreach (IMutableMessage message in messages)
					{
						if (message != null
							&& message.IsSuccessful
							&& Channel.MetaConnect.Equals(message.Channel, StringComparison.OrdinalIgnoreCase))
						{
							IDictionary<string, object> advice = message.Advice;
							object timeout;
							if (advice != null
								&& advice.TryGetValue(Message.TimeoutField, out timeout)
								&& timeout != null && timeout.ToString().Trim().Length > 0)
							{
								exchange._transport.Advice = advice;
							}
						}
					}

					if (null != exchange._listener)
					{
						// Fixes the received messages before processing
						string requestChannel = null;
						string requestUrl, baseUrl;
						foreach (IMutableMessage msg in messages)
						{
							if (String.IsNullOrEmpty(msg.Channel))
							{
								if (null == requestChannel)
								{
									requestUrl = exchange._request.RequestUri.ToString();// Absolute request URI

									baseUrl = exchange._transport.Url;
									baseUrl = (null == baseUrl) ? String.Empty : baseUrl.Trim();

									if (requestUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
									{
										requestChannel = requestUrl.Substring(baseUrl.Length).Trim('\\', '/');
										requestChannel = Channel.Meta.TrimEnd('\\', '/') + "/" + requestChannel;
									}
									else
										requestChannel = String.Empty;
								}

								if (!String.IsNullOrEmpty(requestChannel))
									msg.Channel = requestChannel;
							}
						}

						exchange._listener.OnMessages(messages);
					}
				}
				else
				{
					if (null != exchange._listener)
					{
						exchange._listener.OnProtocolError(String.Format(CultureInfo.InvariantCulture,
							"Empty response (204) from URL: {0}", exchange._request.RequestUri), null, exchange._messages);
					}
					else
					{
						// DEBUG
						logger.ErrorFormat(CultureInfo.InvariantCulture, "Empty response (204) from URL: {0}", exchange._request.RequestUri);
					}
				}
			}
			catch (Exception ex)
			{
				if (null != exchange._listener && !exchange._aborted)
					exchange._listener.OnException(ex, exchange._messages);
				else
					logger.Error(ex);// DEBUG
			}
			finally
			{
				exchange._isSending = false;
				exchange._transport.RemoveRequest(exchange);
			}
		}

		/// <summary>
		/// Abort the request if the timer fires.
		/// </summary>
		private static void TimeoutCallback(object state, bool timedOut)
		{
			if (timedOut)
			{
				// DEBUG
				logger.Warn("HTTP Web request Timeout detected!");

				LongPollingRequest exchange = state as LongPollingRequest;
				if (exchange != null)
				{
					exchange.Abort();

					if (null != exchange._listener)
						exchange._listener.OnExpire(exchange._messages);
				}
			}
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
			return _request.RequestUri.ToString();
		}

	}
}
