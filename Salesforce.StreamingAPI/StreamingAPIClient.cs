using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

using Newtonsoft.Json;

using CometD.Bayeux;
using CometD.Bayeux.Client;
using CometD.Client;
using CometD.Client.Transport;

namespace Salesforce.StreamingAPI
{
	public interface IStreamingAPIClient
	{
		string Id { get; }
		OAuthToken OAuthToken { get; }

		/// <summary>
		/// Refresh the authentication header for the next HTTP requests.
		/// </summary>
		void RefreshOAuthHeader();

		/// <summary>
		/// Connects to the Bayeux server.
		/// </summary>
		bool Handshake(int timeout);
		void Handshake();

		void SubscribeTopic(string topicName, IMessageListener listener);
		bool UnsubscribeTopic(string topicName, IMessageListener listener = null);

		bool Disconnect(int timeout);
		void Disconnect();
	}

	/// <summary>
	/// Salesforce Streaming API Client.
	/// </summary>
	public class StreamingAPIClient : IStreamingAPIClient, IDisposable
	{
		/// <summary>Used to debug.</summary>
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(StreamingAPIClient));

		#region Constants

		protected static string ConsumerKey
		{
			get { return ConfigurationManager.AppSettings["Salesforce_Consumer_Key"]; }
		}

		protected static string ConsumerSecret
		{
			get { return ConfigurationManager.AppSettings["Salesforce_Consumer_Secret"]; }
		}

		protected static string OAuthRedirectUrl
		{
			get { return ConfigurationManager.AppSettings["Salesforce_OAuth_RedirectUrl"]; }
		}


		/// <summary>
		/// This URL is used only for logging in.
		/// The LoginResult returns a ServerUrl which is then used for constructing the streaming URL.
		/// The ServerUrl points to the endpoint where your organization is hosted.
		/// </summary>
		protected static string OAuthTokenUrl
		{
			get { return ConfigurationManager.AppSettings["Salesforce_OAuth_TokenUrl"]; }
		}

		/// <summary>
		/// Streaming endpoint URI.
		/// </summary>
		protected static string StreamingAPIEndpoint
		{
			get { return ConfigurationManager.AppSettings["Salesforce_StreamingAPI_Endpoint"].Trim('/', '\\'); }
		}

		#endregion

		private readonly string _userName;
		private readonly string _password;

		private readonly BayeuxClient _bayeuxClient;
		private readonly HttpClientTransport _clientTransport;

		public StreamingAPIClient(string userName, string password)
		{
			if (null == userName || (userName = userName.Trim()).Length == 0)
				throw new ArgumentNullException("userName");
			if (String.IsNullOrEmpty(password))
				throw new ArgumentNullException("password");

			_userName = userName;
			_password = password;

			// Initializes a new Bayeux client
			IDictionary<string, object> options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
			{
				// TODO: Salesforce socket timeout during connection (CometD session) = 110 seconds
				{ ClientTransport.MaxNetworkDelayOption, 120000 }
			};

			_clientTransport = new LongPollingTransport(options);
			// TODO: DefaultInstanceUrl "https://na14.salesforce.com/" + StreamingAPIEndpoint
			_bayeuxClient = new BayeuxClient(null, _clientTransport);
		}

		public virtual string Id
		{
			get { return _userName; }
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(_userName);
			sb.Append(Environment.NewLine).Append(_bayeuxClient);

			return sb.ToString();
		}

		#region IDisposable Members

		private volatile bool _disposed = false;

