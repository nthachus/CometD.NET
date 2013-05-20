using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Newtonsoft.Json;

namespace Salesforce.StreamingAPI
{
	[Serializable(), JsonObject()]
	public class OAuthToken
	{
		/// <summary>
		/// Minimum of Salesforce Session timeout is 15 minutes, default is 2 hours.
		/// </summary>
		public const int DefaultSessionTimeout = (2 * 60 * 60 - 120) * 1000;

		/// <summary>
		/// A URL, representing the authenticated user, which can be used to access the Identity Service.
		/// </summary>
		[JsonProperty("id")]
		public virtual string IdentityUrl { get; set; }

		/// <summary>
		/// When the signature was created, represented as the number of seconds since the Unix epoch (00:00:00 UTC on 1 January 1970).
		/// </summary>
		[JsonProperty("issued_at")]
		public virtual long IssuedAt { get; set; }

		/// <summary>
		/// Token that can be used in the future to obtain new access tokens (sessions).
		/// </summary>
		[JsonProperty("refresh_token")]
		public virtual string RefreshToken { get; set; }

		/// <summary>
		/// Identifies the Salesforce instance to which API calls should be sent.
		/// </summary>
		[JsonProperty("instance_url", Required = Required.Always)]
		public virtual string InstanceUrl { get; set; }

		/// <summary>
		/// Base64-encoded HMAC-SHA256 signature signed with the consumer's private key containing the concatenated ID and issued_at.
		/// This can be used to verify the identity URL was not modified since it was sent by the server.
		/// </summary>
		[JsonProperty("signature")]
		public virtual string Signature { get; set; }

		/// <summary>
		/// Salesforce session ID that can be used with the Web services API.
		/// </summary>
		[JsonProperty("access_token", Required = Required.Always)]
		public virtual string AccessToken { get; set; }

		/// <summary>
		/// Gets whether this Token is so old it no longer needs to be stored.
		/// </summary>
		[JsonIgnore()]
		public virtual bool IsExpired
		{
			get
			{
				// Current UNIX timestamp
				long ts = DateTime.UtcNow.Ticks / 10000 - 62135596800000;
				return (ts - DefaultSessionTimeout > this.IssuedAt);
			}
		}

		public override string ToString()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		#region Tokens Management

		private static readonly string tokenFilePath
			= Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "OAuthTokens.txt");

		private static volatile IDictionary<string, OAuthToken> tokensDb;
		private static readonly object syncRoot = new object();

		public static void LoadAll()
		{
			lock (syncRoot)// Synchronized method
			{
				IDictionary<string, OAuthToken> savedTokens = null;
				if (File.Exists(tokenFilePath))
				{
					using (StreamReader sr = new StreamReader(tokenFilePath, Encoding.Default, true))
					{
						string buffer = sr.ReadToEnd();
						if (null != buffer && (buffer = buffer.Trim()).Length > 0)
						{
							savedTokens = JsonConvert.DeserializeObject<IDictionary<string, OAuthToken>>(buffer);
						}
					}
				}

				if (null == savedTokens)
					savedTokens = new Dictionary<string, OAuthToken>(StringComparer.OrdinalIgnoreCase);

				tokensDb = savedTokens;
			}
		}

		public static OAuthToken Load(string userName)
		{
			if (null == userName || (userName = userName.Trim()).Length == 0)
				throw new ArgumentNullException("userName");

			OAuthToken result = null;
			tokensDb.TryGetValue(userName, out result);

			return result;
		}

		public void Save(string userName)
		{
			if (null == userName || (userName = userName.Trim()).Length == 0)
				throw new ArgumentNullException("userName");

			tokensDb[userName] = this;
		}

		public static void SaveAll()
		{
			lock (syncRoot)// Synchronized method
			{
				using (StreamWriter sw = new StreamWriter(tokenFilePath, false, Encoding.Default))
				{
					sw.Write(JsonConvert.SerializeObject(tokensDb, Formatting.Indented));
				}
			}
		}

		#endregion
	}

}
