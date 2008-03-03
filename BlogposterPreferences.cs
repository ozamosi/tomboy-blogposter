//
// BlogposterPreferences.cs: The preferences widget
//
// Author:
//   Robin Sonefors (ozamosi@flukkost.nu)
//
// Copyright (C) 2007,2008 Robin Sonefors (http://www.flukkost.nu/blog)
// 

using System;
using System.Xml;
using Mono.Unix;

using Tomboy;
using Blogposter;

namespace Tomboy.Blogposter {
	public class BlogPreferencesFactory : AddinPreferenceFactory
	{
		public override Gtk.Widget CreatePreferenceWidget ()
		{
			return new BlogPreferences ();
		}
	}
	
	class BlogPreferences : Gtk.VBox
	{
		Gtk.TreeView blog_tree;
		Gtk.ListStore blog_store;

		Gtk.Button add_button;
		Gtk.Button remove_button;
		Gtk.Button edit_button;

		XmlDocument doc;
		public BlogPreferences ()
			: base (false, 12)
		{
			this.doc = Utils.OpenXmlfile();
			Gtk.Label l = new Gtk.Label (Catalog.GetString (
				"If your blog supports the Atom Publishing Protocol " +
				"(APP), you can add it here, and you'll then be able " +
				"to post notes directly from Tomboy to your blog. " +
				"You need the URL to either your service document, " +
				"or your collection document."));
			l.Wrap = true;
			l.Xalign = 0;

			PackStart (l, false, false, 0);

			blog_store = CreateBlogStore ();

			blog_tree = new Gtk.TreeView (blog_store);
			blog_tree.HeadersVisible = true;
			blog_tree.Selection.Mode = Gtk.SelectionMode.Single;
			blog_tree.Selection.Changed += SelectionChanged;
			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn label_col = new Gtk.TreeViewColumn ();
			label_col.Title = Catalog.GetString ("Label");
			label_col.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			label_col.Resizable = true;
			label_col.Expand = true;

			renderer = new Gtk.CellRendererText ();
			label_col.PackStart (renderer, true);
			label_col.AddAttribute (renderer, "text", 0 /* label */);
			label_col.SortColumnId = 1; /* label */
			label_col.SortIndicator = false;
			label_col.Reorderable = false;
			label_col.SortOrder = Gtk.SortType.Ascending;

			blog_tree.AppendColumn (label_col);
			Gtk.TreeViewColumn url_col = new Gtk.TreeViewColumn ();
			url_col.Title = Catalog.GetString ("Location");
			url_col.Sizing = Gtk.TreeViewColumnSizing.Fixed;
			url_col.Resizable = true;

			renderer = new Gtk.CellRendererText ();
			url_col.PackStart (renderer, false);
			url_col.AddAttribute (renderer, "text", 1 /* url */);

			blog_tree.AppendColumn (url_col);

			Gtk.ScrolledWindow sw = new Gtk.ScrolledWindow ();
			sw.ShadowType = Gtk.ShadowType.In;
			sw.HeightRequest = 200;
			sw.WidthRequest = 300;
			sw.SetPolicy (Gtk.PolicyType.Automatic, Gtk.PolicyType.Automatic);
			sw.Add (blog_tree);

			PackStart (sw, true, true, 0);

			add_button = new Gtk.Button (Gtk.Stock.Add);
			add_button.Clicked += AddClicked;

			remove_button = new Gtk.Button (Gtk.Stock.Remove);
			remove_button.Sensitive = false;
			remove_button.Clicked += RemoveClicked;
			
			edit_button = new Gtk.Button (Gtk.Stock.Edit);
			edit_button.Sensitive = false;
			edit_button.Clicked += EditClicked;

			Gtk.HButtonBox hbutton_box = new Gtk.HButtonBox ();
			hbutton_box.Layout = Gtk.ButtonBoxStyle.Start;
			hbutton_box.Spacing = 6;

			hbutton_box.PackStart (add_button);
			hbutton_box.PackStart (remove_button);
			hbutton_box.PackStart (edit_button);
			PackStart (hbutton_box, false, false, 0);
			XmlText draft_state;
			draft_state = (XmlText) this.doc.SelectSingleNode ("preferences/settings/draft/text()");
			if (draft_state == null)
			{
				XmlNode node = this.doc.DocumentElement;
				Console.Write ("aa");
				if (node.SelectNodes("settings").Count == 0)
				{
					XmlElement childelement = this.doc.CreateElement ("settings");
					node.AppendChild (childelement);
				}
				node = node.SelectSingleNode ("settings");
				if (node.SelectNodes("draft").Count == 0)
				{
					XmlElement child_element = this.doc.CreateElement ("draft");
					node.AppendChild (child_element);
				}
				XmlText text = this.doc.CreateTextNode ("yes");
				node.SelectSingleNode ("draft").AppendChild (text);
				draft_state = (XmlText) this.doc.SelectSingleNode ("preferences/settings/draft/text()");
			}
			bool draftbool = (draft_state.Value == "yes" ? true : false);
			
			Gtk.CheckButton button = new Gtk.CheckButton ("Create posts as drafts");
			button.Active = draftbool;
			button.Clicked += DraftChanged;
			
			PackStart (button, false, false, 0);

			ShowAll ();
		}

