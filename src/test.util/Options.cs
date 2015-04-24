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

        [ParserState]
        public IParserState LastParserState { get; set; }

        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
