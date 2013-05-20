using System;
using System.Collections.Generic;

namespace CometD.Bayeux
{
	/// <summary>
	/// <p>This interface is the common API for both client-side and
	/// server-side configuration and usage of the Bayeux object.</p>
	/// <p>The <see cref="IBayeux"/> object handles configuration options and a set of
	/// transports that is negotiated with the server.</p>
	/// </summary>
	/// <seealso cref="ITransport"/>
	public interface IBayeux
	{
		/// <summary>
		/// The set of known transport names of this <see cref="IBayeux"/> object.
		/// </summary>
		/// <seealso cref="AllowedTransports"/>
		ICollection<string> KnownTransportNames { get; }

		/// <summary>
		/// The transport with the given name or null if no such transport exist.
		/// </summary>
		/// <param name="transport">The transport name.</param>
		ITransport GetTransport(string transport);

		/// <summary>
		/// The ordered list of transport names that will be used in the
		/// negotiation of transports with the other peer.
		/// </summary>
		/// <seealso cref="KnownTransportNames"/>
		ICollection<string> AllowedTransports { get; }

		/// <summary>
		/// Gets the configuration option with the given <paramref name="qualifiedName"/>.
		/// </summary>
		/// <param name="qualifiedName">The configuration option name.</param>
		/// <seealso cref="SetOption(string, object)"/>
		/// <seealso cref="OptionNames"/>
		object GetOption(string qualifiedName);

		/// <summary>
		/// Sets the specified configuration option with the given <paramref name="value"/>.
		/// </summary>
		/// <param name="qualifiedName">The configuration option name.</param>
		/// <param name="value">The configuration option value.</param>
		/// <seealso cref="GetOption(string)"/>
		void SetOption(string qualifiedName, object value);

		/// <summary>
		/// The set of configuration options.
		/// </summary>
		/// <seealso cref="GetOption(string)"/>
		ICollection<string> OptionNames { get; }
	}

	/// <summary>
	/// <p>The common base interface for Bayeux listeners.</p>
	/// <p>Specific sub-interfaces define what kind of events listeners will be notified.</p>
	/// </summary>
	public interface IBayeuxListener
	{
	}
}