		void DraftChanged (object obj, System.EventArgs args)
		{
			Gtk.CheckButton button = (Gtk.CheckButton) obj;
			XmlText draft_value = (XmlText)this.doc.SelectSingleNode("preferences/settings/draft/text()");
			draft_value.Value = (button.Active?"yes":"no");
			Utils.SaveXmlfile(this.doc);
		}	

		Gtk.ListStore CreateBlogStore ()
		{
			Gtk.ListStore store = new Gtk.ListStore (
				typeof (string), // label
				typeof (string),     // url
				typeof (string),     // username
				typeof (string));    // password
			store.SetSortColumnId (1, Gtk.SortType.Ascending);

			return store;
		}

		void UpdateBlogStore ()
		{
			blog_store.Clear ();
			foreach (XmlNode blog in this.doc.DocumentElement.SelectNodes ("account"))
			{
				string label =  Utils.SelectSingleNodeText (blog, "label");
				string url = Utils.SelectSingleNodeText (blog, "url");
				string username = Utils.SelectSingleNodeText (blog, "username");
				string password = Utils.DecodePass (blog);
				
				Gtk.TreeIter iter = blog_store.Append ();
				blog_store.SetValue (iter, 0, label);
				blog_store.SetValue (iter, 1, url);
				blog_store.SetValue (iter, 2, username);
				blog_store.SetValue (iter, 3, password);
			}
		}

		protected override void OnRealized ()
		{
			base.OnRealized ();

			UpdateBlogStore ();
		}

		void SelectionChanged (object sender, EventArgs args)
		{
			edit_button.Sensitive = remove_button.Sensitive =
				blog_tree.Selection.CountSelectedRows() > 0;
		}

		void AddClicked (object sender, EventArgs args)
		{
			AddBlog dialog = new AddBlog ();

			int response;
			string label;
			string url;
			string username;
			string password;

			response = dialog.Run ();

			if (response != (int) Gtk.ResponseType.Ok)
			{
				dialog.Destroy ();
				return;
			}

			label = dialog.Label;
			url = dialog.Url;
			username = dialog.Username;
			password = dialog.Password;

			dialog.Destroy ();

			XmlElement account = this.doc.CreateElement ("account");
			
			XmlElement element = this.doc.CreateElement ("label");
			XmlText text = this.doc.CreateTextNode (label);
			element.AppendChild (text);
			account.AppendChild (element);
			
			element = this.doc.CreateElement ("url");
			text = this.doc.CreateTextNode (url);
			element.AppendChild (text);
			account.AppendChild (element);
			
			element = this.doc.CreateElement ("username");
			text = this.doc.CreateTextNode (username);
			element.AppendChild (text);
			account.AppendChild (element);
			
			element = this.doc.CreateElement ("password");
			text = this.doc.CreateTextNode (Utils.EncodePass (password));
			element.AppendChild (text);
			account.AppendChild (element);
			
			this.doc.DocumentElement.AppendChild(account);
			
			Utils.SaveXmlfile (this.doc);
			UpdateBlogStore ();
		}

