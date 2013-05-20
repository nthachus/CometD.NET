using System;
using System.Collections.Generic;

using CometD.Bayeux;

namespace CometD.Common
{
	/// <summary>
	/// Partial implementation of <see cref="ITransport"/>.
	/// </summary>
	public abstract class AbstractTransport : ITransport
	{
		private readonly string _name;
		private readonly IDictionary<string, object> _options;

		private volatile string[] _prefix = new string[0];

		/// <summary>
		/// Initializes a new instance of the <see cref="AbstractTransport"/> class.
		/// </summary>
		protected AbstractTransport(string name, IDictionary<string, object> options)
		{
			if (null == name || (name = name.Trim()).Length == 0)
				throw new ArgumentNullException("name");

			_name = name;
			_options = (options != null) ? options
				: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		}

		#region ITransport Members

		/// <summary>
		/// The well known name of this transport, used in transport negotiations.
		/// </summary>
		public virtual string Name
		{
			get { return _name; }
		}

		/// <summary>
		/// Get an option value by searching the option name tree.
		/// The option map obtained by calling <code>BayeuxServer.Options</code>
		/// is searched for the option name with the most specific prefix.
		/// </summary>
		/// <example>
		/// If this transport was initialized with calls:
		/// <pre>
		///   OptionPrefix = "long-polling.jsonp";
		/// </pre>
		/// then a call to GetOption("foobar") will look for the most specific value with names:
		/// <pre>
		///   long-polling.json.foobar
		///   long-polling.foobar
		///   foobar
		/// </pre>
		/// </example>
		public virtual object GetOption(string qualifiedName)
		{
			if (String.IsNullOrEmpty(qualifiedName))
				throw new ArgumentNullException("qualifiedName");

			object result = null;
			_options.TryGetValue(qualifiedName, out result);

			string[] segments = _prefix;
			if (segments != null && segments.Length > 0)
			{
				string prefix = null;
				string key;
				object val;

				foreach (string segment in segments)
				{
					prefix = (prefix == null) ? segment : (prefix + "." + segment);

					key = prefix + "." + qualifiedName;
					if (_options.TryGetValue(key, out val))
						result = val;
				}
			}

			return result;
		}

		/// <summary>
		/// Sets the specified configuration option with the given <paramref name="value"/>.
		/// </summary>
		/// <param name="qualifiedName">The configuration option name.</param>
		/// <param name="value">The configuration option value.</param>
		/// <seealso cref="GetOption(string)"/>
		public virtual void SetOption(string qualifiedName, object value)
		{
			if (String.IsNullOrEmpty(qualifiedName))
				throw new ArgumentNullException("qualifiedName");

			string prefix = this.OptionPrefix;
			lock (_options)
			{
				_options[String.IsNullOrEmpty(prefix)
					? qualifiedName : (prefix + "." + qualifiedName)] = value;
			}
		}

		/// <summary>
		/// The set of configuration options.
		/// </summary>
		public virtual ICollection<string> OptionNames
		{
			get
			{
				ICollection<string> optionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				lock (_options)
				{
					foreach (string name in _options.Keys)
					{
						int lastDot = name.LastIndexOf('.');
						optionNames.Add((lastDot < 0) ? name : name.Substring(lastDot + 1));
					}
				}

				return optionNames;
			}
		}

		/// <summary>
		/// Set the option name prefix segment.
		/// <p>Normally this is called by the super class constructors to establish
		/// a naming hierarchy for options and interacts with the <see cref="SetOption(string, object)"/>
		/// method to create a naming hierarchy for options.</p>
		/// </summary>
		/// <remarks>
		/// The various <see cref="GetOption(String)"/> methods will search this
		/// name tree for the most specific match.
		/// </remarks>
		/// <example>
		/// For example the following sequence of calls:
		/// <pre>
		///   SetOption("foo", "x");
		///   SetOption("bar", "y");
		///   OptionPrefix = "long-polling";
		///   SetOption("foo", "z");
		///   SetOption("whiz", "p");
		///   OptionPrefix = "long-polling.jsonp";
		///   SetOption("bang", "q");
		///   SetOption("bar", "r");
		/// </pre>
		/// will establish the following option names and values:
		/// <pre>
		///   foo: x
		///   bar: y
		///   long-polling.foo: z
		///   long-polling.whiz: p
		///   long-polling.jsonp.bang: q
		///   long-polling.jsonp.bar: r
		/// </pre>
		/// </example>
		public virtual string OptionPrefix
		{
			get
			{
				string[] segments = _prefix;
				return (null == segments) ? null : String.Join(".", segments);
			}
			set
			{
				_prefix = (null == value) ? null
					: ((value.Length == 0) ? new string[0] : value.Split('.'));
			}
		}

		#endregion

		#region GetOption overloaded methods

		/// <summary>
		/// Get option or default value.
		/// </summary>
		/// <param name="optionName">The option name.</param>
		/// <param name="defaultValue">The default value.</param>
		/// <returns>The option or default value.</returns>
		/// <seealso cref="GetOption(string)"/>
		public virtual string GetOption(string optionName, string defaultValue)
		{
			object val = this.GetOption(optionName);
			if (val != null) return val.ToString();

			return defaultValue;
		}

		/// <summary>
		/// Get option or default value.
		/// </summary>
		/// <param name="optionName">The option name.</param>
		/// <param name="defaultValue">The default value.</param>
		/// <returns>The option or default value.</returns>
		/// <seealso cref="GetOption(string)"/>
		public virtual T GetOption<T>(string optionName, T defaultValue) where T : struct
		{
			return ObjectConverter.ToPrimitive<T>(this.GetOption(optionName), defaultValue);
		}

		#endregion

		/// <summary>
		/// Used to debug.
		/// </summary>
		public override string ToString()
		{
#if DEBUG
			return _name + "#" + this.GetHashCode();
#else
			return _name;
#endif
		}
	}
}
