//
// Blogposter.cs: The code that performs the actual posting of the note
//
// Author:
//   Robin Sonefors (ozamosi@flukkost.nu)
//
// Copyright (C) 2007-2008 Robin Sonefors (http://www.flukkost.nu/blog)
// 

using System;
using System.IO;
using System.Text;
using System.Net;
using System.Collections;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using Mono.Unix;

using Tomboy;
using Blogposter;

namespace Tomboy.Blogposter {
	public class Blogposter : NoteAddin
	{
		Gtk.MenuItem item;
		
		public override void Initialize ()
		{
			item = new Gtk.MenuItem (Catalog.GetString ("Post note to your blog"));
			item.Activated += OnMenuItemActivated;
			item.Show ();
			AddPluginMenuItem (item);

			AuthenticationManager.Register (new GoogleClient ());
		}

		public override void Shutdown ()
		{
			item.Activated -= OnMenuItemActivated;
		}
		
		public override void OnNoteOpened ()
		{
		}

		protected void OnMenuItemActivated (object sender, EventArgs args)
		{
			XmlNode choosen_blog;
			
			XmlDocument doc = Utils.OpenXmlfile();
			//XmlNodeList accounts = doc.SelectNodes("preferences/account");
			
			if (doc.SelectNodes("preferences/account").Count == 0)
			{
				StatusMsg (
					"You haven't entered your user information yet. " +
					"Go to the preferences for this plugin and then try again.");
				return;
			}

			// There is at least one document.
			// If it is a service document, we need to clone our account for each collection.
			// Also save the info when we're done, so we only have to do this once.
			// This code assumes you can access this document without authentication
			
			foreach (XmlNode account in doc.SelectNodes ("preferences/account"))
			{
				XmlNode type = account.SelectSingleNode ("type/text()");
				if (type != null && type.Value == "collection") // We already know it's a collection
					continue;
				HttpWebResponse web_response = BlogposterWebRequest(account, "GET");
				if (web_response == null) // There was an error talking to the server - let's ignore this account 
				{
//					doc.DocumentElement.RemoveChild (account);
					continue;
				}
				
				XmlDocument response_document = new XmlDocument ();
				response_document.Load (web_response.GetResponseStream ());
				web_response.Close ();
				XmlNamespaceManager xnm = new XmlNamespaceManager (new NameTable ());
				xnm.AddNamespace ("app", GetAPPNamespace(account));
				xnm.AddNamespace ("atom", "http://www.w3.org/2005/Atom");
				XmlNodeList collections = response_document.SelectNodes ("/app:service/app:workspace/app:collection", xnm);
				if (collections.Count == 0) // Semi-ugly quickfix
				{
					xnm.AddNamespace ("app", GetAPPNamespace(account, true));
					collections = response_document.SelectNodes ("/app:service/app:workspace/app:collection", xnm);
				}
				if (collections.Count == 1) // There is one collection in the service document
				{
					string collection_url = collections.Item (0).SelectSingleNode ("@app:href").Value;
					account.SelectSingleNode ("url/text()").Value = collection_url;
					
					XmlElement element = doc.CreateElement ("type");
					XmlText text = doc.CreateTextNode ("collection");
					element.AppendChild (text);
					account.AppendChild (element);
				}
				else if (collections.Count > 1) // There are many collections in the service document
				{								// Add the new ones to the end of the list
					XmlNode original_node = account; 
					doc.DocumentElement.RemoveChild (account);
					for (int j = 0; j < collections.Count; j++)
					{
						XmlNode new_account = original_node.Clone ();
						XmlNode node = new_account.SelectSingleNode ("label/text()");
						XmlNode title = collections.Item (j).SelectSingleNode("@app:title");
						if (title != null) // Old draft
							node.Value = node.Value + ": " + collections.Item (j).SelectSingleNode ("@app:title", xnm).Value;
						else // New draft
							node.Value = node.Value + ": " + collections.Item (j).SelectSingleNode ("atom:title/text()", xnm).Value;
						node = new_account.SelectSingleNode ("url/text()");
						node.Value = collections.Item (j).SelectSingleNode ("@app:href").Value;
						
						XmlElement element = doc.CreateElement ("type");
						XmlText text = doc.CreateTextNode("collection");
						element.AppendChild (text);
						new_account.AppendChild (element);
						
						doc.DocumentElement.AppendChild (new_account);
					}
				}
				else // This is not a service document at all - it must be a collection
				{
					XmlElement element = doc.CreateElement("type");
					XmlText text = doc.CreateTextNode("collection");
					element.AppendChild (text);
					account.AppendChild (element);
				}
			}
			XmlNodeList accounts =  doc.SelectNodes ("preferences/account");
			if (accounts.Count > 1)
			{
				BlogListView choose_blog;
				
				show_blog_list_dialog:
				choose_blog = new BlogListView(accounts);
				if (choose_blog.Run() == (int)Gtk.ResponseType.Ok)
				{
					try
					{
						choosen_blog = choose_blog.SelectedBlog;
					}
					catch (System.ArgumentNullException)
					{
						goto show_blog_list_dialog;
					}
					finally
					{
						choose_blog.Destroy();
					}
				}
				else
				{
					choose_blog.Destroy();
					return;
				}
			}
			else
			{
				choosen_blog = accounts.Item (0);
			}

			// Do XSL transformations on the note

			StringWriter tmp_writer = new StringWriter ();
			NoteArchiver.Write (tmp_writer, Note.Data); 
			StringReader reader = new StringReader (tmp_writer.ToString ());
			tmp_writer.Close();
			StringWriter writer = new StringWriter();
			XPathDocument note = new XPathDocument (reader);
			
			XsltArgumentList xslt_args = new XsltArgumentList ();

			Assembly asm = Assembly.GetExecutingAssembly ();
			Stream resource = asm.GetManifestResourceStream ("Blogposter.xsl");
			XmlTextReader stylesheet_reader = new XmlTextReader (resource);

			XslTransform xsl = new XslTransform ();
			xsl.Load (stylesheet_reader, null, null);
			xsl.Transform (note, xslt_args, writer);
			
			// Turn the note into an atom entry, and post it.
			
			XmlDocument atom_post = new XmlDocument();
			XmlTextReader result = new XmlTextReader (new MemoryStream (Encoding.UTF8.GetBytes (writer.ToString ())));
			XmlNode content = atom_post.ReadNode (result);
			writer.Close();
			result.Close();

			stylesheet_reader.Close ();

			atom_post.LoadXml ("<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
							  "<entry xmlns=\"http://www.w3.org/2005/Atom\"></entry>");
			XmlElement entry = atom_post.DocumentElement;
			
			XmlElement title_element = atom_post.CreateElement ("title", "http://www.w3.org/2005/Atom");
			title_element.SetAttribute ("type", "text");
			XmlText title_text = atom_post.CreateTextNode ((string) Note.Title);
			title_element.AppendChild (title_text);
			entry.AppendChild (title_element);
			
			XmlElement content_element = atom_post.CreateElement ("content", "http://www.w3.org/2005/Atom");
			content_element.SetAttribute ("type", "xhtml");
			content_element.AppendChild (content);
			entry.AppendChild (content_element);
			
			string atomns = GetAPPNamespace (choosen_blog);
			
			XmlElement control = atom_post.CreateElement ("control", atomns);
			XmlElement draft_element = atom_post.CreateElement ("draft", atomns);
			XmlNode draft_text = doc.SelectSingleNode ("preferences/settings/draft/text()");

			XmlText draft_contents;
			if (draft_text != null)
				draft_contents = atom_post.CreateTextNode (draft_text.Value);
			else
				draft_contents = atom_post.CreateTextNode ("yes");

			draft_element.AppendChild (draft_contents);
			control.AppendChild (draft_element);
			entry.AppendChild (control);

/*			foreach (Tag tag in Note.Tags)
			{
				element = atompost.CreateElement ("category", "http://www.w3.org/2005/Atom");
				element.SetAttribute ("term", tag.Name);
				entry.AppendChild (element);
			}
*/				
			byte[] text_entry = Encoding.UTF8.GetBytes (atom_post.InnerXml);
			BlogposterWebRequest(choosen_blog, "POST", text_entry);
			
			// If we found more information, save it.
			Utils.SaveXmlfile(doc); 

		}

