using System;
using System.Collections.Generic;

namespace CometD.Bayeux
{
	/// <summary>
	/// <p>A transport abstract the details of the protocol used to send
	/// Bayeux messages over the network.</p>
	/// <p>Transports have well known names and both a Bayeux client
	/// and a Bayeux server can negotiate the transport they want to use by
	/// exchanging the list of supported transport names.</p>
	/// <p>Transports can be configured using <em>options</em>. The transport
	/// implementation provides a set of option names that
	/// it uses to configure itself and an option prefix
	/// that allows specific tuning of the configuration.<br/>
	/// Option prefixes may be composed of segments separated by the "." character.</p>
	/// <p>For example, imagine to configure the transports for normal long polling,
	/// for JSONP long polling and for WebSocket. All provide a common option name
	/// called "timeout" and the JSONP long polling transport provides also a specific
	/// option name called "callback".<br/>
	/// The normal long polling transport has prefix "long-polling.json",
	/// the JSONP long polling transport has prefix "long-polling.jsonp" and the
	/// WebSocket long polling transport has prefix "ws". The first two prefixes
	/// have 2 segments.</p>
	/// <p>The configurator will asks the transports the set of option names, obtaining
	/// ["timeout", "callback"]; then will ask each transport its prefix, obtaining
	/// ["long-polling.json", "long-polling.jsonp"].<br/>
	/// The configurator can now look in the configuration (for example a properties
	/// file or servlet init parameters) for entries that combine the option names and
	/// option prefix segments, such as:</p>
	/// <ul>
	/// <li>"timeout" => specifies the timeout for all transports</li>
	/// <li>"long-polling.timeout" => specifies the timeout for both normal long polling
	/// transport and JSONP long polling transport, but not for the WebSocket transport</li>
	/// <li>"long-polling.jsonp.timeout" => specifies the timeout for JSONP long polling
	/// transport overriding more generic entries</li>
	/// <li>"ws.timeout" => specifies the timeout for WebSocket transport overriding more
	/// generic entries</li>
	/// <li>"long-polling.jsonp.callback" => specifies the "callback" parameter for the
	/// JSONP long polling transport.</li>
	/// </ul>
	/// </summary>
	public interface ITransport
	{
		/// <summary>
		/// The well known name of this transport, used in transport negotiations.
		/// </summary>
		/// <seealso cref="IBayeux.AllowedTransports"/>
		string Name { get; }

		/// <summary>
		/// Returns the configuration option with the given <paramref name="qualifiedName"/>.
		/// </summary>
		/// <param name="qualifiedName">The configuration option name.</param>
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

		/// <summary>
		/// Specifies an option prefix made of string segments separated by the "."
		/// character, used to override more generic configuration entries.
		/// </summary>
		/// <value>The option prefix for this transport.</value>
		string OptionPrefix { get; }
	}
}
