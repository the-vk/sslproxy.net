using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace test.util
{
	class Program
	{
		static Options options;       
		private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
		private static List<ThreadClient> _ThreadsClients;
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
			try
			{              
				AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
				var configPath = Path.GetFullPath(Path.Combine(AssemblyDirectory, "log4net.config"));
				XmlConfigurator.ConfigureAndWatch(new FileInfo(configPath));
				options = new Options();
				if (!CommandLine.Parser.Default.ParseArguments(args, options))
				{
					var sb = new StringBuilder();
					sb.AppendLine("Invalid command line:");
					foreach (var error in options.LastParserState.Errors)
					{
						sb.AppendFormat("\t{0}: ", error.BadOption.LongName);
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
				ServicePointManager.ServerCertificateValidationCallback +=
				(sender, cert, chain, sslPolicyErrors) => true;
				_ThreadsClients = new List<ThreadClient>();
				Log.Info("Options:" + options.ToString());
				Log.Info("Start sending requests");
				SendRequests();
				Thread.Sleep(Timeout.Infinite);
				Log.Info("Stop sending requests");

			}
			catch(Exception e)
			{
				Log.Error("Exception caught", e);
			}
		}


		public static void SendRequests()
		{
			for (uint i = 0; i < options.ClientsCount; ++i)
			{
				_ThreadsClients.Add(new ThreadClient());
			}            
		}

		

		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Log.Fatal("Unhandled exception in domain.", (Exception)e.ExceptionObject);
		}

		class ThreadClient
		{
			public ThreadClient()
			{
				this.thread = new Thread(ThreadWork);
				this.client = new HttpClient();
				client.Timeout = TimeSpan.FromSeconds(options.Timeout);
				client.BaseAddress = new Uri("https://" + options.Endpoint);
				if (options.KeepAlive)
					client.DefaultRequestHeaders.Connection.Add("Keep-Alive");
				this.thread.Start();               
			}

			Thread thread;
			HttpClient client;

			public void ThreadWork()
			{
				try
				{
					List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>();                    
					for (uint i = 0; i < options.RequestsCount; ++i)
					{                        
						Task<HttpResponseMessage> task = client.GetAsync("/Default.aspx");
						tasks.Add(task);
					}
					HttpResponseMessage response;
					foreach (Task<HttpResponseMessage> task in tasks)
					{
						response = task.Result;
						Thread.Sleep(options.Interval);
					}
					this.client.Dispose();                    
				}
				catch (Exception e)
				{
					Log.Error("Exception while sending request", e);
				}
			}
		}
	}
}
