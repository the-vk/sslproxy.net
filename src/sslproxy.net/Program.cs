using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace sslproxy.net
{
	class Program
	{
		private static ILog Log = LogManager.GetLogger(typeof (Program));

		static void Main(string[] args)
		{
			XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));


			var options = new Options();
			if (!CommandLine.Parser.Default.ParseArguments(args, options))
			{
				Console.WriteLine(options.GetUsage());
				return;
			}

			var proxyEngine = new ProxyEngine(options);

			if (options.Console)
			{
				Log.Info("Running in console mode.");
				proxyEngine.Start();
				Console.WriteLine("Press enter to exit...");
				Console.ReadLine();
				proxyEngine.Stop();
			}
			else
			{
				Log.Info("Running in service mode.");
				var services = new ServiceBase[] {new ProxyEngineService(proxyEngine)};
				ServiceBase.Run(services);
			}
		}
	}
}
