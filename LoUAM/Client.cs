using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

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

    class Client
    {
        private readonly string host;
        private readonly int port;
        private readonly string password;

        private HttpClientHandler handler;
        private HttpClient client;

        public enum ClientStateEnum
        {
            Disconnected,
            Connecting,
            Connected
        }
        private ClientStateEnum clientState;
        public ClientStateEnum ClientState { get => clientState; set => clientState = value; }

        public Client(string host, int port) : this(host, port, "")
        {
        }

        public Client(string host, int port, string password)
        {
            this.host = host;
            this.port = port;
            this.password = password;
        }

        public async Task ConnectAsync()
        {
            handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    // Accept self-signed certs
                    return true;
                };

            client = new HttpClient(handler)
            {
                BaseAddress = new Uri($"https://{this.host}:{this.port}/")
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
                Disconnect();
                throw new ConnectionErrorException("Connection failed.", ex);
            }
        }

        public void Disconnect()
        {
            this.ClientState = ClientStateEnum.Disconnected;
            this.client = null;
            this.handler = null;
        }

        public async void UpdatePlayer(Player Player)
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
                    System.Diagnostics.Debug.WriteLine(players.ToString());
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
    }
}
