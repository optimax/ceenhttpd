﻿using System;
using Ceen.Database;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Class for storing a single user login entry
	/// </summary>
	public class LoginEntry
	{
		/// <summary>
		/// Gets or sets the user identifier.
		/// </summary>
		public string UserID { get; set; }
		/// <summary>
		/// Gets or sets the username.
		/// </summary>
        [Unique("id")]
		public string Username { get; set; }
        /// <summary>
        /// Gets or sets the PBKDF2 token that represents the password.
        /// </summary>
        [Unique("id")]
        public string Token { get; set; }
	}
}
