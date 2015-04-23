using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using log4net;
using log4net.Config;

namespace sslproxy.net
{
	class Program
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (Program));

		static public string AssemblyDirectory
		{
			get
			{
				var codeBase = Assembly.GetExecutingAssembly().CodeBase;
				var uri = new UriBuilder(codeBase);
				var path = Uri.UnescapeDataString(uri.Path);
				return Path.GetDirectoryName(path);
			}
		}

		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += ExceptionHandler;

			var configPath = Path.GetFullPath(Path.Combine(AssemblyDirectory, "log4net.config"));
			XmlConfigurator.ConfigureAndWatch(new FileInfo(configPath));

			var options = new Options();
			if (!CommandLine.Parser.Default.ParseArguments(args, options))
			{
				var sb = new StringBuilder();
				sb.AppendLine("Invalid command line:");
				foreach (var error in options.LastParserState.Errors)
				{
					sb.AppendFormat("\t{0}: ",error.BadOption.LongName);
					var errorList = new List<string>();
					if (error.ViolatesFormat) errorList.Add("invalid format");
					if (error.ViolatesMutualExclusiveness) errorList.Add("violates mutual exclusiveness");
					if (error.ViolatesRequired) errorList.Add("missing required option");
					sb.AppendLine(String.Join(", ", errorList));
				}
				Log.Error(sb);
				Console.WriteLine(options.GetUsage());
				return;
			}

			var proxyEngine = new ProxyEngine(options);

			if (options.Console)
			{
				Log.Info("Running in console mode.");
                Log.Info("Options:" + options.ToString());
				proxyEngine.Start();
				Console.WriteLine("Press enter to exit...");
				Console.ReadLine();
				proxyEngine.Stop();
			}
			else
			{
				Log.Info("Running in service mode.");
                Log.Info("Options:" + options.ToString());
				var services = new ServiceBase[] {new ProxyEngineService(proxyEngine)};
				ServiceBase.Run(services);
			}
		}

		private static void ExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			Log.Fatal("Unhandled exception in domain.", (Exception)e.ExceptionObject);
		}
	}
}
