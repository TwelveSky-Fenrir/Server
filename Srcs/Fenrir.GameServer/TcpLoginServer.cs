using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Fenrir.Framework.Extensions;
using Fenrir.GameServer.Metadata;
using Microsoft.Extensions.Logging;

namespace Fenrir.GameServer;

public class TcpLoginServer
{
    private const int Port = 11091;
    private const string IpAddress = "127.0.0.1";
    private static int _sessionIdCounter;
    private readonly ILogger<TcpLoginServer> _logger;
    private readonly UnifiedProtocolHandler _protocolHandler;
    private readonly TcpListener _server;
    private readonly SessionManager _sessionManager;

    public TcpLoginServer(ILogger<TcpLoginServer> logger)
    {
        _server = new TcpListener(IPAddress.Parse(IpAddress), Port);
        _logger = logger;
        _sessionManager = new SessionManager(logger);
        _protocolHandler = new UnifiedProtocolHandler(_sessionManager, logger);
    }

    public async Task StartAsync()
    {
        _server.Start();
        _logger.LogInformation($"Serveur démarré sur {IpAddress}:{Port}");

        while (true)
            try
            {
                var client = await _server.AcceptTcpClientAsync();
                _logger.LogInformation("Nouveau client accepté.");
                var session = _sessionManager.CreateSession(client, ++_sessionIdCounter);
                _logger.LogInformation($"Session créée : {session.SessionId}");
                _ = HandleClientAsync(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'acceptation d'un nouveau client.");
            }
    }

    private async Task HandleClientAsync(UserSession session)
    {
        _logger.LogInformation("Début de la gestion du client...");
        try
        {
            using (var networkStream = session.Client.GetStream())
            {
                networkStream.ReadTimeout = 5000;

                // Envoi du message de handshake initial
                var handshakePacket = CreateHandshakePacket(session.SessionId);
                await session.Client.Client.AcceptAsync();
                await networkStream.WriteAsync(handshakePacket, 0, handshakePacket.Length);
                _logger.LogInformation("Message de handshake envoyé.");

                var buffer = new byte[4096];
                var socket = session.Client.Client;
                var handshakeValid = false;

                // Tentative de handshake avec le client
                for (var attempt = 0; attempt < 10 && !handshakeValid; attempt++)
                {
                    _logger.LogInformation("Attente de la réponse de handshake du client...");
                    if (networkStream.DataAvailable)
                    {
                        var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        var clientResponse = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        if (clientResponse == "HELLO SERVER")
                        {
                            _logger.LogInformation("Handshake avec le client réussi.");
                            handshakeValid = true;
                        }
                        else
                        {
                            _logger.LogWarning("Handshake invalide. Fermeture de la connexion.");
                            return;
                        }
                    }
                    else
                    {
                        await Task.Delay(500);
                    }
                }

                if (!handshakeValid)
                {
                    _logger.LogWarning(
                        "Le handshake n'a pas pu être validé après plusieurs tentatives. Fermeture de la connexion.");
                    return;
                }

                // Gestion du client après validation du handshake
                while (true)
                {
                    _logger.LogInformation("Tentative de lecture du buffer...");

                    // Vérifier si le client est toujours connecté
                    if (socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0)
                    {
                        _logger.LogInformation("Le client semble avoir fermé la connexion.");
                        break;
                    }

                    try
                    {
                        if (networkStream.DataAvailable)
                        {
                            var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                            _logger.LogInformation($"Bytes lus: {bytesRead}");

                            if (bytesRead == 0)
                            {
                                _logger.LogInformation("Le client a fermé la connexion.");
                                break;
                            }

                            // Log des données brutes avant déchiffrement
                            var rawData = BitConverter.ToString(buffer, 0, bytesRead);
                            _logger.LogInformation($"Données brutes reçues : {rawData}");

                            // Appliquer le XOR pour déchiffrer les données reçues
                            _logger.LogInformation("Application du XOR pour le déchiffrement...");


                            // TODO: what about current position?
                            XorEncryption.Decrypt(buffer.AsSpan(0, bytesRead));
                            _logger.LogInformation("Déchiffrement du buffer terminé.");

                            // Log des données déchiffrées
                            // TODO: Why the heck is this using strings?
                            var decryptedData = BitConverter.ToString(buffer, 0, bytesRead);
                            _logger.LogInformation($"Données déchiffrées : {decryptedData}");

                            // Convertir le buffer en MessageMetadata pour le traitement
                            if (bytesRead >= 9) // Vérifier que la taille minimale est respectée
                            {
                                // TODO: consider if this needs to be ReadOnly?
                                // This will copy data, if we just agree not to write to it, no need for a new allocation?
                                ReadOnlyMemory<byte> readOnlyMemory = buffer.AsSpan(9, bytesRead - 9).ToArray();
                                // How about a read only span?
                                // ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(buffer, 9, bytesRead - 9);
                                var messageMetadata = new MessageMetadata(
                                    BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)),
                                    BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4, 4)),
                                    buffer[8],
                                    readOnlyMemory
                                );

                                // var messageMetadata = new MessageMetadata(
                                //     BitConverter.ToInt32(decryptedBuffer, 0),
                                //     BitConverter.ToInt32(decryptedBuffer, 4),
                                //     decryptedBuffer[8],
                                //     new ReadOnlyMemory<byte>(decryptedBuffer, 9, bytesRead - 9)
                                // );

                                await _protocolHandler.HandleClientProtocolAsync(session, messageMetadata);
                            }
                            else
                            {
                                _logger.LogWarning("Packet received too small to contain a valid header.");
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No data available at the moment, waiting...");
                            await Task.Delay(500); // TODO: Remove this.
                        }
                    }
                    catch (IOException ioEx)
                    {
                        _logger.LogWarning(ioEx, "Error");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling the client.");
        }
        finally
        {
            _sessionManager.RemoveSession(session.SessionId);
            session.Client.Close();
            // TODO: Event?
            _logger.LogInformation("Client connection closed.");
        }
    }

    private byte[] CreateHandshakePacket(int sessionId)
    {
        // TODO: Use structs although for a header only packet no actual data length we might want to keep it simpler like send id.
        var messageLength = 9; // (int + int + byte)
        var messageUserId = sessionId;
        byte protocolId = 0x01; // PacketType.HelloPacket

        var packet = new byte[messageLength];
        BitConverter.GetBytes(messageLength).CopyTo(packet, 0);
        BitConverter.GetBytes(messageUserId).CopyTo(packet, 4);
        packet[8] = protocolId;

        return packet;
    }
}