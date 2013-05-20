using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using System.Web.Script.Serialization;

namespace CometD.Common
{
	/// <summary>
	/// Converts an object from one object type to another object type.
	/// </summary>
	public static class ObjectConverter
	{
		/// <summary>
		/// Unsigned integer number style.
		/// </summary>
		public static readonly NumberStyles UnsignedNumberStyle
			= NumberStyles.HexNumber | NumberStyles.AllowParentheses
				| NumberStyles.AllowThousands | NumberStyles.AllowExponent;

		/// <summary>
		/// Signed integer number style.
		/// </summary>
		public static readonly NumberStyles IntegerNumberStyle
			= NumberStyles.Integer | NumberStyles.AllowTrailingSign | NumberStyles.AllowParentheses
				| NumberStyles.AllowThousands | NumberStyles.AllowExponent | NumberStyles.AllowHexSpecifier;

		private static readonly IDictionary<Type, Func<string, object>> parserLookup
			= new Dictionary<Type, Func<string, object>>()
			{
				{ typeof(bool), s => bool.Parse(s) },
				//
				{ typeof(byte), s => byte.Parse(s, UnsignedNumberStyle, CultureInfo.CurrentCulture) },
				{ typeof(sbyte), s => sbyte.Parse(s, IntegerNumberStyle, CultureInfo.CurrentCulture) },
				{ typeof(short), s => short.Parse(s, IntegerNumberStyle, CultureInfo.CurrentCulture) },
				{ typeof(ushort), s => ushort.Parse(s, UnsignedNumberStyle, CultureInfo.CurrentCulture) },
				{ typeof(int), s => int.Parse(s, IntegerNumberStyle, CultureInfo.CurrentCulture) },
				{ typeof(uint), s => uint.Parse(s, UnsignedNumberStyle, CultureInfo.CurrentCulture) },
				{ typeof(long), s => long.Parse(s, IntegerNumberStyle, CultureInfo.CurrentCulture) },
				{ typeof(ulong), s => ulong.Parse(s, UnsignedNumberStyle, CultureInfo.CurrentCulture) },
				//
				{ typeof(decimal), s => decimal.Parse(s, IntegerNumberStyle | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture) },
				{ typeof(float), s => float.Parse(s, IntegerNumberStyle | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture) },
				{ typeof(double), s => double.Parse(s, IntegerNumberStyle | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture) },
				//
				{ typeof(DateTime), s => DateTime.Parse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces) },
			};

		private static readonly JavaScriptSerializer jsonParser = new JavaScriptSerializer();

		/// <summary>
		/// Converts the specified JSON string to an object of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of the resulting object.</typeparam>
		/// <param name="content">The JSON string to be deserialized.</param>
		/// <returns>The deserialized object.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
		/// <exception cref="ArgumentException">Invalid JSON <paramref name="content"/> string.</exception>
		/// <exception cref="InvalidOperationException">It is not possible to convert input to <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(string content)
		{
			return jsonParser.Deserialize<T>(content);
		}

		/// <summary>
		/// Converts an object to a JSON string.
		/// </summary>
		/// <param name="value">The object to serialize.</param>
		/// <returns>The serialized JSON string.</returns>
		/// <exception cref="ArgumentException">The recursion limit was exceeded.</exception>
		/// <exception cref="InvalidOperationException">The resulting JSON string exceeds MaxJsonLength.
		/// -or- <paramref name="value"/> contains a circular reference.</exception>
		public static string Serialize(object value)
		{
			return jsonParser.Serialize(value);
		}

		/// <summary>
		/// Converts a specified <see cref="Object"/> to a primitive type
		/// like <see cref="Boolean"/>, <see cref="Int32"/>, <see cref="Int64"/>,..
		/// </summary>
		public static T ToPrimitive<T>(object value, T defaultValue) where T : struct
		{
			if (value != null)
			{
				Type type = typeof(T);
				try
				{
					return (T)Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
				}
				catch (InvalidCastException)
				{
					string s = value.ToString().Trim();
					if (!String.IsNullOrEmpty(s) && parserLookup.ContainsKey(type))
					{
						try
						{
							return (T)parserLookup[type](s);
						}
						catch (ArgumentException ex)
						{
							// DEBUG
							Trace.TraceWarning("Failed to parse invalid value '{1}' of type {2}:{0}{3}",
								Environment.NewLine, s, type.Name, ex.ToString());
						}
						catch (FormatException ex)
						{
							// DEBUG
							Trace.TraceWarning("Failed to parse invalid-format value '{1}' of type {2}:{0}{3}",
								Environment.NewLine, s, type.Name, ex.ToString());
						}
						catch (OverflowException ex)
						{
							// DEBUG
							Trace.TraceWarning("Failed to parse overflow value '{1}' of type {2}:{0}{3}",
								Environment.NewLine, s, type.Name, ex.ToString());
						}
					}
				}
			}

			return defaultValue;
		}

		/// <summary>
		/// Converts a specified <see cref="Object"/> to a generic object type
		/// like <see cref="IList"/>, <see cref="IDictionary"/>,..
		/// </summary>
		public static T ToObject<T>(object value, T defaultValue = null, Action<T> finallyCallback = null) where T : class
		{
			if (null != value)
			{
				T result = value as T;
				if (result == null)
				{
					string s = value as string;
					try
					{
						if (s != null) s = s.Trim();
						else
						{
							// DEBUG
							Trace.TraceWarning("Invalid cast from type: {0} to type: {1}", value.GetType(), typeof(T));
							s = jsonParser.Serialize(value);
						}

						result = jsonParser.Deserialize<T>(s);
					}
					catch (ArgumentException ex)
					{
						// DEBUG
						Trace.TraceWarning("Failed to parse invalid JSON value: {1}{0}{2}",
							Environment.NewLine, s, ex.ToString());
					}
					catch (InvalidOperationException ex)
					{
						// DEBUG
						Trace.TraceWarning("Failed to parse to {1} from JSON content: {2}{0}{3}",
							Environment.NewLine, typeof(T), s, ex.ToString());
					}
					finally
					{
						if (null != finallyCallback)
							finallyCallback(result);
					}
				}

				return result;
			}

			return defaultValue;
		}

	}
}
