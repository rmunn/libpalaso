using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Palaso.Reporting
{
	public class ErrorReport
	{
		protected static string s_emailAddress = null;
		protected static string s_emailSubject = "Exception Report";

		/// <summary>
		/// a list of name, string value pairs that will be included in the details of the error report.
		/// </summary>
		private static StringDictionary s_properties =
			new StringDictionary();

		private static bool s_isOkToInteractWithUser = true;
		private static bool s_justRecordNonFatalMessagesForTesting=false;
		private static string s_previousNonFatalMessage;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///
		/// </summary>
		/// <param name="error"></param>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static string GetExceptionText(Exception error)
		{
			StringBuilder subject = new StringBuilder();
			subject.AppendFormat("Exception: {0}", error.Message);

			StringBuilder txt = new StringBuilder();

			txt.Append("Msg: ");
			txt.Append(error.Message);

			try
			{
				if (error is COMException)
				{
					txt.Append("\r\nCOM message: ");
					txt.Append(new Win32Exception(((COMException) error).ErrorCode).Message);
				}
			}
			catch {}

			try
			{
				txt.Append("\r\nSource: ");
				txt.Append(error.Source);
				subject.AppendFormat(" in {0}", error.Source);
			}
			catch {}

			try
			{
				if (error.TargetSite != null)
				{
					txt.Append("\r\nAssembly: ");
					txt.Append(error.TargetSite.DeclaringType.Assembly.FullName);
				}
			}
			catch {}

			try
			{
				txt.Append("\r\nStack: ");
				txt.Append(error.StackTrace);
			}
			catch {}

			s_emailSubject = subject.ToString();

			txt.Append("\r\n");
			return txt.ToString();
		}

		public static string GetVersionForErrorReporting()
		{
			Assembly assembly = Assembly.GetEntryAssembly();
			if (assembly != null)
			{
				string version = VersionNumberString;

				version += " (apparent build date: ";
				try
				{
					string path = assembly.CodeBase.Replace(@"file:///", "");
					version += File.GetLastWriteTimeUtc(path).Date.ToShortDateString() + ")";
				}
				catch
				{
					version += "???";
				}

#if DEBUG
				version += "  (Debug version)";
#endif
				return version;
			}
			return "unknown";
		}

		public static object GetAssemblyAttribute(Type attributeType)
		{
			Assembly assembly = Assembly.GetEntryAssembly();
			if (assembly != null)
			{
				object[] attributes =
					assembly.GetCustomAttributes(attributeType, false);
				if (attributes != null && attributes.Length > 0)
				{
					return attributes[0];
				}
			}
			return null;
		}

		public static string VersionNumberString
		{
			get
			{
				object attr = GetAssemblyAttribute(typeof (AssemblyFileVersionAttribute));
				if (attr != null)
				{
					return ((AssemblyFileVersionAttribute) attr).Version;
				}
				return Application.ProductVersion;
			}
		}

		public static string UserFriendlyVersionString
		{
			get
			{
				string v = VersionNumberString;
				string build = v.Substring(v.LastIndexOf('.') + 1);
				string label = "";
				object attr = GetAssemblyAttribute(typeof (AssemblyProductAttribute));
				if (attr != null)
				{
					label = ((AssemblyProductAttribute) attr).Product + ", ";
				}

				//return "Version 1 Preview, build " + build;
				return label + "build " + build;
			}
		}

//        /// ------------------------------------------------------------------------------------
//        /// <summary>
//        ///make this false during automated testing
//        /// </summary>
//        /// ------------------------------------------------------------------------------------
//        public static bool OkToInteractWithUser
//        {
//            set { s_isOkToInteractWithUser = value; }
//            get { return s_isOkToInteractWithUser; }
//        }

		/// <summary>
		/// this overrides OkToInteractWithUser
		/// The test can then retrieve from PreviousNonFatalMessage
		/// </summary>
//        public static bool JustRecordNonFatalMessagesForTesting
//        {
//            set { s_justRecordNonFatalMessagesForTesting = value; }
//            get { return s_justRecordNonFatalMessagesForTesting; }
//        }

		/// <summary>
		/// for unit test
		/// </summary>
//        public static string PreviousNonFatalMessage
//        {
//            get { return s_previousNonFatalMessage; }
//        }

		/// <summary>
		/// use this in unit tests to cleanly check that a message would have been shown.
		/// E.g.  using (new Palaso.Reporting.ErrorReport.NonFatalErrorReportExpected()) {...}
		/// </summary>
		public class NonFatalErrorReportExpected :IDisposable
		{
			private readonly bool previousJustRecordNonFatalMessagesForTesting;
			public NonFatalErrorReportExpected()
			{
				previousJustRecordNonFatalMessagesForTesting = s_justRecordNonFatalMessagesForTesting;
				s_justRecordNonFatalMessagesForTesting = true;
				s_previousNonFatalMessage = null;//this is a static, so a previous unit test could have filled it with something (yuck)
			}
			public void Dispose()
			{
				s_justRecordNonFatalMessagesForTesting= previousJustRecordNonFatalMessagesForTesting;
				if (s_previousNonFatalMessage == null)
					throw new Exception("Non Fatal Error Report was expected but wasn't generated.");
				s_previousNonFatalMessage = null;
			}
			/// <summary>
			/// use this to check the actual contents of the message that was triggered
			/// </summary>
			public string Message
			{
				get { return s_previousNonFatalMessage; }
			}
		}

		/// <summary>
		/// set this property if you want the dialog to offer to create an e-mail message.
		/// </summary>
		public static string EmailAddress
		{
			set { s_emailAddress = value; }
			get { return s_emailAddress; }
		}

		/// <summary>
		/// set this property if you want something other than the default e-mail subject
		/// </summary>
		public static string EmailSubject
		{
			set { s_emailSubject = value; }
			get { return s_emailSubject; }
		}

		/// <summary>
		/// a list of name, string value pairs that will be included in the details of the error report.
		/// </summary>
		public static StringDictionary Properties
		{
			get
			{
				return s_properties;
			}
			set
			{
				s_properties = value;
			}
		}

		public static bool IsOkToInteractWithUser
		{
			get
			{
				return s_isOkToInteractWithUser;
			}
			set
			{
				s_isOkToInteractWithUser = value;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///	add a property that he would like included in any bug reports created by this application.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void AddProperty(string label, string contents)
		{
			//avoid an error if the user changes the value of something,
			//which happens in FieldWorks, for example, when you change the language project.
			if (s_properties.ContainsKey(label))
			{
				s_properties.Remove(label);
			}

			s_properties.Add(label, contents);
		}

		public static void AddStandardProperties()
		{
			AddProperty("Version", ErrorReport.GetVersionForErrorReporting());
			AddProperty("CommandLine", Environment.CommandLine);
			AddProperty("CurrentDirectory", Environment.CurrentDirectory);
			AddProperty("MachineName", Environment.MachineName);
			AddProperty("OSVersion", Environment.OSVersion.ToString());
			AddProperty("DotNetVersion", Environment.Version.ToString());
			AddProperty("WorkingSet", Environment.WorkingSet.ToString());
			AddProperty("UserDomainName", Environment.UserDomainName);
			AddProperty("UserName", Environment.UserName);
			AddProperty("Culture", CultureInfo.CurrentCulture.ToString());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public static string GetHiearchicalExceptionInfo(Exception error, ref Exception innerMostException)
		{
			string x = ErrorReport.GetExceptionText(error);

			if (error.InnerException != null)
			{
				innerMostException = error.InnerException;

				x += "**Inner Exception:\r\n";
				x += GetHiearchicalExceptionInfo(error.InnerException, ref innerMostException);
			}
			return x;
		}

		/// <summary>
		/// Put up a message box, unless OkToInteractWithUser is false, in which case throw an Appliciation Exception
		/// </summary>
		public static void ReportNonFatalMessage(string message, params object[] args)
		{
			if(s_justRecordNonFatalMessagesForTesting)
			{
				ErrorReport.s_previousNonFatalMessage = String.Format(message, args);
			}
			else if (ErrorReport.IsOkToInteractWithUser)
			{
				NonFatalErrorDialog.Show(String.Format(message, args),
										 UsageReporter.AppNameToUseInDialogs + " Error",
										 "&OK");

			}
			else
			{
				throw new NonFatalMessageSentToUserException(String.Format(message, args));
			}
		}

		public class NonFatalMessageSentToUserException : ApplicationException
		{
			public NonFatalMessageSentToUserException(string message) : base(message) {}
		}
	}
}
