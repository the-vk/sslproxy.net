using System;
using System.ComponentModel;
using System.Configuration.Install;

namespace sslproxy.net
{
	[RunInstaller(true)]
	public partial class ProjectInstaller : Installer
	{
		public ProjectInstaller()
		{
			InitializeComponent();
		}

		private void serviceProcessInstaller1_BeforeInstall(object sender, InstallEventArgs e)
		{
			var commandLine = Context.Parameters["CommandLine"];
			if (!String.IsNullOrEmpty(commandLine))
			{
				Context.Parameters["assemblyPath"] = AppendPathParameter(Context.Parameters["assemblyPath"], commandLine);
			}
		}

		private string AppendPathParameter(string path, string parameter)
		{
			if (path.Length > 0 && path[0] != '"')
			{
				path = "\"" + path + "\"";
			}
			path += " " + parameter;
			return path;
		}
	}
}
