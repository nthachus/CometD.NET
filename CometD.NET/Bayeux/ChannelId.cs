using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace CometD.Bayeux
{
	/// <summary>
	/// <p>Reification of a channel id with methods to test properties
	/// and compare with other <see cref="ChannelId"/>s.</p>
	/// <p>A <see cref="ChannelId"/> breaks the channel id into path segments so that, for example,
	/// <code>"/foo/bar"</code> breaks into <code>["foo","bar"]</code>.</p>
	/// <p><see cref="ChannelId"/> can be wild, when they end with one or two wild characters <code>"*"</code>;
	/// a <see cref="ChannelId"/> is shallow wild if it ends with one wild character (for example <code>"/foo/bar/*"</code>)
	/// and deep wild if it ends with two wild characters (for example <code>"/foo/bar/**"</code>).</p>
	/// </summary>
	[Serializable]
	public sealed class ChannelId : IEquatable<ChannelId>
	{
		private readonly string _id;
		private readonly string[] _segments;
		private readonly int _wild;
		private readonly IList<string> _wilds;
		private readonly string _parent;

		/// <summary>
		/// Constructs a new <see cref="ChannelId"/> with the given id.
		/// </summary>
		/// <param name="id">The channel id in string form.</param>
		private ChannelId(string id)
		{
			if (null == id || (id = id.Trim()).Length == 0)
				throw new ArgumentNullException("id");

			if ("/".Equals(id = id.Replace('\\', '/')) || id[0] != '/' || id.Contains("//"))
				throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Invalid channel id '{0}'.", id));

			id = id.TrimEnd('/');
			_id = id;

			_segments = id.Substring(1).Split('/');

			string lastSegment = _segments[_segments.Length - 1];
			if ("*".Equals(lastSegment))// WILD
				_wild = 1;
			else if ("**".Equals(lastSegment))// DEEPWILD
				_wild = 2;
			else
				_wild = 0;

			if (_wild > 0)
			{
				_wilds = (new List<string>()).AsReadOnly();
			}
			else
			{
				string[] wilds = new string[_segments.Length + 1];
				StringBuilder sb = new StringBuilder("/");

				// [foo, bar] -> [/foo/*, /foo/**, /**]
				for (int i = 0; i < _segments.Length; ++i)
				{
					if (i > 0)
						sb.Append(_segments[i - 1]).Append('/');

					wilds[_segments.Length - i] = sb.ToString() + "**";
				}

				wilds[0] = sb.ToString() + "*";
				_wilds = (new List<string>(wilds)).AsReadOnly();
			}

			_parent = (_segments.Length == 1) ? null : _id.Substring(0, _id.Length - lastSegment.Length - 1);
		}

		/// <summary>
		/// Whether this <see cref="ChannelId"/> is either shallow wild or deep wild.
		/// </summary>
		public bool IsWild
		{
			get { return (_wild > 0); }
		}

		/// <summary>
		/// Shallow wild <see cref="ChannelId"/>s end with a single wild character <code>"*"</code>
		/// and match non wild channels with the same depth.
		/// </summary>
		/// <example>
		/// <code>"/foo/*"</code> matches <code>"/foo/bar"</code>, but not <code>"/foo/bar/baz"</code>.
		/// </example>
		/// <value>Whether this <see cref="ChannelId"/> is a shallow wild channel id.</value>
		public bool IsShallowWild
		{
			get { return this.IsWild && !this.IsDeepWild; }
		}

		/// <summary>
		/// Deep wild <see cref="ChannelId"/>s end with a double wild character "**"
		/// and match non wild channels with the same or greater depth.
		/// </summary>
		/// <example>
		/// <code>"/foo/**"</code> matches <code>"/foo/bar"</code> and <code>"/foo/bar/baz"</code>.
		/// </example>
		/// <value>Whether this <see cref="ChannelId"/> is a deep wild channel id.</value>
		public bool IsDeepWild
		{
			get { return (_wild > 1); }
		}

		/// <summary>
		/// A <see cref="ChannelId"/> is a meta <see cref="ChannelId"/>
		/// if it starts with <code>"/meta/"</code>.
		/// </summary>
		/// <value>Whether the first segment is "meta".</value>
		public bool IsMeta
		{
			get { return Channel.IsMeta(_id); }
		}

		/// <summary>
		/// A <see cref="ChannelId"/> is a service <see cref="ChannelId"/>
		/// if it starts with <code>"/service/"</code>.
		/// </summary>
		/// <value>Whether the first segment is "service".</value>
		public bool IsService
		{
			get { return Channel.IsService(_id); }
		}

		/// <summary>
		/// Returns whether this <see cref="ChannelId"/>
		/// is neither <see cref="IsMeta">meta</see> nor <see cref="IsService">service</see>.
		/// </summary>
		public bool IsBroadcast
		{
			get { return Channel.IsBroadcast(_id); }
		}

		/// <summary>
		/// <p>Tests whether this <see cref="ChannelId"/> matches the given <see cref="ChannelId"/>.</p>
		/// <p>If the given <see cref="ChannelId"/> is wild,
		/// then it matches only if it is equal to this <see cref="ChannelId"/>.</p>
		/// <p>If this <see cref="ChannelId"/> is non-wild,
		/// then it matches only if it is equal to the given <see cref="ChannelId"/>.</p>
		/// <p>Otherwise, this <see cref="ChannelId"/> is either shallow or deep wild, and
		/// matches <see cref="ChannelId"/>s with the same number of equal segments (if it is
		/// shallow wild), or <see cref="ChannelId"/>s with the same or a greater number of
		/// equal segments (if it is deep wild).</p>
		/// </summary>
		/// <param name="channelId">The channelId to match.</param>
		/// <returns>True if this <see cref="ChannelId"/> matches the given <see cref="ChannelId"/>.</returns>
		public bool Matches(ChannelId channelId)
		{
			if (null == channelId) return false;

			if (channelId.IsWild || _wild <= 0)
				return this.Equals(channelId);

			if (_wild > 1)// DEEPWILD
			{
				if (_segments.Length >= channelId._segments.Length)
					return false;
			}
			else// WILD
			{
				if (_segments.Length != channelId._segments.Length)
					return false;
			}

			for (int i = _segments.Length - 2; i >= 0; i--)
			{
				if (!_segments[i].Equals(channelId._segments[i], StringComparison.OrdinalIgnoreCase))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Returns how many segments this <see cref="ChannelId"/> is made of.
		/// </summary>
		/// <seealso cref="GetSegment(int)"/>
		public int Depth
		{
			get { return _segments.Length; }
		}

		/// <summary>
		/// Returns whether this <see cref="ChannelId"/> is an ancestor
		/// of the given <see cref="ChannelId"/>.
		/// </summary>
		/// <param name="id">The channel to test.</param>
		/// <seealso cref="IsParentOf(ChannelId)"/>
		public bool IsAncestorOf(ChannelId id)
		{
			// Is root of another channel?
			if (null == id) return false;

			if (this.IsWild || this.Depth >= id.Depth)
				return false;

			for (int i = _segments.Length - 1; i >= 0; i--)
			{
				if (!_segments[i].Equals(id._segments[i], StringComparison.OrdinalIgnoreCase))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Returns whether this <see cref="ChannelId"/> is the parent
		/// of the given <see cref="ChannelId"/>.
		/// </summary>
		/// <param name="id">The channel to test.</param>
		/// <seealso cref="IsAncestorOf(ChannelId)"/>
		public bool IsParentOf(ChannelId id)
		{
			return (null != id && this.IsAncestorOf(id) && this.Depth == id.Depth - 1);
		}

		/// <summary>
		/// Returns the <see cref="ChannelId"/> parent of this <see cref="ChannelId"/>.
		/// </summary>
		/// <seealso cref="IsParentOf(ChannelId)"/>
		public string Parent
		{
			get { return _parent; }
		}

		/// <summary>
		/// Returns the index-nth segment of this channel,
		/// or null if no such segment exist.
		/// </summary>
		/// <param name="index">The segment index.</param>
		/// <seealso cref="Depth"/>
		public string GetSegment(int index)
		{
			return (index < 0 || index >= _segments.Length) ? null : _segments[index];
		}

		/// <summary>
		/// The list of wilds channels that match this channel,
		/// or the empty list if this channel is already wild.
		/// </summary>
		public IList<string> Wilds
		{
			get { return _wilds; }
		}

		#region IEquatable Members

		/// <summary>
		/// Returns a <see cref="String"/> that represents the current <see cref="ChannelId"/>.
		/// </summary>
		public override string ToString()
		{
			return _id;
		}

		/// <summary>
		/// Determines whether this <see cref="ChannelId"/> and a specified
		/// <see cref="ChannelId"/> object have the same value.
		/// </summary>
		public bool Equals(ChannelId other)
		{
			return (null == other) ? false
				: _id.Equals(other._id, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Determines whether the specified <see cref="Object"/>
		/// is equal to the current <see cref="ChannelId"/>.
		/// </summary>
		public override bool Equals(object obj)
		{
			if (null == obj) return false;

			ChannelId that = obj as ChannelId;
			if (null != that) return this.Equals(that);

			return base.Equals(obj);
		}

		/// <summary>
		/// Returns the hash code for this <see cref="ChannelId"/>.
		/// </summary>
		public override int GetHashCode()
		{
			return _id.GetHashCode();
		}

		#endregion

		private static readonly IDictionary<string, ChannelId> _instances
			= new Dictionary<string, ChannelId>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Retrieves the cached <see cref="ChannelId"/> or create it if it does not exist.
		/// </summary>
		public static ChannelId Create(string id)
		{
			if (null == id || (id = id.Trim()).Length == 0)
				throw new ArgumentNullException("id");

			lock (_instances)
			{
				if (!_instances.ContainsKey(id))
					_instances.Add(id, new ChannelId(id));

				return _instances[id];
			}
		}
	}
}
