using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace CometD.Client.Transport
{
	/// <summary>
	/// Represents a registry of <see cref="ClientTransport"/>s.
	/// </summary>
	public class TransportRegistry
	{
		private readonly IDictionary<string, ClientTransport> _transports
			= new Dictionary<string, ClientTransport>(StringComparer.OrdinalIgnoreCase);
		private readonly ICollection<string> _allowedTransports
			= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Adds a new <see cref="ClientTransport"/> into this registry.
		/// </summary>
		public virtual void Add(ClientTransport transport)
		{
			if (transport != null)
			{
				string transportName = transport.Name;
				lock (_transports)
					_transports[transportName] = transport;

				lock (_allowedTransports)
				{
					if (!_allowedTransports.Contains(transportName))
						_allowedTransports.Add(transportName);
				}
			}
		}

		/// <summary>
		/// Returns unmodifiable collection of known transports.
		/// </summary>
		public virtual ICollection<string> KnownTransports
		{
			get { return _transports.Keys; }
		}

		/// <summary>
		/// Returns unmodifiable list of allowed transports.
		/// </summary>
		public virtual ICollection<string> AllowedTransports
		{
			get { return _allowedTransports; }
		}

		/// <summary>
		/// Returns a list of requested transports that exists in this registry.
		/// </summary>
		public virtual IList<ClientTransport> Negotiate(
			IEnumerable<string> requestedTransports, string bayeuxVersion)
		{
			IList<ClientTransport> list = new List<ClientTransport>();

			if (null != requestedTransports)
			{
				foreach (string transportName in requestedTransports)
				{
					if (!String.IsNullOrEmpty(transportName)
						&& _allowedTransports.Contains(transportName))
					{
						ClientTransport transport = this.GetTransport(transportName);
						if (transport != null && transport.Accept(bayeuxVersion))
						{
							list.Add(transport);
						}
					}
				}
			}

			return list;
		}

		/// <summary>
		/// Returns an existing <see cref="ClientTransport"/> in this registry.
		/// </summary>
		/// <param name="transport">The transport name.</param>
		/// <returns>Return null if the <paramref name="transport"/> does not exist.</returns>
		public virtual ClientTransport GetTransport(string transport)
		{
			if (String.IsNullOrEmpty(transport))
				throw new ArgumentNullException("transport");

			ClientTransport val;
			return _transports.TryGetValue(transport, out val) ? val : null;
		}

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();

			sb.AppendFormat(CultureInfo.InvariantCulture, "{{{0}  AllowedTransports: [", Environment.NewLine);
			lock (_allowedTransports)
			{
				string[] allowedTransports = new string[_allowedTransports.Count];
				_allowedTransports.CopyTo(allowedTransports, 0);

				sb.AppendFormat(CultureInfo.InvariantCulture, "'{0}'", String.Join("', '", allowedTransports));
			}

			sb.AppendFormat(CultureInfo.InvariantCulture, "]{0}  KnownTransports: [", Environment.NewLine);
			lock (_transports)
			{
				foreach (KeyValuePair<string, ClientTransport> t in _transports)
				{
					sb.AppendFormat(CultureInfo.InvariantCulture, "{0}    +- '{1}': {2}", Environment.NewLine,
						t.Key, t.Value.ToString().Replace(Environment.NewLine, Environment.NewLine + "       "));
				}
			}

			sb.AppendFormat(CultureInfo.InvariantCulture, "{0}  ]{0}}}", Environment.NewLine);
			return sb.ToString();
		}
	}
}
