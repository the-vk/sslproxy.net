using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace sslproxy.net
{
	enum ProxyConnectionState
	{
		Closed,
		PendingOpen,
		Open,
		PendingClose,
		Failed
	}
	class ProxyConnection
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(ProxyConnection));

		private readonly TcpClient _inboundClient;
		private readonly TcpClient _outboundClient;

		private readonly byte[] _inboundBuffer;
		private readonly byte[] _outboundBuffer;

		private readonly ConnectionMode _inboundMode;
		private readonly ConnectionMode _outboundMode;

		private readonly EndPoint _inboundEndPoint;
		private readonly EndPoint _outboundEndPoint;

		private readonly string _certificate;

		private readonly bool _dumpTraffic;

		private readonly string _targetHost;

		private readonly AutoResetEvent _closeEvent;

		public ProxyConnectionState InboundConnectionState { get; private set; }
		public ProxyConnectionState OutboundConnectionState { get; private set; }

		public event EventHandler Closed;

		protected virtual void OnClosed(Object sender, EventArgs e)
		{
			var handler = Closed;
			if (handler != null) handler(sender, e);
		}

		public ProxyConnection(TcpClient inboundClient, IPEndPoint outboundEndPoint, ConnectionMode inboundMode, ConnectionMode outboundMode, uint bufferSize, string certificateName, bool dumpTraffic, string targetHost)
		{
			_closeEvent = new AutoResetEvent(true);

			_certificate = certificateName;

			_inboundMode = inboundMode;
			_outboundMode = outboundMode;

			_inboundBuffer = new byte[bufferSize];
			_outboundBuffer = new byte[bufferSize];

			_dumpTraffic = dumpTraffic;

			_targetHost = targetHost;

			_inboundClient = inboundClient;
			_inboundEndPoint = _inboundClient.Client.RemoteEndPoint;
			InboundConnectionState = ProxyConnectionState.Open;

			Log.InfoFormat("Accepted {0} connection from {1}.", _inboundMode, inboundClient.Client.RemoteEndPoint);

			OutboundConnectionState = ProxyConnectionState.Closed;

			_outboundEndPoint = outboundEndPoint;

			_outboundClient = new TcpClient();
			Run(outboundEndPoint);
		}

		public void Close()
		{
			if (InboundConnectionState == ProxyConnectionState.Closed && OutboundConnectionState == ProxyConnectionState.Closed)
				return;

			Log.Info("Closing proxy connection...");

			_closeEvent.WaitOne();

			if (InboundConnectionState != ProxyConnectionState.Closed)
			{
				InboundConnectionState = ProxyConnectionState.PendingClose;
				_inboundClient.Close();
				InboundConnectionState = ProxyConnectionState.Closed;
				Log.InfoFormat("{0} connection from {1} is closed.", _inboundMode, _inboundEndPoint);
			}

			if (OutboundConnectionState != ProxyConnectionState.Closed)
			{
				OutboundConnectionState = ProxyConnectionState.PendingClose;
				_outboundClient.Close();
				OutboundConnectionState = ProxyConnectionState.Closed;
				Log.InfoFormat("{0} connection to {1} is closed.", _outboundMode, _outboundEndPoint);

				OnClosed(this, new EventArgs());
			}
		}

		private void Run(IPEndPoint outboundEndPoint)
		{
			var runTask = new Task(async () =>
			{
				using (LogicalThreadContext.Stacks["connectionID"].Push(Guid.NewGuid().ToString()))
				{
					OutboundConnectionState = ProxyConnectionState.PendingOpen;
					try
					{
						await _outboundClient.ConnectAsync(outboundEndPoint.Address, outboundEndPoint.Port);
					}
					catch (Exception ex)
					{
						Log.Error(String.Format("Failed to connect to {0}.", outboundEndPoint), ex);
						Close();
						return;
					}
					OutboundConnectionState = ProxyConnectionState.Open;

					Log.InfoFormat("Open {0} connection to {1}.", _outboundMode, outboundEndPoint);

					Stream sourceStream;
					try
					{
						sourceStream = await GetStream(_inboundClient.GetStream(), _inboundMode, true, _targetHost);
						Log.DebugFormat("Inbound connection from {0} to {1} established.", _inboundEndPoint, _outboundEndPoint);
					}
					catch (IOException ex)
					{
						Log.Error(
							String.Format("Failed to establish inbound connection from {0} to {1} due to commnunication error.",
								_inboundEndPoint, _outboundEndPoint), ex);
						Close();
						return;
					}
					catch (ObjectDisposedException ex)
					{
						Close();
						return;
					}
					catch (Exception ex)
					{
						Log.Error(
							String.Format("Failed to establish inbound connection from {0} to {1} due to unhandled exception.",
								_inboundEndPoint, _outboundEndPoint), ex);
						Close();
						return;
					}

					Stream destinationStream;
					try
					{
						destinationStream = await GetStream(_outboundClient.GetStream(), _outboundMode, false, _targetHost);
						Log.DebugFormat("Outbound connection from {0} to {1} established.", _inboundEndPoint, _outboundEndPoint);
					}
					catch (IOException ex)
					{
						Log.Error(
							String.Format("Failed to establish outbound connection from {0} to {1}.", _inboundEndPoint, _outboundEndPoint),
							ex);
						Close();
						return;
					}
					catch (Exception ex)
					{
						Log.Error("Unhandled exception.", ex);
						Close();
						return;
					}

					var inboundRun = RunConnection(sourceStream, destinationStream, _inboundBuffer, _inboundEndPoint, _outboundEndPoint,
						_inboundMode, _outboundMode);
					var outboundRun = RunConnection(destinationStream, sourceStream, _outboundBuffer, _outboundEndPoint,
						_inboundEndPoint, _outboundMode, _inboundMode);

					Task.WaitAll(inboundRun, outboundRun);
				}
			});
			runTask.Start();
		}

		private async Task RunConnection(Stream sourceStream, Stream destinationStream, byte[] buffer, EndPoint inEndpoint, EndPoint outEndpoint, ConnectionMode inboundMode, ConnectionMode outboundMode)
		{
			Log.DebugFormat("Proxying traffic from {0} to {1}.", inEndpoint, outEndpoint);

			FileStream dump = null;

			if (_dumpTraffic)
			{
				if (!Directory.Exists("Dumps")) Directory.CreateDirectory("Dumps");
				var fileName = String.Format("Dumps\\{0} - {1} - {2}.log", DateTime.Now.ToString("yyyyMMdd-HHmmssffffff"), inEndpoint, outEndpoint).Replace(':', '_');
				dump = new FileStream(fileName, FileMode.CreateNew);
			}

			while (true)
			{
				try
				{
					Log.DebugFormat("Reading from {0}.", inEndpoint);
					var read = await sourceStream.ReadAsync(buffer, 0, buffer.Length);
					if (read == 0)
					{
						Close();
						return;
					}
					_closeEvent.Reset();
					Log.DebugFormat("Received {0} bytes from {1} connection from {2}.", read, inboundMode, inEndpoint);

					Log.DebugFormat("Writing to {0}.", outEndpoint);
					await destinationStream.WriteAsync(buffer, 0, read);
					Log.DebugFormat("Sent {0} bytes to {1} connection to {2}.", read, outboundMode, outEndpoint);

					_closeEvent.Set();

					if (dump == null) continue;
					await dump.WriteAsync(buffer, 0, read);
					await dump.FlushAsync();
				}
				catch (IOException ex)
				{
					_closeEvent.Set();
					if (!(ex.InnerException is ObjectDisposedException))
					{
						Log.Error("IOException.", ex);
					}
					Close();
					return;
				}
				catch (ObjectDisposedException ex)
				{
					_closeEvent.Set();
					Close();
					return;
				}
				catch (Exception ex)
				{
					_closeEvent.Set();
					Log.Error("Unhandled exception.", ex);
					Close();
					return;
				}
			}
		}

		private async Task<Stream> GetStream(Stream sourceStream, ConnectionMode connectionMode, bool isServer, string targetHost)
		{
			switch (connectionMode)
			{
				case ConnectionMode.TCP:
					return sourceStream;
				case ConnectionMode.SSL:
					var sslStream = new SslStream(sourceStream);
					if (isServer)
					{
						var cert = ProxyEngine.FindCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindBySubjectName, _certificate);
						Log.DebugFormat("Authenticating as server using certificate: {0}, Thumbprint={1}.", cert.SubjectName.Name, cert.Thumbprint);
						await sslStream.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls, false);
						Log.Debug("Completed authentication as server.");
					}
					else
					{
						await sslStream.AuthenticateAsClientAsync(targetHost);
					}
					return sslStream;
			}
			return null;
		}
	}
}
