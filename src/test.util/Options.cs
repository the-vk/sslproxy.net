using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace test.util
{
	class Options
	{
		[Option("endpoint", Required = true, HelpText = "Endpoint address in IP:PORT format where util will send requests.")]
		public string Endpoint { get; set; }

		[Option("clients-count", Required = true, HelpText = "Number of active clients that will send requests in each thread.")]
		public int ClientsCount { get; set; }

		[Option("requests-count", DefaultValue = 100, HelpText = "Number of requests that will send every client.")]
		public int RequestsCount { get; set; }

		[Option("keep-alive", DefaultValue = false, HelpText = "Connection: keep-alive value of requests http header.")]
		public bool KeepAlive { get; set; }

		[Option("timeout", DefaultValue = 100, HelpText = "Http clients timeout in seconds.")]
		public int Timeout { get; set; }

		[Option("interval", DefaultValue = 1, HelpText = "Interval between sending each  request of each client in ms.")]
		public int Interval { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }

		public string GetUsage()
		{
			return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

		public override string ToString()
		{
			return string.Format("\r\n Endpoint: {0} \r\n ClientsCount: {1} , RequestsCount: {2} \r\n KeepAlive: {3} , Timeout: {4} \r\n , Interval: {4} \r\n", Endpoint, ClientsCount, RequestsCount, KeepAlive, Timeout, Interval);
		}
	}
}
