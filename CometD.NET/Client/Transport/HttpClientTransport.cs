using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;

using CometD.Bayeux;
using CometD.Common;

namespace CometD.Client.Transport
{
	/// <summary>
	/// Represents the base class of the HTTP Client <see cref="ITransport">Transport</see>.
	/// </summary>
	public abstract class HttpClientTransport : ClientTransport
	{
		#region Properties

		private volatile string _url;
		/// <summary>
		/// All HTTP requests are made relative to this base URL.
		/// </summary>
		public virtual string Url
		{
			get { return _url; }
			set { _url = value; }
		}

		private volatile CookieCollection _cookieProvider;
		/// <summary>
		/// HTTP request cookies collection.
		/// </summary>
		public virtual CookieCollection CookieProvider
		{
			get { return _cookieProvider; }
			set { _cookieProvider = value; }
		}

		#endregion

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpClientTransport"/> class.
		/// </summary>
		/// <param name="name">The transport name (required).</param>
		/// <param name="options">The HTTP request (header) options.</param>
		protected HttpClientTransport(string name, IDictionary<string, object> options)
			: base(name, options) { }

		/// <summary>
		/// Returns the <see cref="Cookie"/> with a specific name from this HTTP transport.
		/// </summary>
		public virtual Cookie GetCookie(string name)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			CookieCollection cookies = _cookieProvider;
			return (cookies != null) ? cookies[name] : null;
		}

		/// <summary>
		/// Adds a <see cref="Cookie"/> to this HTTP transport.
		/// </summary>
		public virtual void SetCookie(Cookie cookie)
		{
			if (null == cookie)
				throw new ArgumentNullException("cookie");

			CookieCollection cookies = _cookieProvider;
			if (cookies != null)
			{
				lock (cookies) cookies.Add(cookie);// TODO: SetCookie
			}
		}

		/// <summary>
		/// Setups HTTP request headers.
		/// </summary>
		protected virtual void ApplyRequestHeaders(HttpWebRequest request)
		{
			if (null == request)
				throw new ArgumentNullException("request");

			// Persistent Internet connection option
			string s = this.GetOption(HttpRequestHeader.Connection.ToString(), null);
			if (!String.IsNullOrEmpty(s))
				request.KeepAlive = "Keep-Alive".Equals(s, StringComparison.OrdinalIgnoreCase);

			// Accept HTTP header option
			s = this.GetOption(HttpRequestHeader.Accept.ToString(), null);
			if (!String.IsNullOrEmpty(s)) request.Accept = s;

			// Authorization header option
			s = this.GetOption(HttpRequestHeader.Authorization.ToString(), null);
			if (!String.IsNullOrEmpty(s))
				request.Headers[HttpRequestHeader.Authorization] = s;
		}

		/// <summary>
		/// Setups HTTP request cookies.
		/// </summary>
		protected virtual void ApplyRequestCookies(HttpWebRequest request)
		{
			if (null == request)
				throw new ArgumentNullException("request");

			CookieCollection cookies = _cookieProvider;
			if (null != cookies)
			{
				lock (cookies)
				{
					if (request.CookieContainer == null)
						request.CookieContainer = new CookieContainer();

					// TODO: request.CookieContainer.Add(cookies);
					foreach (Cookie c in cookies)
					{
						if (c != null && (!c.Discard || !c.Expired))
							request.CookieContainer.Add(c);
					}
				}
			}
		}

	}
}
