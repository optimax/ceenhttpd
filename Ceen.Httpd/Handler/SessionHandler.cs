﻿using System;
using System.Threading.Tasks;

namespace Ceen.Httpd.Handler
{
	public class SessionHandler : IHttpModule
	{
		/// <summary>
		/// The name of the storage module
		/// </summary>
		public const string STORAGE_MODULE_NAME = "session-storage";

		/// <summary>
		/// Gets or sets the name of the cookie with the token.
		/// </summary>
		public string CookieName { get; set; } = "ceen-session-token";

		/// <summary>
		/// Gets or sets the number of seconds a session is valid.
		/// </summary>
		public int ExpirationSeconds { get; set; } = 60 * 30;

		/// <summary>
		/// Gets or sets a value indicating if the session cookie gets the &quot;secure&quot; option set,
		/// meaning that it will only be sent over HTTPS
		/// </summary>
		public bool SessionCookieSecure { get; set; } = false;

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			if (context.Session != null)
				return false;
			
			var sessiontoken = context.Request.Cookies[CookieName];

			if (!string.IsNullOrWhiteSpace(sessiontoken))
			{
				// If the session exists, hook it up
				context.Session = await context.Storage.GetStorageAsync(STORAGE_MODULE_NAME, sessiontoken, ExpirationSeconds, false);
				if (context.Session != null)
					return false;
			}

			// Create new storage
			sessiontoken = Guid.NewGuid().ToString();
			context.Response.AddCookie(CookieName, sessiontoken, secure: SessionCookieSecure, httponly: true);
			context.Session = await context.Storage.GetStorageAsync(STORAGE_MODULE_NAME, sessiontoken, ExpirationSeconds, true);

			return false;
		}
	}
}