using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace sslproxy.net
{
	class ProxyEngine
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(ProxyEngine));

		private readonly Options _options;

		private TcpListener _listener;

		private readonly List<ProxyConnection> _connections;

		public ProxyEngine(Options options)
		{
			_options = options;
			_connections = new List<ProxyConnection>();
		}

		public void Start()
		{
			Log.Info("Starting proxy engine...");

			ValidateOptions(_options);

			_listener = new TcpListener(_options.InboundEndPoint);
			try
			{
				_listener.Start();
			}
			catch (SocketException ex)
			{
				if (ex.NativeErrorCode == (int) SocketError.AccessDenied)
				{
					Log.ErrorFormat("Inbound port {0} is used by another application.", _options.InboundEndPoint.Port);
				}
				else
				{
					Log.Error("Failed to start proxy engine.", ex);
				}
				throw;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to start proxy engine.", ex);
				throw;
			}
			var task = new Task(async () =>
			{
				while (true)
				{
					TcpClient client;
					try
					{
						client = await _listener.AcceptTcpClientAsync();
					}
					catch (ObjectDisposedException)
					{
						return;
					}
					catch (Exception ex)
					{
						Log.Error("Unhandled exception.", ex);
						Stop();
						return;
					}
					HandleInboundConnection(client);
				}
			});

			task.Start();

			Log.Info("Proxy engine is running.");
		}

		public void Stop()
		{
			Log.Info("Stopping proxy engine...");
            Log.Info("Number of connections: " + _connections.Count);
			_listener.Stop();            
			foreach (var proxyConnection in _connections)
			{
				proxyConnection.Closed -= proxyConnection_Closed;
				proxyConnection.Close("Proxy engine is stopping");
			}
			Log.Info("Proxy engine is stopped.");
		}

		private void HandleInboundConnection(TcpClient client)
		{
			var proxyConnection = new ProxyConnection(client, _options.OutboundEndPoint, _options.InboundMode, _options.OutboundMode, _options.BufferSize, _options.Certificate, _options.DumpTraffic, _options.TargetHost);
			proxyConnection.Closed += proxyConnection_Closed;
			_connections.Add(proxyConnection);
            Log.InfoFormat("Number of connections: {0}", _connections.Count);
		}

		void proxyConnection_Closed(object sender, EventArgs e)
		{
			var proxyConnection = (ProxyConnection)sender;
			_connections.Remove(proxyConnection);
            Log.InfoFormat("Number of connections: {0}", _connections.Count);
		}

		private void ValidateOptions(Options options)
		{
			if (options.InboundMode == ConnectionMode.SSL)
			{
				if (String.IsNullOrEmpty(options.Certificate))
				{
					throw new Exception("Certificate name was not provided.");
				}

				try
				{
					var certificate = FindCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindBySubjectName, options.Certificate);
					{
						if (!certificate.HasPrivateKey)
						{
							throw new Exception("Specified certificate does not has private key.");
						}
					}
				}
				catch (Exception)
				{
					throw new Exception("Specified certificate is not available.");
				}
			}
		}

		public static X509Certificate2 FindCertificate(StoreLocation location, StoreName name, X509FindType findType,
			string findValue)
		{
			var store = new X509Store(name, location);
			try
			{
				store.Open(OpenFlags.ReadOnly);
				var col = store.Certificates.Find(findType, findValue, true);
				return col[0];
			}
			finally
			{
				store.Close();
			}
		}
	}
}
