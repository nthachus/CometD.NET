using System;
using System.Collections.Generic;

using CometD.Common;
using CometD.Bayeux;
using CometD.Bayeux.Client;

namespace CometD.Client
{
	/// <summary>
	/// Provides a Bayeux client implementation of the <see cref="AbstractSessionChannel"/> class.
	/// </summary>
	public class BayeuxClientChannel : AbstractSessionChannel
	{
		private readonly BayeuxClient _bayeuxClient;

		/// <summary>
		/// Initializes a new instance of the <see cref="BayeuxClientChannel"/> class
		/// with the specified <see cref="ChannelId"/>.
		/// </summary>
		public BayeuxClientChannel(BayeuxClient session, ChannelId channelId)
			: base(channelId)
		{
			if (null == session)
				throw new ArgumentNullException("session");

			_bayeuxClient = session;
		}

		/// <summary>
		/// The client session associated with this channel.
		/// </summary>
		public override IClientSession Session
		{
			get { return _bayeuxClient; }
		}

		/// <summary>
		/// Send subscription message(s) to Bayeux server
		/// to subscribe this session channel with all assigned message listeners.
		/// </summary>
		protected override void SendSubscribe()
		{
			if (this.ChannelId.IsWild) return;

			IMutableMessage message = _bayeuxClient.NewMessage();
			message.Channel = Channel.MetaSubscribe;
			message[Message.SubscriptionField] = this.Id;

			_bayeuxClient.EnqueueSend(message);
		}

		/// <summary>
		/// Send un-subscription message(s) to Bayeux server
		/// to un-subscribe all assigned message listeners from this session channel.
		/// </summary>
		protected override void SendUnsubscribe()
		{
			if (this.ChannelId.IsWild) return;

			IMutableMessage message = _bayeuxClient.NewMessage();
			message.Channel = Channel.MetaUnsubscribe;
			message[Message.SubscriptionField] = this.Id;

			_bayeuxClient.EnqueueSend(message);
		}

		/// <summary>
		/// Publishes the given <paramref name="data"/> to this channel,
		/// optionally specifying the <paramref name="messageId"/> to set on the publish message.
		/// </summary>
		/// <param name="data">The data to publish.</param>
		/// <param name="messageId">The message id to set on the message,
		/// or null to let the implementation choose the message id.</param>
		public override void Publish(object data, string messageId)
		{
			if (this.ChannelId.IsWild) return;

			IMutableMessage message = _bayeuxClient.NewMessage();
			message.Channel = this.Id;
			message.Data = data;
			if (!String.IsNullOrEmpty(messageId))
				message.Id = messageId;

			_bayeuxClient.EnqueueSend(message);
		}
	}
}
