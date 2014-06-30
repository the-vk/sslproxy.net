using System;
using System.ServiceProcess;

namespace sslproxy.net
{
	partial class ProxyEngineService : ServiceBase
	{
		private readonly ProxyEngine _proxyEngine;

		public ProxyEngineService(ProxyEngine proxyEngine)
		{
			_proxyEngine = proxyEngine;

			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			_proxyEngine.Start();
		}

		protected override void OnStop()
		{
			_proxyEngine.Stop();
		}
	}
}