		protected string GetAPPNamespace(XmlNode account, bool force_old)
		{
			string atomns;
			if (force_old || (account.SelectSingleNode ("url/text()") as XmlText).Value.ToLower ().IndexOf ("blogger") !=  -1) //Blogger use the old
			{
				atomns = "http://purl.org/atom/app#";
			}
			else //Let's assume the new namespace works (Wordpress 2.3)
			{
				atomns = "http://www.w3.org/2007/app";
			}
			return atomns;
		}
		protected string GetAPPNamespace(XmlNode account)
		{
			return GetAPPNamespace(account, false);
		}

		protected void StatusMsg (string Message)
		{
			HIGMessageDialog hmd = new HIGMessageDialog(Window, 
					Gtk.DialogFlags.DestroyWithParent,
					Gtk.MessageType.Error,
					Gtk.ButtonsType.Ok,
					Catalog.GetString ("Error"),
					Catalog.GetString (Message));
			hmd.Run();
			hmd.Destroy();
		}
		
		protected HttpWebResponse BlogposterWebRequest (XmlNode account, string method, byte[] data)
		{
			ServicePointManager.CertificatePolicy = new UnsecureCertificatePolicy();
			
			string password = Utils.DecodePass (account);
			string url = Utils.SelectSingleNodeText(account, "url");
			string username = Utils.SelectSingleNodeText(account, "username");

			if (password == "")
			{
				string label = Utils.SelectSingleNodeText (account, "label");
				AskForLoginData login_data = new AskForLoginData(label, username, password); 
				if (login_data.Run() == (int)Gtk.ResponseType.Ok)
				{
					username = login_data.Username;
					password = login_data.Password;
					if (login_data.Save)
					{
						Utils.SetOrUpdateNodeText(account, "username", username);
						Utils.SetOrUpdateNodeText(account, "password", Utils.EncodePass(password));
					}
					login_data.Destroy();
				}
				else
				{
					login_data.Destroy();
					return null;
				}
			}
			
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create (url);
			
			request.Credentials = new NetworkCredential (username, password);
			request.Method = method;
			ServicePointManager.Expect100Continue = false;
			
			if (data != null)
			{
				request.ContentType = "application/atom+xml";
				request.ContentLength = data.Length;
				Stream requestStream = request.GetRequestStream ();
				requestStream.Write (data, 0, data.Length);
				requestStream.Close ();
			}
			
			try
			{
				return (HttpWebResponse) request.GetResponse ();
			}
			catch (WebException exception)
			{
				HttpWebResponse response = (HttpWebResponse) exception.Response;
				if ((int) response.StatusCode == 401)
				{
					response.Close ();
					XmlNode password_node = account.SelectSingleNode ("password/text()");
					if (password_node != null)
						password_node.Value = "";
					return BlogposterWebRequest(account, method, data);
				}
				else
				{
					StatusMsg ("The server returned error " + response.StatusCode.ToString () + ": " + response.StatusDescription);
					response.Close ();
					return null;	
				}
			}
		}
		
