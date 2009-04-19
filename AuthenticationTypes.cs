//
// AuthenticationTypes.cs: Contains authentication modules to implement custom authentication schemes. 
//
// Author:
//   Robin Sonefors (ozamosi@flukkost.nu)
//
// Copyright (C) 2007-2008 Robin Sonefors (http://www.flukkost.nu/blog)
// 
using System;
using System.Net;
using System.Web;
using System.IO;
using System.Text;

using Tomboy;

namespace Tomboy.Blogposter
{
	public class GoogleClient : IAuthenticationModule
	{
		static protected string auth_token;
		private string auth_type = "GoogleLogin";
		private string hackish_auth_type = "AuthSub"; //Kinda broken behaviour - will it help Sandy?
		
		public Authorization Authenticate(string challenge, WebRequest request, ICredentials credentials)
		{	
			int index = challenge.ToLower ().IndexOf (auth_type.ToLower ());
			if (index == -1)
			{
				index = challenge.ToLower ().IndexOf (hackish_auth_type.ToLower ());
				if (index == -1)
					return null;
			}
			if (GoogleClient.auth_token != null)
			{
				return new Authorization(auth_type + " auth=" + GoogleClient.auth_token);
			}
			
			string username = credentials.GetCredential (request.RequestUri, auth_type).UserName;
			string password = credentials.GetCredential (request.RequestUri, auth_type).Password;
			
			byte[] login_data = Encoding.ASCII.GetBytes (String.Format (
					"Email={0}&Passwd={1}&source=Tomboy-Blogposter-0.4.4&service=blogger",
					HttpUtility.UrlEncode (username), HttpUtility.UrlEncode (password)));
			HttpWebRequest login_request = (HttpWebRequest) WebRequest.Create("https://www.google.com/accounts/ClientLogin");
			ServicePointManager.Expect100Continue = false;
			login_request.Method = "POST";
			login_request.ContentType = "application/x-www-form-urlencoded";
			login_request.ContentLength = login_data.Length;
			Stream login_request_stream = login_request.GetRequestStream ();
			login_request_stream.Write (login_data, 0, login_data.Length);
			try
			{
				HttpWebResponse login_response = (HttpWebResponse) login_request.GetResponse ();
				Stream login_response_stream = login_response.GetResponseStream ();
				StreamReader login_response_stream_reader = new StreamReader (login_response_stream);
				string lines = login_response_stream_reader.ReadToEnd ();
				login_response.Close ();
				login_response_stream.Close ();
				login_response_stream_reader.Close ();
				foreach (string line in lines.Split ('\n'))
				{
					if (line.StartsWith ("Auth="))
					{
						GoogleClient.auth_token = line.Substring (5);
						break;
					}
				}
			}
			catch (WebException exception)
			{
				Logger.Log(((HttpWebResponse) exception.Response).StatusCode.ToString());
				GoogleClient.auth_token = null;
				exception.Response.Close ();
			}
			login_request_stream.Close ();
			
			if (GoogleClient.auth_token != null)
				return new Authorization(auth_type + " auth=" + GoogleClient.auth_token);
			else
				return null;
		}
		
		public string AuthenticationType { get { return auth_type; } }
		
		public bool CanPreAuthenticate { get { return false; } }
		
		public Authorization PreAuthenticate (WebRequest request, ICredentials credentials)
		{ return null; }
	}
}
