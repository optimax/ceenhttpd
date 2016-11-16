﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace Ceen.Httpd
{
	/// <summary>
	/// Implementation of a simple regexp based router
	/// </summary>
	public class Router : IRouter
	{
		/// <summary>
		/// List of rules
		/// </summary>
		public IList<KeyValuePair<Regex, IHttpModule>> Rules { get; set; }

		/// <summary>
		/// Creates a new router
		/// </summary>
		public Router()
			: this(new KeyValuePair<string, IHttpModule>[0])
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Router"/> class.
		/// </summary>
		/// <param name="items">The list of routes to use.</param>
		public Router(params KeyValuePair<string, IHttpModule>[] items)
			: this(items.AsEnumerable())
		{
		}
		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.Router"/> class.
		/// </summary>
		/// <param name="rules">The routing rules.</param>
		public Router(IEnumerable<KeyValuePair<string, IHttpModule>> rules)
		{
			Rules = rules.Select(x => new KeyValuePair<Regex, IHttpModule>(ToRegex(x.Key), x.Value)).ToList();
		}

		/// <summary>
		/// Parses a string and determines if it is a regular expression or not
		/// </summary>
		/// <returns>The parsed regular expression.</returns>
		/// <param name="value">The string to parse.</param>
		public static Regex ToRegex(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;
			
			if (value.StartsWith("[") && value.EndsWith("]"))
				return new Regex(value.Substring(1, value.Length - 2));
			else
				return new Regex(Regex.Escape(value));
		}

		/// <summary>
		/// Add the specified route and handler.
		/// </summary>
		/// <param name="route">The route to match.</param>
		/// <param name="handler">The handler to use.</param>
		public void Add(string route, IHttpModule handler)
		{
			Rules.Add(new KeyValuePair<Regex, IHttpModule>(ToRegex(route), handler));
		}

		/// <summary>
		/// Add the specified route and handler.
		/// </summary>
		/// <param name="route">The route to match.</param>
		/// <param name="handler">The handler to use.</param>
		public void Add(Regex route, IHttpModule handler)
		{
			Rules.Add(new KeyValuePair<Regex, IHttpModule>(route, handler));
		}

		/// <summary>
		/// Process the specified request.
		/// </summary>
		/// <param name="request">Request.</param>
		/// <param name="response">Response.</param>
		/// <returns><c>True</c> if the processing was handled, false otherwise</returns>
		public async Task<bool> Process(IHttpContext context)
		{
			foreach (var rule in Rules)
			{
				if (rule.Key != null)
				{
					var m = rule.Key.Match(context.Request.Path);
					if (!m.Success || m.Length != context.Request.Path.Length)
						continue;
				}

				if (await rule.Value.HandleAsync(context))
					return true;
			}

			return false;
		}
	}
}
