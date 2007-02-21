using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Reporting
{
	public class EmailMessage
	{
		private string _body= "";
		protected string _address = "";
		protected string _subject = "";

		public void Send()
		{
			string body = _body.Replace(System.Environment.NewLine, "%0A").Replace("\"", "%22").Replace("&", "%26");

			System.Diagnostics.Process p = new Process();
			p.StartInfo.FileName =String.Format("mailto:{0}?subject={1}&body={2}", _address, _subject, body);
			p.Start();
		}

		/// <summary>
		///
		/// </summary>
		public string Address
		{
			set
			{
				_address = value;
			}
			get
			{
				return _address;
			}
		}
		/// <summary>
		/// the  e-mail subject
		/// </summary>
		public string Subject
		{
			set
			{
				_subject = value;
			}
			get
			{
				return _subject;
			}
		}
		/// <summary>
		///
		/// </summary>
		public string Body
		{
			set
			{
			   _body = value;
			}
			get
			{
				return _body;
			}
		}

	}
}