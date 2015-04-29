using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;

namespace sslproxy.net
{
	enum ConnectionMode
	{
		TCP,
		SSL
	}

	class Options
	{
		[Option("console", HelpText = "Run proxy engine in console mode.")]
		public bool Console { get; set; }

		[Option("inbound-mode", Required = true, HelpText = "Inbound mode. Valid values are TCP and SSL.")]
		public ConnectionMode InboundMode { get; set; }

		public IPEndPoint InboundEndPoint { get; set; }

		[Option("inbound-endpoint", Required = true, HelpText = "Inbound endpoint. Should be in format ip:port.")]
		public string InboundEndPointString {
			get { return InboundEndPoint.ToString(); }
			set { InboundEndPoint = ParseIPEndpoint(value); }
		}

		[Option("outbound-mode", Required = true, HelpText = "Outbound mode. Valid values are TCP and SSL.")]
		public ConnectionMode OutboundMode { get; set; }

		public IPEndPoint OutboundEndPoint { get; set; }

		[Option("outbound-endpoint", Required = true, HelpText = "Outbound endpoint. Should be in format ip:port.")]
		public string OutboundEndPointString
		{
			get { return OutboundEndPoint.ToString(); }
			set { OutboundEndPoint = ParseIPEndpoint(value); }
			
		}

		[Option("buffer-size", DefaultValue = 16384u, HelpText = "Size of network buffer.")]
		public uint BufferSize { get; set; }

		[Option("certificate", HelpText = "SSL certificate name to use.")]
		public string Certificate { get; set; }

		[Option('d', "dump-traffic", HelpText = "Dump all trafic to file.", DefaultValue = false)]
		public bool DumpTraffic { get; set; }

		[Option("outbound-target-host", HelpText = "Remote target host to check against SSL cert if outbound mode is SSL.")]
		public string TargetHost { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }

		public string GetUsage()
		{
			return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
		}

                public override string ToString()
                {
                    string result = string.Format("\r\n Console: {0} \r\n InboundMode: {1} , InboundEndPoint: {2} \r\n OutboundMode: {3} , OutboundEndPoint: {4} \r\n", Console, InboundMode, InboundEndPointString, OutboundMode, OutboundEndPointString);
                    result += string.Format(" BufferSize: {0} , Certificate: {1} \r\n DumpTraffic: {2} , TargetHost: {3}", BufferSize, Certificate, DumpTraffic, TargetHost);
                    return result;
                }

		private static IPEndPoint ParseIPEndpoint(string endPoint)
		{
			var ep = endPoint.Split(':');
			if (ep.Length < 2) throw new FormatException("Invalid endpoint format");
			IPAddress ip;
			if (ep.Length > 2)
			{
				if (!IPAddress.TryParse(string.Join(":", ep, 0, ep.Length - 1), out ip))
				{
					throw new FormatException("Invalid ip-adress");
				}
			}
			else
			{
				if (!IPAddress.TryParse(ep[0], out ip))
				{
					throw new FormatException("Invalid ip-adress");
				}
			}
			int port;
			if (!int.TryParse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
			{
				throw new FormatException("Invalid port");
			}
			return new IPEndPoint(ip, port);
		}
	}
}
