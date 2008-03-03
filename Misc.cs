//
// Misc.cs: Various helper functions
//
// Author:
//   Robin Sonefors (ozamosi@lysator.liu.se)
//
// Copyright (C) 2007 Robin Sonefors (http://www.flukkost.nu/blog)
// 

using System;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Xml;
using System.Text;
using Mono.Unix;
using System.IO;

namespace Blogposter
{
	
		// Assume that the user isn't being spoofed, doesn't care about security, and just want to encrypt his/her password exchange
	public class UnsecureCertificatePolicy : ICertificatePolicy
	{
		public bool CheckValidationResult (ServicePoint sp, 
			X509Certificate certificate, WebRequest request, int error)
		{
			return true;
		}
	}
	
	public class Utils
	{
		// Crash when empty account list => bad. Keep the file path in lot's of different places => bad.
		static string settingsfile =  Environment.GetEnvironmentVariable ("HOME") + "/.tomboy/Blogposter/accounts.xml";
		public static XmlDocument OpenXmlfile()
		{
			XmlDocument doc = new XmlDocument ();
			try
			{
				doc.Load(settingsfile);
				if (doc.ChildNodes [1].Name == "accounts")
				{
					string contents = doc.ChildNodes [1].InnerXml;
					doc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"+
							"<preferences>" + contents + "</preferences>");
				}
			}
			catch
			{
				doc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"+
						"<preferences></preferences>");
			}
			return doc;
		}
		public static void SaveXmlfile(XmlDocument doc)
		{
			try
			{
				doc.Save(settingsfile);
			}
			catch (DirectoryNotFoundException)
			{
				Directory.CreateDirectory(Environment.GetEnvironmentVariable ("HOME") + "/.tomboy/Blogposter");
				doc.Save(settingsfile);
			}
				
		}
		
		//Encode and decode passwords in one place
		public static string DecodePass(XmlNode account)
		{
			XmlNode passwd = account.SelectSingleNode ("password/text()");
			if (passwd == null)
			{
				return "";
			}
			else
			{
				byte[] passwdbytes = Convert.FromBase64String (passwd.Value);
				return Encoding.UTF8.GetString (passwdbytes);
			}
		}

		public static string EncodePass(string cleartextpass)
		{
			return Convert.ToBase64String (Encoding.UTF8.GetBytes (cleartextpass));
		}
	}
}