		protected HttpWebResponse BlogposterWebRequest (XmlNode account, string method)
		{
			return BlogposterWebRequest(account, method, null);
		}
	}


	// If you have multiple blogs, the following class construct a dialog
	// that allows you to choose which one you want to post to

	public class BlogListView : Gtk.Dialog {
		public XmlNode SelectedBlog
		{
			get
			{
				return (XmlNode)name_to_node [item_list.ActiveText];
			}
		}
		Gtk.ComboBox item_list;
		Hashtable name_to_node;
		
		public BlogListView (XmlNodeList elements) : base ()
		{
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = "";
			VBox.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;
			
			Gtk.Button button = new Gtk.Button (Gtk.Stock.Cancel);
			AddActionWidget (button, Gtk.ResponseType.Cancel);
			button = new Gtk.Button (Gtk.Stock.Ok);
			AddActionWidget (button, Gtk.ResponseType.Ok);

			Gtk.HBox hbox = new Gtk.HBox (false, 12);
			hbox.BorderWidth = 5;
			VBox.PackStart(hbox, false, false, 0);

			Gtk.Image image = new Gtk.Image(Gtk.Stock.DialogQuestion, Gtk.IconSize.Dialog);
			image.Yalign = 0;
			hbox.PackStart (image, false, false, 0);
			
			Gtk.VBox content_vbox = new Gtk.VBox (false, 12);
			hbox.PackStart (content_vbox, true, true, 0);
			
			string title = String.Format ("<span weight='bold' size='larger'>{0}</span>\n",
										  Catalog.GetString("Multiple blogs"));
			Gtk.Label l = new Gtk.Label(title);
			l.UseMarkup = true;
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			content_vbox.PackStart(l, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("I found serveral blogs. Please chose which one you would like to send your post to."));
			l.UseMarkup = true;
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			content_vbox.PackStart(l, false, false, 0);

			item_list = Gtk.ComboBox.NewText();
			name_to_node = new Hashtable();

			foreach (XmlNode node in elements)
			{
				string text = node.SelectSingleNode ("label/text()").Value;
				item_list.AppendText (text);
				name_to_node[text] = node;
			}

			content_vbox.PackStart (item_list, false, false, 0);

			ShowAll ();
		}
	}
	