		void RemoveClicked (object sender, EventArgs args)
		{
			this.doc.DocumentElement.RemoveChild (GetSelected());
			
			Utils.SaveXmlfile (this.doc);
			UpdateBlogStore ();
		}
		
		void EditClicked (object sender, EventArgs args)
		{
			XmlNode selected = GetSelected(); 
			
			string label = Utils.SelectSingleNodeText (selected, "label");
			string url = Utils.SelectSingleNodeText (selected, "url");
			string username = Utils.SelectSingleNodeText (selected, "username");
			string password = Utils.DecodePass (selected);
			int response;
			
			EditBlog dialog = new EditBlog (label, url, username, password);
			
			response = dialog.Run ();

			if (response != (int) Gtk.ResponseType.Ok)
			{
				dialog.Destroy ();
				return;
			}

			label = dialog.Label;
			url = dialog.Url;
			username = dialog.Username;
			password = dialog.Password;

			dialog.Destroy ();

			Utils.SetOrUpdateNodeText (selected, "label", label);
			Utils.SetOrUpdateNodeText (selected, "url", url);
			Utils.SetOrUpdateNodeText (selected, "username", username);
			Utils.SetOrUpdateNodeText (selected, "password", Utils.EncodePass(password));
			
			Utils.SaveXmlfile (this.doc);
			UpdateBlogStore ();
			
		}
		
		private XmlNode GetSelected ()
		{
			Gtk.TreeIter iter;
			if (!blog_tree.Selection.GetSelected (out iter))
				return null;
			return this.doc.SelectSingleNode ("preferences/account[label=\""+(string) blog_store.GetValue (iter, 0)+"\"]");
		}
	}
	
	//Used by the preferences widget to edit/add blogs

	public class AddBlog : EditBlog
	{
		public AddBlog() : base ("", "", "", "")
		{ }
	}

	public class EditBlog : Gtk.Dialog
	{
		public string Label { get { return label.Text; } }
		public string Url { get { return url.Text; } }
		public string Username { get { return username.Text; } }
		public string Password { get { return password.Text; } }
		
		Gtk.Entry label;
		Gtk.Entry url;
		Gtk.Entry username;
		Gtk.Entry password;

		public EditBlog (string default_label, string default_url, string default_username, string default_password): base ()
		{
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = "";
			VBox.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;
			
			Gtk.Button button = new Gtk.Button (Gtk.Stock.Cancel);
			AddActionWidget (button, Gtk.ResponseType.Cancel);
			button = new Gtk.Button (Gtk.Stock.Close);
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
										  Catalog.GetString("Account information"));
			Gtk.Label l = new Gtk.Label(title);
			l.UseMarkup = true;
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			content_vbox.PackStart(l, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("Enter the information for your blog in the corresponding fields"));
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			content_vbox.PackStart(l, false, false, 0);

			hbox = new Gtk.HBox();
			content_vbox.PackStart(hbox, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("Label for this blog (only used internally)"));
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			hbox.PackStart(l, false, false, 0);
			
			this.label = new Gtk.Entry(default_label);
			hbox.PackStart(this.label, false, false, 0);
			
			hbox = new Gtk.HBox();
			content_vbox.PackStart(hbox, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("URL to Atom Publishing Service or Collection document"));
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			hbox.PackStart(l, false, false, 0);
			
			this.url = new Gtk.Entry(default_url);
			hbox.PackStart(this.url, false, false, 0);
			
			hbox = new Gtk.HBox();
			content_vbox.PackStart(hbox, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("Username"));
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			hbox.PackStart(l, false, false, 0);
			
			this.username = new Gtk.Entry(default_username);
			hbox.PackStart(this.username, false, false, 0);
			
			hbox = new Gtk.HBox();
			content_vbox.PackStart(hbox, false, false, 0);
			
			l = new Gtk.Label(Catalog.GetString("Password (optional. Will not be encrypted, just scrambled)"));
			l.Justify = Gtk.Justification.Left;
			l.LineWrap = true;
			l.SetAlignment (0.0f, 0.5f);
			hbox.PackStart(l, false, false, 0);
			
			this.password = new Gtk.Entry(default_password);
			this.password.Visibility = false;
			hbox.PackStart(this.password, false, false, 0);
			
			ShowAll ();
		}
	}
}