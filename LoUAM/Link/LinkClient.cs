﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LoUAM
{
    public class ConnectionErrorException : Exception
    {
        public ConnectionErrorException()
        {
        }

        public ConnectionErrorException(string message) : base(message)
        {
        }

        public ConnectionErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class ClientNotConnectedException : Exception
    {
        public ClientNotConnectedException()
        {
        }

        public ClientNotConnectedException(string message) : base(message)
        {
        }

        public ClientNotConnectedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class CommunicationErrorException : Exception
    {
        public CommunicationErrorException()
        {
        }

        public CommunicationErrorException(string message) : base(message)
        {
        }

        public CommunicationErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class TooManyAttemptsException : Exception
    {
        public TooManyAttemptsException()
        {
        }

        public TooManyAttemptsException(string message) : base(message)
        {
        }

        public TooManyAttemptsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class LinkClient
    {
        private readonly bool https;
        private readonly string host;
        private readonly int port;
        private readonly string password;

        private HttpClientHandler handler;
        private HttpClient client;

        private DispatcherTimer updateTimer;

        public enum ClientStateEnum
        {
            Disconnected,
            Connecting,
            Connected,
            ConnectionFailed
        }
        public ClientStateEnum ClientState { get; set; }

        private const int MAX_CONNECTION_ATTEMPTS = 3;
        public int ConnectionAttempts { get; private set; } = 0;
        public string ConnectionError { get; set; } = "";

        public readonly object CurrentPlayerLock = new object();
        public Player CurrentPlayer { get; set; }

        public readonly object OtherPlayersLock = new object();
        public IEnumerable<Player> OtherPlayers { get; set; } = new List<Player>();

        public LinkClient(bool https, string host, int port, string password)
        {
            this.https = https;
            this.host = host;
            this.port = port;
            this.password = password;

            updateTimer = new DispatcherTimer();
            updateTimer.Tick += TimerUpdate_TickAsync;
            updateTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            updateTimer.Start();
        }

        public void ResetConnectionAttempts()
        {
            ConnectionAttempts = 0;
        }

        public async Task ConnectAsync()
        {
            if (ConnectionAttempts >= MAX_CONNECTION_ATTEMPTS)
            {
                throw new TooManyAttemptsException("Too many attempts.");
            }
            this.ClientState = ClientStateEnum.Connecting;
            ConnectionAttempts++;
            handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    // Accept self-signed certs
                    return true;
                };

            string URI = $"{this.host}:{this.port}/".ToLower();
            if (https)
            {
                if (URI.StartsWith("https://"))
                {
                    ;
                }
                else if (URI.StartsWith("http://"))
                {
                    URI = URI.Replace("http://", "https://");
                }
                else
                {
                    URI = "https://" + URI;
                }
            } else
            {
                if (URI.StartsWith("http://"))
                {
                    ;
                }
                else if (URI.StartsWith("https://"))
                {
                    URI = URI.Replace("https://", "http://");
                }
                else
                {
                    URI = "http://" + URI;
                }
            }

            client = new HttpClient(handler)
            {
                BaseAddress = new Uri(URI)
            };

            var username = "LoUAM";
            var password = this.password;
            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(authToken));

            try
            {
                ClientState = ClientStateEnum.Connecting;
                HttpResponseMessage response = await this.client.GetAsync("/");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new ConnectionErrorException("Unauthorized: wrong password?");
                }

                response.EnsureSuccessStatusCode();
                ClientState = ClientStateEnum.Connected;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    ConnectionError = ex.InnerException.Message;
                else
                    ConnectionError = ex.Message;   
                ClientState = ClientStateEnum.ConnectionFailed;
            }
        }

        public void Disconnect()
        {
            this.updateTimer.IsEnabled = false;
            this.ClientState = ClientStateEnum.Disconnected;
            this.client = null;
            this.handler = null;
        }

        public async Task UpdatePlayer(Player Player)
        {
            if (this.client == null || this.handler == null || this.ClientState == ClientStateEnum.Disconnected)
            {
                throw new ClientNotConnectedException("UpdatePlayer() invoked, but client is not connected?");
            }
            if (this.ClientState == ClientStateEnum.Connecting)
            {
                throw new ClientNotConnectedException("UpdatePlayer() invoked, but client is still connecting?");
            }
            try
            {
                var MyPlayerDataSerialized = JsonConvert.SerializeObject(Player);
                var MyPlayerDataSerializedBytes = System.Text.Encoding.UTF8.GetBytes(MyPlayerDataSerialized);
                var MyPlayerDataContent = new ByteArrayContent(MyPlayerDataSerializedBytes);
                MyPlayerDataContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await client.PostAsync("/players", MyPlayerDataContent);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException();
                }
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new CommunicationErrorException("Cannot submit player info.", ex);
            }
        }

        public async Task<IEnumerable<Player>> RetrievePlayers()
        {
            if (this.client == null || this.handler == null || this.ClientState == ClientStateEnum.Disconnected)
            {
                throw new ClientNotConnectedException("RetrievePlayers() invoked, but client is not connected?");
            }
            if (this.ClientState == ClientStateEnum.Connecting)
            {
                throw new ClientNotConnectedException("RetrievePlayers() invoked, but client is still connecting?");
            }
            try
            {
                HttpResponseMessage response = await this.client.GetAsync("/players");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException();
                }
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(json))
                {
                    IEnumerable<Player> players;
                    players = JsonConvert.DeserializeObject<IEnumerable<Player>>(json);
                    return players;
                }

                throw new CommunicationErrorException("Cannot retrieve other players info: empty result?");
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new CommunicationErrorException("Cannot retrieve other players info.", ex);
            }
        }

        public async Task UpdateServerAsync()
        {
            if (this.ClientState == ClientStateEnum.ConnectionFailed && ConnectionAttempts<MAX_CONNECTION_ATTEMPTS)
            {
                try
                {
                    this.ConnectionError = "";
                    await this.ConnectAsync();
                }
                catch (TooManyAttemptsException ex)
                {
                    if (ex.InnerException != null)
                        ConnectionError = ex.InnerException.Message;
                    else
                        ConnectionError = ex.Message;
                    this.ClientState = ClientStateEnum.ConnectionFailed;
                    return;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                        ConnectionError = ex.InnerException.Message;
                    else
                        ConnectionError = ex.Message;
                    this.ClientState = ClientStateEnum.ConnectionFailed;
                    return;
                }
            }

            if (ClientState == ClientStateEnum.Connected)
            {
                if (CurrentPlayer != null)
                {
                    try
                    {
                        await this.UpdatePlayer(CurrentPlayer);
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null)
                            ConnectionError = ex.InnerException.Message;
                        else
                            ConnectionError = ex.Message;
                        this.ClientState = ClientStateEnum.ConnectionFailed;
                        return;
                    }
                }

                try
                {
                    OtherPlayers = await this.RetrievePlayers();
                    if (CurrentPlayer != null)
                    {
                        OtherPlayers = OtherPlayers.Where(player => player != null && player.ObjectId != CurrentPlayer.ObjectId);
                    }
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                        ConnectionError = ex.InnerException.Message;
                    else
                        ConnectionError = ex.Message;
                    this.ClientState = ClientStateEnum.ConnectionFailed;
                    return;
                }
            }
        }
        private async void TimerUpdate_TickAsync(object sender, EventArgs e)
        {
            updateTimer.Stop();

            await UpdateServerAsync();

            updateTimer.Start();
        }

    }
}