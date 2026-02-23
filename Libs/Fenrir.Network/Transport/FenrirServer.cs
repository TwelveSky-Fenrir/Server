using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Collections;
using Fenrir.Network.Dispatcher;
using Fenrir.Network.Framing;
using Fenrir.Network.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Network.Transport;

/// <summary>Represents a tcp server that can be used to listen for incoming connections.</summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TPacketType"></typeparam>
public abstract class FenrirServer
// <TPacketType, TMessage>
//    where TMessage : struct // TODO: Figure out
{
    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    //private readonly IMessageDispatcher<TPacketType, TMessage> _messageDispatcher;
    protected readonly FenrirServerOptions _options;
    protected readonly IServiceProvider _provider;
    private readonly Socket _socket;
    private readonly PeriodicTimer _timer;
    protected readonly List<IClient> _clients;
    protected readonly IPacketCollection _packetCollection;

    /// <summary>Gets the session collection of type <typeparamref name="ISession" />.</summary>
    public ISessionCollection<ISession> Sessions { get; }


    /// <summary>Initializes a new instance of the <see cref="FenrirServer{ISession,TMessage}" /> class.</summary>
    /// <param name="options">The server options.</param>
    /// <param name="messageDispatcher">The message dispatcher.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="provider">The service provider.</param>
    /// <param name="sessions">The session collection.</param>
    /// <param name="packetCollection"></param>
    protected FenrirServer(
        IOptions<FenrirServerOptions> options,
        //IMessageDispatcher<TPacketType, TMessage> messageDispatcher,
        ILoggerFactory loggerFactory,
        IServiceProvider provider,
        IPacketCollection packetCollection)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _cts = new CancellationTokenSource();
        _options = options.Value;
        //_messageDispatcher = messageDispatcher;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger("Fenrir.Transport.FenrirServer");
        _provider = provider;
        _packetCollection = packetCollection;
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.KeepAliveInterval));
        Sessions = new SessionCollection(options);
        _clients = new List<IClient>(); // TODO: Can use struct or span?
    }


    /// <summary>Starts the server asynchronously.</summary>
    public async Task StartAsync()
    {
        var endpoint = _options.GetRemoteEndPoint();

        try
        {
            _socket.Bind(endpoint);
        }
        catch (SocketException e)
        {
            _logger.LogError(e, "Failed to bind to {EndPoint}", endpoint);
            throw;
        }

        _socket.Listen(_options.MaxConnections);

        _logger.LogInformation("Listening on {EndPoint}", endpoint);

        if (_options.EnableKeepAlive)
            _ = KeepAliveAsync();

        while (!_cts.IsCancellationRequested)
        {
            var clientSocket = await _socket.AcceptAsync(_cts.Token).ConfigureAwait(false);
            // TODO: Whay is this a logger per client?
            // Why can't logger be injected into class based on its type?
            // ILogger<ExampleHandler> logger) We can get logger from using DI but, we might have issue setting context on it?

            // TODO: Factory get session builder.
            ISession session;
            var sessionLogger = _loggerFactory.CreateLogger("Session");
            String sessionId = Guid.NewGuid().ToString();
            using (var scope = sessionLogger.BeginScope(new Dictionary<string, object>
                   {
                       ["SessionId"] = sessionId,
                       ["RemoteEndPoint"] = clientSocket.RemoteEndPoint.ToString()
                   }))
            {
                // TODO: May be unsafe, please consider checking type is IPV4 EndPoint.
                var remoteEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint;
                session = CreateSession(sessionId, remoteEndPoint, sessionLogger, _options);
            }
            
            // TODO: Much fixing.
            //var messageParser = _provider.GetRequiredService<IMessageParser<TMessage>>();
            
            // TODO: Remove socket from session.
            //var session = CreateSession(clientSocket, messageParser, logger, _options);

            
            // TODO: Factory get Client Builder.
            IClient client;
            var clientLogger = _loggerFactory.CreateLogger("Client");
            using (var scope = _logger.BeginScope(new Dictionary<string, object>
                   {
                       ["SessionId"] = session.SessionId,
                       ["RemoteEndPoint"] = clientSocket.RemoteEndPoint.ToString()
                   }))
            {
                client = CreateClient(clientSocket);
            }
            // TODO: Client Factory?


            // Check if IP has hit a limit of connections.
            // if (!CanAddSession(session))
            // {
            //     await session.DisposeAsync().ConfigureAwait(false);
            //     continue;
            // }
            //
            // Sessions.AddSession(session);

            // TODO: Fix this.
            // _ = OnSessionConnectedAsync(session)
            //     .ContinueWith(_ => OnSessionConnectedAsync(session), _cts.Token)
            //     .Unwrap()
            //     .ContinueWith(_ => ReceiveAsyncForClient(client, _cts.Token), _cts.Token)
            //     .Unwrap()
            //     .ContinueWith(_ => OnSessionDisconnectedAsync(session), _cts.Token)
            //     .Unwrap()
            //     .ContinueWith(_ => session.DisposeAsync().AsTask(), _cts.Token)
            //     .Unwrap()
            //     .ContinueWith(_ => Sessions.RemoveSession(session.SessionId), _cts.Token)
            //     .ConfigureAwait(false);
        }
    }

    // private void ReceiveAsyncForClient(IClient client, CancellationToken ctsToken)
    // {
    //     
    //     // Receive data from client.
    //     // client.ReceiveAsync(ctsToken);
    //
    //     // Process message(s)? in buffer?
    //
    //     // TODO: Consider fairness, should we process all messages for 1 client before any other, or process 1 message per client at a time?
    //     // The simpliest option is to process all available, so I'll go with that for now.
    //
    //     // client.ReceiveBuffer.Span
    //     // TODO: Read?
    //     // IPacketHeader header = default;
    //     // IPacket packet = default;
    //     // // TODO: Get packet id from header
    //     // TODO: get optional packet based on id
    //     // TODO: call handler
    //     
    //     // If there are bytes left in the buffer, we need to move them to the start of the buffer.
    //     //client.ReceiveBuffer.Span.Slice(client.RecvBufferPosition).CopyTo(client.ReceiveBuffer.Span);
    //
    //     // Send message(s) to cilent?
    // }

    protected abstract IClient CreateClient(Socket socket);

    /// <summary>Initializes a new instance of the <see cref="ISession" /> class.</summary>
    /// <param name="sessionId"></param>
    /// <param name="endpoint">The remote endpoint.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The server options.</param>
    protected abstract ISession CreateSession(
        string sessionId,
        IPEndPoint endpoint,
        //IMessageParser<TMessage> messageParser,
        //IMessageDispatcher<TPacketType, ISession, TMessage> messageDispatcher,
        ILogger logger,
        FenrirServerOptions options);

    private async Task KeepAliveAsync()
    {
        // TODO: Yeet clients and session.
        // while (await _timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
        // {
        //     if (_cts.IsCancellationRequested)
        //         return;
        //
        //     await Sessions.ExecuteAsync(x =>
        //     {
        //         Sessions.RemoveSession(x.SessionId);
        //         return x.DisposeAsync();
        //     }, x => !x.IsConnected).ConfigureAwait(false);
        // }
    }

    /// <summary>Determines whether the session can be added to the session collection.</summary>
    /// <param name="session">The session to add.</param>
    /// <returns><see langword="true" /> if the session can be added; otherwise, <see langword="false" />.</returns>
    protected virtual bool CanAddSession(ISession session)
    {
        return !Sessions.IsFull && 
               (
                   _options.MaxConnectionsByIpAddress == 0 ||
                   Sessions.CountSessions(x => x.RemoteEndPoint.Address.ToString().Equals(session.RemoteEndPoint.Address.ToString(), StringComparison.InvariantCultureIgnoreCase)) < _options.MaxConnectionsByIpAddress
               );
    }

    /// <summary>Called when a session is connected.</summary>
    /// <param name="session">The connected session.</param>
    protected virtual Task OnSessionConnectedAsync(ISession session)
    {
        _logger.LogInformation("Session ({Name}) connected from {EndPoint}", session, session.RemoteEndPoint);
        return Task.CompletedTask;
    }

    /// <summary>Called when a session is disconnected.</summary>
    /// <param name="session">The session will be disconnected.</param>
    protected virtual Task OnSessionDisconnectedAsync(ISession session)
    {
        _logger.LogInformation("Session ({Name}) disconnected from {EndPoint}", session, session.RemoteEndPoint);
        return Task.CompletedTask;
    }
}