		/// <summary>
		/// Releases the unmanaged resources and disposes of the managed resources used by the <see cref="StreamingAPIClient" />.
		/// </summary>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="StreamingAPIClient" /> and optionally disposes of the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources; false to releases only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing && !_disposed)
			{
				_disposed = true;

				_bayeuxClient.Dispose();
			}
		}

		#endregion

		#region HTTP Requests Processing

		private static void InitHttpRequest(HttpWebRequest req, string accessToken)
		{
			req.Accept = "application/json";
			req.KeepAlive = true;
			req.AllowWriteStreamBuffering = true;	// Is needed for KeepAlive
			req.AllowAutoRedirect = true;
			req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

			if (null != accessToken && (accessToken = accessToken.Trim()).Length > 0)
				req.Headers[HttpRequestHeader.Authorization] = "OAuth " + accessToken;
		}

		public static string HttpPost<T>(string url, T postData, string accessToken = null)
		{
			if (null == url || (url = url.Trim()).Length == 0)
				throw new ArgumentNullException("url");
			// DEBUG
			logger.InfoFormat("Try to post data to URL: {0}", url);

			bool isObject = (typeof(T) != typeof(string));

			HttpWebRequest req = WebRequest.Create(new Uri(url, UriKind.RelativeOrAbsolute)) as HttpWebRequest;
			req.Method = WebRequestMethods.Http.Post;

			InitHttpRequest(req, accessToken);
			req.ContentType = (!isObject ? "application/x-www-form-urlencoded" : req.Accept)
				+ ";charset=" + Encoding.UTF8.WebName;

			// Add parameters to post
			if (null != postData)
			{
				string payLoad = (!isObject) ? postData as string : JsonConvert.SerializeObject(postData);
				if (!String.IsNullOrEmpty(payLoad))
				{
					// DEBUG
					if (logger.IsDebugEnabled)
						logger.DebugFormat("Posting data: {0}", payLoad);

					byte[] data = Encoding.UTF8.GetBytes(payLoad);
					req.ContentLength = data.Length;

					using (Stream os = req.GetRequestStream())
					{
						os.Write(data, 0, data.Length);
					}
				}
			}

			// Do the post and get the response
			WebResponse resp = null;
			Exception error = null;
			try
			{
				resp = req.GetResponse();
			}
			catch (WebException wex)
			{
				error = wex;
				resp = wex.Response;
			}

			string result = null;
			if (resp != null)
			{
				using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8, true))
				{
					result = sr.ReadToEnd();
					if (null != result) result = result.Trim();
				}
			}

			if (null != error)
				throw new InvalidOperationException(String.IsNullOrEmpty(result) ? error.GetBaseException().Message : result, error);

			return result;
		}

		public static string HttpGet(string url, string accessToken = null)
		{
			if (null == url || (url = url.Trim()).Length == 0)
				throw new ArgumentNullException("url");
			// DEBUG
			logger.InfoFormat("Try to get data from URL: {0}", url);

			HttpWebRequest req = WebRequest.Create(new Uri(url, UriKind.RelativeOrAbsolute)) as HttpWebRequest;
			req.Method = WebRequestMethods.Http.Get;

			InitHttpRequest(req, accessToken);
			req.ContentType = req.Accept + ";charset=" + Encoding.UTF8.WebName;
			req.Headers["X-PrettyPrint"] = "1";

			WebResponse resp = null;
			Exception error = null;
			try
			{
				resp = req.GetResponse();
			}
			catch (WebException wex)
			{
				error = wex;
				resp = wex.Response;
			}

			string result = null;
			if (resp != null)
			{
				using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8, true))
				{
					result = sr.ReadToEnd();
					if (null != result) result = result.Trim();
				}
			}

			if (null != error)
				throw new InvalidOperationException(String.IsNullOrEmpty(result) ? error.GetBaseException().Message : result, error);

			return result;
		}

		#endregion

		#region OAuth 2.0 Authentication

		private volatile OAuthToken _token = null;
		public virtual OAuthToken OAuthToken
		{
			get { return _token; }
		}

		private static readonly object syncRoot = new object();
		/// <summary>
		/// Refresh the authentication header for the next HTTP requests.
		/// </summary>
		public virtual void RefreshOAuthHeader()
		{
			bool updated = false;
			OAuthToken lastToken = null;

			lock (syncRoot)// Threads-safe
			{
				lastToken = _token;
				if (null == lastToken)
				{
					lastToken = OAuthToken.Load(_userName);
					// DEBUG
					if (logger.IsDebugEnabled)
						logger.DebugFormat("Retrieved last OAuth token: {0}", lastToken);
				}

				if (null == lastToken || lastToken.IsExpired)
				{
					// Refresh the token
					lastToken = RefreshAccessToken(_userName, _password);
					updated = true;
				}

				_token = lastToken;
			}

			if (null != lastToken)
			{
				if (updated)
					lastToken.Save(_userName);

				// Updates the Salesforce RestAPI base URL
				lock (_clientTransport)
				{
					if (updated || String.IsNullOrEmpty(_clientTransport.Url))
					{
						_clientTransport.Url = lastToken.InstanceUrl.TrimEnd('/', '\\') + "/" + StreamingAPIEndpoint;
						_clientTransport.SetOption(HttpRequestHeader.Authorization.ToString(), "OAuth " + lastToken.AccessToken);
					}
				}
			}
		}

		/// <summary>
		/// Authenticates to Salesforce to obtain a new Salesforce access token.
		/// </summary>
		protected static OAuthToken RefreshAccessToken(string userName, string password)
		{
			StringBuilder body = new StringBuilder();
			body.Append("grant_type=password");
			body.Append("&username=" + Uri.EscapeDataString(userName));
			body.Append("&password=" + Uri.EscapeDataString(password));
			body.Append("&client_id=" + Uri.EscapeDataString(ConsumerKey));
			body.Append("&client_secret=" + Uri.EscapeDataString(ConsumerSecret));
			body.Append("&redirect_uri=" + Uri.EscapeDataString(OAuthRedirectUrl));

			string result = HttpPost<string>(OAuthTokenUrl, body.ToString());

			// Convert the JSON response into a token object
			OAuthToken token = JsonConvert.DeserializeObject<OAuthToken>(result);
			// DEBUG
			if (logger.IsDebugEnabled)
				logger.DebugFormat("Obtained OAuth (v2.0) Token: {0}", token);

			return token;
		}

		#endregion

		/// <summary>
		/// Handles all Bayeux client errors.
		/// </summary>
		protected virtual void HandleBayeuxErrors()
		{
			IClientSessionChannel channel = _bayeuxClient.GetChannel("/**");
			if (null != channel)
				channel.AddListener(new CallbackMessageListener<IStreamingAPIClient>(OnBayeuxClientFailure, this));
		}

		protected virtual void RemoveBayeuxErrorHandlers()
		{
			IClientSessionChannel channel = _bayeuxClient.GetChannel("/**", false);
			if (null != channel)
				channel.RemoveListener(null);
		}

		/// <summary>
		/// Used to handle all Bayeux client errors.
		/// </summary>
		private static void OnBayeuxClientFailure(
			IClientSessionChannel channel, IMessage message, IStreamingAPIClient that)
		{
			if (null != message && !message.IsSuccessful && null == message.Data)
			{
				// DEBUG
				logger.InfoFormat(CultureInfo.InvariantCulture,
					"Failed message for client '{1}', channel: {2}{0}{3}",
					Environment.NewLine, that.Id, channel.Id, JsonConvert.SerializeObject(message, Formatting.Indented));

				object val;
				/*string error;
				if (message.TryGetValue(Message.ErrorField, out val)
					&& null != val
					&& (error = val.ToString().Trim()).Length > 0)
				{
					// DEBUG
					logger.ErrorFormat("Error during {0}: {1}", message.Channel, error);
				}*/

				Exception exception;
				if (message.TryGetValue(Message.ExceptionField, out val)
					&& null != val
					&& (exception = val as Exception) != null)
				{
					// DEBUG
					logger.Error("Exception during: " + message.Channel, exception);

					try { that.RefreshOAuthHeader(); }
					catch (Exception) { }
				}

				/*string failure = null;
				if (message.TryGetValue(Message.MessageField, out val)
					&& null != val
					&& (failure = val.ToString().Trim()).Length > 0)
				{
					// DEBUG
					logger.Error("Failed sending message: " + failure);
				}*/
			}
		}

		#region IStreamingAPIClient Members

		/// <summary>
		/// Connects to the Bayeux server.
		/// </summary>
		public virtual bool Handshake(int timeout)
		{
			this.RefreshOAuthHeader();
			this.HandleBayeuxErrors();

			try
			{
				return _bayeuxClient.Handshake(null, timeout);
			}
			catch (Exception)
			{
				this.RemoveBayeuxErrorHandlers();
				throw;
			}
		}

		public virtual void Handshake()
		{
			this.RefreshOAuthHeader();
			this.HandleBayeuxErrors();

			try
			{
				_bayeuxClient.Handshake(null);
			}
			catch (Exception)
			{
				this.RemoveBayeuxErrorHandlers();
				throw;
			}
		}

		public virtual void SubscribeTopic(string topicName, IMessageListener listener)
		{
			if (null == topicName || (topicName = topicName.Trim()).Length == 0)
				throw new ArgumentNullException("topicName");
			if (null == listener)
				throw new ArgumentNullException("listener");

			//this.RefreshOAuthHeader();

			IClientSessionChannel channel = _bayeuxClient.GetChannel(Channel.Topic + "/" + topicName);
			if (null != channel)
				channel.Subscribe(listener);
		}

		public virtual bool UnsubscribeTopic(string topicName, IMessageListener listener = null)
		{
			if (null == topicName || (topicName = topicName.Trim()).Length == 0)
				throw new ArgumentNullException("topicName");

			//this.RefreshOAuthHeader();

			IClientSessionChannel channel = _bayeuxClient.GetChannel(Channel.Topic + "/" + topicName, false);
			if (null != channel)
			{
				if (null != listener) channel.Unsubscribe(listener);
				else channel.Unsubscribe();

				return true;
			}

			return false;
		}

		public virtual bool Disconnect(int timeout)
		{
			//this.RefreshOAuthHeader();
			this.RemoveBayeuxErrorHandlers();

			return _bayeuxClient.Disconnect(timeout);
		}

		public virtual void Disconnect()
		{
			//this.RefreshOAuthHeader();
			this.RemoveBayeuxErrorHandlers();

			_bayeuxClient.Disconnect();
		}

		#endregion

	}
}
