using System;
using System.Collections.Generic;

namespace CometD.Bayeux
{
	/// <summary>
	/// Bayeux channel constants.
	/// </summary>
	public static class Channel
	{
		/// <summary>
		/// Constant representing the meta prefix.
		/// </summary>
		public const string Meta = "/meta";

		/// <summary>
		/// Constant representing the handshake meta channel.
		/// </summary>
		public const string MetaHandshake = Meta + "/handshake";

		/// <summary>
		/// Constant representing the connect meta channel.
		/// </summary>
		public const string MetaConnect = Meta + "/connect";

		/// <summary>
		/// Constant representing the subscribe meta channel.
		/// </summary>
		public const string MetaSubscribe = Meta + "/subscribe";

		/// <summary>
		/// Constant representing the unsubscribe meta channel.
		/// </summary>
		public const string MetaUnsubscribe = Meta + "/unsubscribe";

		/// <summary>
		/// Constant representing the disconnect meta channel.
		/// </summary>
		public const string MetaDisconnect = Meta + "/disconnect";


		/// <summary>
		/// Constant representing the "service" prefix.
		/// </summary>
		public const string Service = "/service";

		/// <summary>
		/// Constant representing the "topic" prefix.
		/// </summary>
		public const string Topic = "/topic";

		/// <summary>
		/// Helper method to test if the string form of a <see cref="ChannelId"/>
		/// represents a meta <see cref="ChannelId"/>.
		/// </summary>
		/// <param name="channelId">The channel id to test.</param>
		/// <returns>Whether the given channel id is a meta channel id.</returns>
		public static bool IsMeta(string channelId)
		{
			return channelId != null
				&& channelId.StartsWith(Meta + "/", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Helper method to test if the string form of a <see cref="ChannelId"/>
		/// represents a service <see cref="ChannelId"/>.
		/// </summary>
		/// <param name="channelId">The channel id to test.</param>
		/// <returns>Whether the given channel id is a service channel id.</returns>
		public static bool IsService(string channelId)
		{
			return channelId != null
				&& channelId.StartsWith(Service + "/", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Helper method to test if the string form of a <see cref="ChannelId"/>
		/// represents a broadcast <see cref="ChannelId"/>.
		/// </summary>
		/// <param name="channelId">The channel id to test.</param>
		/// <returns>Whether the given channel id is a broadcast channel id.</returns>
		public static bool IsBroadcast(string channelId)
		{
			return (channelId != null && !IsMeta(channelId) && !IsService(channelId));
		}
	}

	/// <summary>
	/// <p>A Bayeux channel is the primary message routing mechanism within Bayeux:
	/// both Bayeux clients and Bayeux server use channels to group listeners that
	/// are interested in receiving messages with that channel.</p>
	///
	/// <p>This interface is the common root for both the client side
	/// representation of a channel and the server side representation of a channel.</p>
	///
	/// <p>Channels are identified with strings that look like paths (e.g. "/foo/bar")
	/// called "channel id".<br/>
	/// Meta channels have channel ids starting with "/meta/" and are reserved for the
	/// operation of they Bayeux protocol.<br/>
	/// Service channels have channel ids starting with "/service/" and are channels
	/// for which publish is disabled, so that only server side listeners will receive
	/// the messages.</p>
	///
	/// <p>A channel id may also be specified with wildcards.<br/>
	/// For example "/meta/*" refers to all top level meta channels
	/// like "/meta/subscribe" or "/meta/handshake".<br/>
	/// The channel "/foo/**" is deeply wild and refers to all channels like "/foo/bar",
	/// "/foo/bar/bob" and "/foo/bar/wibble/bip".<br/>
	/// Wildcards can only be specified as last segment of a channel; therefore channel
	/// "/foo/*/bar/**" is an invalid channel.</p>
	/// </summary>
	public interface IChannel
	{
		/// <summary>
		/// The channel id as a string.
		/// </summary>
		string Id { get; }

		/// <summary>
		/// The channel ID as a <see cref="ChannelId"/>.
		/// </summary>
		ChannelId ChannelId { get; }

		/// <summary>
		/// Tells whether the channel is a meta channel, that is if its
		/// id starts with <code>"/meta/"</code>.
		/// </summary>
		/// <value>True if the channel is a meta channel.</value>
		bool IsMeta { get; }

		/// <summary>
		/// Tells whether the channel is a service channel, that is if its
		/// id starts with <code>"/service/"</code>.
		/// </summary>
		/// <value>True if the channel is a service channel.</value>
		bool IsService { get; }

		/// <summary>
		/// A broadcasting channel is a channel that is neither a
		/// meta channel nor a service channel.
		/// </summary>
		/// <value>Whether the channel is a broadcasting channel.</value>
		bool IsBroadcast { get; }

		/// <summary>
		/// Tells whether a channel contains the wild character '*', for example
		/// <code>"/foo/*"</code> or if it is <see cref="IsDeepWild"/>.
		/// </summary>
		/// <value>True if the channel contains the '*' or '**' characters.</value>
		bool IsWild { get; }

		/// <summary>
		/// Tells whether a channel contains the deep wild characters '**', for example
		/// <code>"/foo/**"</code>.
		/// </summary>
		/// <value>True if the channel contains the '**' characters.</value>
		bool IsDeepWild { get; }

		/// <summary>
		/// <p>Sets a named channel attribute value.</p>
		/// <p>Channel attributes are convenience data that allows arbitrary
		/// application data to be associated with a channel.</p>
		/// </summary>
		/// <param name="name">The attribute name.</param>
		/// <param name="value">The attribute value.</param>
		void SetAttribute(string name, object value);

		/// <summary>
		/// Retrieves the value of named channel attribute.
		/// </summary>
		/// <param name="name">The name of the attribute.</param>
		/// <returns>The attribute value or null if the attribute is not present.</returns>
		object GetAttribute(string name);

		/// <summary>
		/// The list of channel attribute names.
		/// </summary>
		ICollection<string> AttributeNames { get; }

		/// <summary>
		/// Removes a named channel attribute.
		/// </summary>
		/// <param name="name">The name of the attribute.</param>
		/// <returns>The value of the attribute.</returns>
		object RemoveAttribute(string name);
	}
}
