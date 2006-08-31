using System;
using System.ComponentModel;
using System.Windows.Forms;
using NUnit.Framework;
using WeSay.Language;

namespace WeSay.UI.Tests
{
	[TestFixture]
	public class GhostBindingTests
	{
		private Papa _papa = new Papa();
		private TextBox _ghostFirstNameWidget;
		private TextBox _papaNameWidget;
	   private GhostBinding _binding;
		protected bool _didNotify;


		public class Child : WeSay.LexicalModel.WeSayDataObject
		{
			private MultiText first=new MultiText();
			private MultiText middle=new MultiText();

			public MultiText First
			{
				get { return first; }
				set { first = value; }
			}

			public MultiText Middle
			{
				get { return middle; }
				set { middle = value; }
			}
		}

		public class Papa : WeSay.LexicalModel.WeSayDataObject
		{
			private WeSay.Data.InMemoryBindingList<Child> _children = new WeSay.Data.InMemoryBindingList<Child>();

			public Papa()
			{
				_children = new WeSay.Data.InMemoryBindingList<Child>();

				WireUpEvents();
		   }

			public WeSay.Data.InMemoryBindingList<Child> Children
			{
				get { return _children; }
			}

			protected override void WireUpEvents()
		   {
			   base.WireUpEvents();
			   WireUpList(_children, "children");
		   }

		}

		void _binding_Triggered(object sender, object newObject, EventArgs args)
		{
			_didNotify = true;
		}

		[SetUp]
		public void Setup()
		{
			_papaNameWidget = new TextBox();
			_papaNameWidget.Text  =  "John";
			_ghostFirstNameWidget = new TextBox();
			_binding = new GhostBinding(_papa.Children, "First", "en", _ghostFirstNameWidget);
			_didNotify = false;
			//Window w = new Window("test");
			//VBox box = new VBox();
			//w.Add(box);
			//box.PackStart(_papaNameWidget);
			//box.PackStart(_ghostFirstNameWidget);
			//box.ShowAll();
			//w.ShowAll();
			_papaNameWidget.Show();
			while (Gtk.Application.EventsPending())
			{ Gtk.Application.RunIteration(); }

			//Application.Run();
			_papaNameWidget.Focus();
			 _ghostFirstNameWidget.Focus();
	   }


		[Test]
		public void EmptyListGrows()
		{
			_ghostFirstNameWidget.Text = "Samuel";
			Assert.AreEqual(0, _papa.Children.Count);
			_papaNameWidget.Focus();
			Assert.AreEqual(1, _papa.Children.Count);
		}

		[Test]
		public void NewItemGetsValue()
		{
			_ghostFirstNameWidget.Text = "Samuel";
			_papaNameWidget.Focus();
			Assert.AreEqual("Samuel", _papa.Children[0].First["en"]);
		}
		 [Test]
		public void NewItemTriggersEvent()
		{
			 _binding.Triggered += new GhostBinding.GhostTriggered(_binding_Triggered);
			_ghostFirstNameWidget.Text = "Samuel";
			_papaNameWidget.Focus();
			Assert.IsTrue(_didNotify);
		}


	}
}