	public class AskForLoginData : Gtk.Dialog
	{
		public string Username { get { return _username.Text; } }
		public string Password { get { return _password.Text; } }
		public bool Save { get { return _save.Active; } }
		
		Gtk.Entry _username;
		Gtk.Entry _password;
		Gtk.CheckButton _save;
		
		public AskForLoginData(string label, string username, string password)
		{
			
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = "";
			VBox.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;
			
			Gtk.Button button = new Gtk.Button (Gtk.Stock.Cancel);
			AddActionWidget (button, Gtk.ResponseType.Cancel);
			button = new Gtk.Button (Gtk.Stock.Ok);
			AddActionWidget (button, Gtk.ResponseType.Ok);

			Gtk.HBox hbox = new Gtk.HBox (false, 12);
			hbox.BorderWidth = 5;
			VBox.PackStart(hbox, false, false, 0);

			Gtk.Image image = new Gtk.Image(Gtk.Stock.DialogQuestion, Gtk.IconSize.Dialog);
			image.Yalign = 0;
			hbox.PackStart (image, false, false, 0);
			
			Gtk.VBox content_vbox = new Gtk.VBox (false, 12);
			hbox.PackStart (content_vbox, true, true, 0);
			
			string title = String.Format ("<span weight='bold' size='larger'>{0} {1}</span>\n",
										  Catalog.GetString("Login to"), label);
			Gtk.Label l = new Gtk.Label(title);
			l.UseMarkup = true;
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			content_vbox.PackStart(l, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("Username:"));
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			content_vbox.PackStart(l, false, false, 0);
			
			_username = new Gtk.Entry();
			_username.Text = username;
			content_vbox.PackStart(_username, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("Password:"));
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			content_vbox.PackStart(l, false, false, 0);
			
			_password = new Gtk.Entry();
			_password.Visibility = false;
			_password.Text = password;
			content_vbox.PackStart(_password, false, false, 0);
			
			_save = new Gtk.CheckButton ("Save info");
			_save.Active = false;
			content_vbox.PackStart (_save, false, false, 0);
			
			ShowAll ();
		}
	}
}
