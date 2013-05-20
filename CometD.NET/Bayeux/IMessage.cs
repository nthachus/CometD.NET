using System;
using System.Collections.Generic;

namespace CometD.Bayeux
{
	/// <summary>
	/// Bayeux message fields and enumeration values.
	/// </summary>
	public static class Message
	{
		/// <summary>
		/// Constant representing the message client-id field.
		/// </summary>
		public const string ClientIdField = "clientId";

		/// <summary>
		/// Constant representing the message data field.
		/// </summary>
		public const string DataField = "data";

		/// <summary>
		/// Constant representing the message channel field.
		/// </summary>
		public const string ChannelField = "channel";

		/// <summary>
		/// Constant representing the message id field.
		/// </summary>
		public const string IdField = "id";

		/// <summary>
		/// Constant representing the message error field.
		/// </summary>
		public const string ErrorField = "error";

		/// <summary>
		/// Constant representing the message timestamp field.
		/// </summary>
		public const string TimestampField = "timestamp";

		/// <summary>
		/// Constant representing the message transport field.
		/// </summary>
		public const string TransportField = "transport";

		/// <summary>
		/// Constant representing the message advice field.
		/// </summary>
		public const string AdviceField = "advice";

		/// <summary>
		/// Constant representing the message successful field.
		/// </summary>
		public const string SuccessfulField = "successful";

		/// <summary>
		/// Constant representing the message subscription field.
		/// </summary>
		public const string SubscriptionField = "subscription";

		/// <summary>
		/// Constant representing the message extension field.
		/// </summary>
		public const string ExtensionField = "ext";

		/// <summary>
		/// Constant representing the message connection-type field.
		/// </summary>
		public const string ConnectionTypeField = "connectionType";

		/// <summary>
		/// Constant representing the message version field.
		/// </summary>
		public const string VersionField = "version";

		/// <summary>
		/// Constant representing the message minimum-version field.
		/// </summary>
		public const string MinVersionField = "minimumVersion";

		/// <summary>
		/// Constant representing the message supported-connection-types field.
		/// </summary>
		public const string SupportedConnectionTypesField = "supportedConnectionTypes";

		/// <summary>
		/// Constant representing the message reconnect field.
		/// </summary>
		public const string ReconnectField = "reconnect";

		/// <summary>
		/// Constant representing the message interval field.
		/// </summary>
		public const string IntervalField = "interval";


		/// <summary>
		/// Constant representing the message timeout field.
		/// </summary>
		public const string TimeoutField = "timeout";

		/// <summary>
		/// Constant representing the message "message" field that contain sent failed messages.
		/// </summary>
		public const string MessageField = "message";

		/// <summary>
		/// Constant representing the message "exception" field.
		/// </summary>
		public const string ExceptionField = "exception";


		/// <summary>
		/// Constant representing the message reconnect retry value.
		/// </summary>
		public const string ReconnectRetryValue = "retry";

		/// <summary>
		/// Constant representing the message reconnect handshake value.
		/// </summary>
		public const string ReconnectHandshakeValue = "handshake";

		/// <summary>
		/// Constant representing the message reconnect none value.
		/// </summary>
		public const string ReconnectNoneValue = "none";
	}

	/// <summary>
	/// <p>The Bayeux protocol exchange information by means of messages.</p>
	/// <p>This interface represents the API of a Bayeux message, and consists
	/// mainly of convenience methods to access the known fields of the message dictionary.</p>
	/// <p>This interface comes in both an immutable and mutable versions.<br/>
	/// Mutability may be deeply enforced by an implementation, so that it is not correct
	/// to cast a passed Message, to a Message.Mutable, even if the implementation allows this.</p>
	/// </summary>
	public interface IMessage : IDictionary<string, object>
	{
		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.AdviceField"/>.
		/// </summary>
		/// <value>The advice of the message.</value>
		IDictionary<string, object> Advice { get; }

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ChannelField"/>.
		/// </summary>
		/// <remarks>Bayeux message always have a non null channel.</remarks>
		/// <value>The channel of the message.</value>
		string Channel { get; }

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ChannelField"/>.
		/// </summary>
		/// <remarks>Bayeux message always have a non null channel.</remarks>
		/// <value>The channel of the message.</value>
		ChannelId ChannelId { get; }

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ClientIdField"/>.
		/// </summary>
		/// <value>The client id of the message.</value>
		string ClientId { get; }

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.DataField"/>.
		/// </summary>
		/// <value>The data of the message.</value>
		/// <seealso cref="DataAsDictionary"/>
		object Data { get; }

		/// <summary>
		/// A messages that has a meta channel is dubbed a "meta message".
		/// </summary>
		/// <value>Whether the channel's message is a meta channel.</value>
		bool IsMeta { get; }

		/// <summary>
		/// Publish message replies contain the "successful" field.
		/// </summary>
		/// <value>Whether this message is a publish reply (as opposed to a published message).</value>
		bool IsPublishReply { get; }

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.SuccessfulField"/>.
		/// </summary>
		/// <value>Whether the message is successful.</value>
		bool IsSuccessful { get; }

		/// <summary>
		/// The data of the message as a <code>Dictionary</code>.
		/// </summary>
		/// <seealso cref="Data"/>
		IDictionary<string, object> DataAsDictionary { get; }

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ExtensionField"/>.
		/// </summary>
		/// <value>The extension of the message.</value>
		IDictionary<string, object> Extension { get; }

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.IdField"/>.
		/// </summary>
		/// <value>The id of the message.</value>
		string Id { get; }
	}

	/// <summary>
	/// The mutable version of a <see cref="IMessage"/>.
	/// </summary>
	public interface IMutableMessage : IMessage
	{
		#region Overwritten Properties

		/// <summary>
		/// The channel of this message.
		/// </summary>
		new string Channel { get; set; }

		/// <summary>
		/// The client id of this message.
		/// </summary>
		new string ClientId { get; set; }

		/// <summary>
		/// The data of this message.
		/// </summary>
		new object Data { get; set; }

		/// <summary>
		/// The id of this message.
		/// </summary>
		new string Id { get; set; }

		/// <summary>
		/// The successfulness of this message.
		/// </summary>
		new bool IsSuccessful { get; set; }

		#endregion

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.AdviceField"/> and create it if it does not exist.
		/// </summary>
		/// <param name="create">Whether to create the advice field if it does not exist.</param>
		/// <returns>The advice of the message.</returns>
		IDictionary<string, object> GetAdvice(bool create);

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.DataField"/> and create it if it does not exist.
		/// </summary>
		/// <param name="create">Whether to create the data field if it does not exist.</param>
		/// <returns>The data of the message.</returns>
		IDictionary<string, object> GetDataAsDictionary(bool create);

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ExtensionField"/> and create it if it does not exist.
		/// </summary>
		/// <param name="create">Whether to create the extension field if it does not exist.</param>
		/// <returns>The extension of the message.</returns>
		IDictionary<string, object> GetExtension(bool create);
	}

}
