using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Runtime.Serialization;

using CometD.Bayeux;

namespace CometD.Common
{
	/// <summary>
	/// Implements the interface <see cref="IMutableMessage"/> with a <see cref="T:Dictionary&lt;string, object&gt;"/>.
	/// </summary>
	[Serializable]
	public sealed class DictionaryMessage : Dictionary<string, object>, IMutableMessage
	{
		//private const long serialVersionUID = 4318697940670212190L;

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="DictionaryMessage"/> class.
		/// </summary>
		public DictionaryMessage() : base(StringComparer.OrdinalIgnoreCase) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="DictionaryMessage"/> class
		/// that contains elements copied from the specified <see cref="T:IDictionary&lt;string, object&gt;"/>.
		/// </summary>
		public DictionaryMessage(IDictionary<string, object> message)
			: base(message, StringComparer.OrdinalIgnoreCase) { }

		private DictionaryMessage(SerializationInfo info, StreamingContext context)
			: base(info, context) { }

		#endregion

		#region IMessage Properties

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.AdviceField"/>.
		/// </summary>
		public IDictionary<string, object> Advice
		{
			get
			{
				return this.GetObjectValue<IDictionary<string, object>>(Message.AdviceField);
			}
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ChannelField"/>.
		/// </summary>
		/// <remarks>Bayeux message always have a non null channel.</remarks>
		public string Channel
		{
			get
			{
				object val;
				return (this.TryGetValue(Message.ChannelField, out val) && val != null)
					? val.ToString() : null;
			}
			set
			{
				this.SetOrDeleteValue(Message.ChannelField, value);
			}
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ChannelField"/>.
		/// </summary>
		/// <remarks>Bayeux message always have a non null channel.</remarks>
		public ChannelId ChannelId
		{
			get
			{
				string ch = this.Channel;
				return (ch == null || (ch = ch.Trim()).Length == 0)
					? null : Bayeux.ChannelId.Create(ch);
			}
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ClientIdField"/>.
		/// </summary>
		public string ClientId
		{
			get
			{
				object val;
				return (this.TryGetValue(Message.ClientIdField, out val) && val != null)
					? val.ToString() : null;
			}
			set
			{
				this.SetOrDeleteValue(Message.ClientIdField, value);
			}
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.DataField"/>.
		/// </summary>
		public object Data
		{
			get
			{
				object val;
				return this.TryGetValue(Message.DataField, out val) ? val : null;
			}
			set
			{
				this.SetOrDeleteValue(Message.DataField, value);
			}
		}

		/// <summary>
		/// The data of the message as a <code>Dictionary</code>.
		/// </summary>
		public IDictionary<string, object> DataAsDictionary
		{
			get
			{
				return this.GetObjectValue<IDictionary<string, object>>(Message.DataField);
			}
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ExtensionField"/>.
		/// </summary>
		public IDictionary<string, object> Extension
		{
			get
			{
				return this.GetObjectValue<IDictionary<string, object>>(Message.ExtensionField);
			}
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.IdField"/>.
		/// </summary>
		/// <remarks>Support also old-style ids of type long.</remarks>
		public string Id
		{
			get
			{
				object val;
				if (this.TryGetValue(Message.IdField, out val) && val != null)
					return val.ToString();

				return null;
			}
			set
			{
				this.SetOrDeleteValue(Message.IdField, value);
			}
		}

		/// <summary>
		/// Whether the channel's message is a meta channel.
		/// </summary>
		public bool IsMeta
		{
			get { return Bayeux.Channel.IsMeta(this.Channel); }
		}

		/// <summary>
		/// Publish message replies contain the "successful" field.
		/// </summary>
		/// <value>Whether this message is a publish reply (as opposed to a published message).</value>
		public bool IsPublishReply
		{
			get { return (!this.IsMeta && this.ContainsKey(Message.SuccessfulField)); }
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.SuccessfulField"/>.
		/// </summary>
		public bool IsSuccessful
		{
			get
			{
				return this.GetValue<bool>(Message.SuccessfulField, false);
			}
			set
			{
				lock (this) this[Message.SuccessfulField] = value;
			}
		}

		#endregion

		#region IMutableMessage Members

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.AdviceField"/> and create it if it does not exist.
		/// </summary>
		public IDictionary<string, object> GetAdvice(bool create)
		{
			IDictionary<string, object> result = this.Advice;
			if (create && result == null)
			{
				result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
				lock (this) this[Message.AdviceField] = result;
			}

			return result;
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.DataField"/> and create it if it does not exist.
		/// </summary>
		public IDictionary<string, object> GetDataAsDictionary(bool create)
		{
			IDictionary<string, object> result = this.DataAsDictionary;
			if (create && result == null)
			{
				result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
				lock (this) this[Message.DataField] = result;
			}

			return result;
		}

		/// <summary>
		/// Convenience method to retrieve the <see cref="Message.ExtensionField"/> and create it if it does not exist.
		/// </summary>
		public IDictionary<string, object> GetExtension(bool create)
		{
			IDictionary<string, object> result = this.Extension;
			if (create && result == null)
			{
				result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
				lock (this) this[Message.ExtensionField] = result;
			}

			return result;
		}

		#endregion

		#region IDictionary<string, object> Extension Methods

		private void SetOrDeleteValue(string key, object value)
		{
			lock (this)
			{
				if (value == null)
				{
					if (this.ContainsKey(key))
						this.Remove(key);
				}
				else
					this[key] = value;
			}
		}

		private T GetValue<T>(string key, T defaultValue) where T : struct
		{
			object val;
			return this.TryGetValue(key, out val)
				? ObjectConverter.ToPrimitive<T>(val, defaultValue) : defaultValue;
		}

		private T GetObjectValue<T>(string key, T defaultValue = null) where T : class
		{
			object val;
			return this.TryGetValue(key, out val)	// Updates value back to the dictionary
				? ObjectConverter.ToObject<T>(val, defaultValue, o => { lock (this) this[key] = o; })
				: defaultValue;
		}

		#endregion

		#region IEquatable Members

		/// <summary>
		/// Represents this message as a JSON string.
		/// </summary>
		public override string ToString()
		{
			return ObjectConverter.Serialize(this as IDictionary<string, object>);
		}

		#endregion

		/// <summary>
		/// Converts the specified JSON string to a list of <see cref="IMutableMessage"/>.
		/// </summary>
		public static IList<IMutableMessage> ParseMessages(string content)
		{
			if (null == content || (content = content.Trim()).Length == 0)
				return null;

			IList<IDictionary<string, object>> list = null;
			bool tryNext = false;
			try
			{
				list = ObjectConverter.Deserialize<IList<IDictionary<string, object>>>(content);
			}
			catch (ArgumentException) { tryNext = true; }
			catch (InvalidOperationException) { tryNext = true; }

			if (tryNext)
			{
				// DEBUG
				//Trace.TraceInformation("Try to parse to IDictionary from JSON content: {0}", content);
				try
				{
					IDictionary<string, object> obj
						= ObjectConverter.Deserialize<IDictionary<string, object>>(content);

					if (null != obj)
					{
						list = new List<IDictionary<string, object>>();
						list.Add(obj);
					}
				}
				catch (ArgumentException ex)
				{
					// DEBUG
					Trace.TraceWarning("Failed to parse invalid JSON content: {1}{0}{2}",
						Environment.NewLine, content, ex.ToString());
				}
				catch (InvalidOperationException ex)
				{
					// DEBUG
					Trace.TraceWarning("Exception when parsing to IDictionary from JSON content: {1}{0}{2}",
						Environment.NewLine, content, ex.ToString());
				}
			}

			IList<IMutableMessage> result = new List<IMutableMessage>();
			if (list != null)
			{
				foreach (IDictionary<string, object> message in list)
				{
					if (message != null && message.Count > 0)
						result.Add(new DictionaryMessage(message));
				}
			}

			return result;
		}
	}
}
