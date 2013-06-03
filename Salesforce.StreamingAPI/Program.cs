using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

using Newtonsoft.Json;

using CometD.Bayeux.Client;
using CometD.Bayeux;

namespace Salesforce.StreamingAPI
{
	/// <summary>
	/// This example demonstrates how a streaming client works against the Salesforce Streaming API.
	/// </summary>
	public class Program
	{
		static void Main(/*string[] args*/)
		{
			AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
			// Set server certificate validation callback
			ServicePointManager.ServerCertificateValidationCallback = ValidateRemoteServerCertificate;
			ServicePointManager.Expect100Continue = false;	// TODO: Turning HttpWebRequest performance

			IList<TestAccount> accounts = TestAccounts;
			if (null != accounts && accounts.Count > 0)
			{
				// Load Tokens DB
				OAuthToken.LoadAll();
				AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainProcessExit;

				IDictionary<TestAccount, IStreamingAPIClient> streamingClients = new Dictionary<TestAccount, IStreamingAPIClient>();
				foreach (TestAccount account in accounts)
				{
					IStreamingAPIClient streamingClient = new StreamingAPIClient(account.UserName, account.Password);

					Console.WriteLine("Handshake with client: {0}", account.UserName);
					/*if (*/
					streamingClient.Handshake(60000);// 60 seconds timeout
					//{
					foreach (string topicName in account.PushTopics)
					{
						Console.WriteLine("Subscribes push-topic: {0} with listener2", topicName);
						streamingClient.SubscribeTopic(topicName,
							new CallbackMessageListener<IStreamingAPIClient>(OnMessageReceived2, streamingClient));

						if (account.PushTopics.Count < 2)
						{
							Console.WriteLine("Subscribes push-topic: {0} with listener1", topicName);
							streamingClient.SubscribeTopic(topicName,
								new CallbackMessageListener<IStreamingAPIClient>(OnMessageReceived, streamingClient));
						}
					}

					streamingClients[account] = streamingClient;
					//}
				}

				Console.WriteLine("Press Enter to exit...");
				Console.ReadLine();

				if (streamingClients.Count > 0)
				{
					foreach (KeyValuePair<TestAccount, IStreamingAPIClient> item in streamingClients)
					{
						try
						{
							foreach (string topicName in item.Key.PushTopics)
							{
								Console.WriteLine("Un-subscribes push-topic: {0}", topicName);
								item.Value.UnsubscribeTopic(topicName);
							}

							Console.WriteLine("Disconnects the Bayeux server for client: {0}", item.Key.UserName);
							item.Value.Disconnect(30000);// 1 minutes timeout
						}
						finally
						{
							((IDisposable)item.Value).Dispose();
						}
					}
				}
			}
		}

		private static readonly log4net.ILog _topicLogger = log4net.LogManager.GetLogger("PushTopicMessages");

		/// <summary>
		/// Processes the PushTopic message have just arrived.
		/// </summary>
		private static void OnMessageReceived(
			IClientSessionChannel channel, IMessage message, IStreamingAPIClient streamingClient)
		{
			// DEBUG
			_topicLogger.DebugFormat(CultureInfo.InvariantCulture,
				"Listener1 - Received PushTopic message for client '{1}', channel: {2}{0}{3}",
				Environment.NewLine, streamingClient.Id, channel.Id, JsonConvert.SerializeObject(message, Formatting.Indented));

			Thread.Sleep(3 * 60 * 1000);// Emulates long-time processing method

			// DEBUG
			_topicLogger.DebugFormat(CultureInfo.InvariantCulture,
				"Listener1 - (After 3 minutes) Processed PushTopic message for client '{1}', channel: {2}{0}{3}",
				Environment.NewLine, streamingClient.Id, channel.Id, JsonConvert.SerializeObject(message, Formatting.Indented));
		}

		private static void OnMessageReceived2(
			IClientSessionChannel channel, IMessage message, IStreamingAPIClient streamingClient)
		{
			// DEBUG
			_topicLogger.DebugFormat(CultureInfo.InvariantCulture,
				"Listener2 - Received PushTopic message for client '{1}', channel: {2}{0}{3}",
				Environment.NewLine, streamingClient.Id, channel.Id, JsonConvert.SerializeObject(message, Formatting.Indented));

			Thread.Sleep(10 * 1000);

			// DEBUG
			_topicLogger.DebugFormat(CultureInfo.InvariantCulture,
				"Listener2 - (After 10 seconds) Processed PushTopic message for client '{1}', channel: {2}{0}{3}",
				Environment.NewLine, streamingClient.Id, channel.Id, JsonConvert.SerializeObject(message, Formatting.Indented));
		}

		/// <summary>Used to debug.</summary>
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Program));

		private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = e.ExceptionObject as Exception;
			if (null != ex)
				logger.Error("Unhandled exception has occurred:", ex);
		}

		private static void OnCurrentDomainProcessExit(object sender, EventArgs e)
		{
			OAuthToken.SaveAll();

			// This would be automatic, but in partial trust scenarios it is not
			log4net.LogManager.Shutdown();
		}

		/// <summary>
		/// Verifies the remote Secure Sockets Layer (SSL) certificate used for authentication.
		/// </summary>
		/// <param name="sender">An object that contains state information for this validation.</param>
		/// <param name="certificate">The certificate used to authenticate the remote party.</param>
		/// <param name="chain">The chain of certificate authorities associated with the remote certificate.</param>
		/// <param name="sslPolicyErrors">One or more errors associated with the remote certificate.</param>
		/// <returns>A System.Boolean value that determines whether the specified certificate is accepted for authentication.</returns>
		private static bool ValidateRemoteServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			// This is a Salesforce RestAPI Client, so all certificates are trusted
			return true;
		}

		public static IList<TestAccount> TestAccounts
		{
			get
			{
				string jsonContent = ConfigurationManager.AppSettings["Salesforce_Test_Accounts"];
				return String.IsNullOrEmpty(jsonContent) ? null : JsonConvert.DeserializeObject<IList<TestAccount>>(jsonContent);
			}
		}

	}

	[Serializable(), JsonObject()]
	public sealed class TestAccount : IEquatable<TestAccount>
	{
		public string UserName { get; set; }
		public string Password { get; set; }
		public IList<string> PushTopics { get; set; }

		#region IEquatable<TestAccount> Members

		public override string ToString()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		public override int GetHashCode()
		{
			return this.UserName.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			TestAccount other = obj as TestAccount;
			if (null != other)
				return this.Equals(other);

			return base.Equals(obj);
		}

		public bool Equals(TestAccount other)
		{
			return (null != other
				&& String.Compare(this.UserName, other.UserName, StringComparison.OrdinalIgnoreCase) == 0);
		}

		#endregion
	}

}
