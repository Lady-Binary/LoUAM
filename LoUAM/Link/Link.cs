using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Net.Sockets;

using uhttpsharp;
using uhttpsharp.Handlers;
using uhttpsharp.Listeners;
using uhttpsharp.RequestProviders;

using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.IO;
using System.Dynamic;
using uhttpsharp.Handlers.Compression;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace LoUAM
{
    public class Link
    {
        private bool https;
        private string host;
        private int port;
        private string password;

        public SemaphoreSlim CurrentPlayerSemaphoreSlim = new SemaphoreSlim(1, 1);
        public Player CurrentPlayer;

        public SemaphoreSlim OtherPlayersSemaphoreSlim = new SemaphoreSlim(1, 1);
        public List<Player> OtherPlayers { get; set; } = new List<Player>();

        private DispatcherTimer updateTimer;

        public enum StateEnum
        {
            Idle,
            ServerListening,
            ServerListenFailed,
            ClientDisconnected,
            ClientConnecting,
            ClientConnected,
            ClientConnectionFailed
        }
        public StateEnum State { get; set; }
        public string Error { get; set; } = "";

        public Link() : this(true, "localhost", 4443, "s3cr3t")
        {
        }

        public Link(bool https, int port, string password) : this(https, "localhost", port, password)
        {
        }

        public Link(bool https, string host, int port, string password)
        {
            this.https = https;
            this.port = port;
            this.password = password;

            updateTimer = new DispatcherTimer();
            updateTimer.Tick += TimerUpdate_TickAsync;
            updateTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
        }

        private async void TimerUpdate_TickAsync(object sender, EventArgs e)
        {
            updateTimer.Stop();

            if (State == StateEnum.ServerListening)
            {
                await UpdatePlayersAsync();
            }
            if (State == StateEnum.ClientConnected)
            {
                await UpdateServerAsync();
            }
            if (this.State == StateEnum.ClientConnectionFailed && ConnectionAttempts < MAX_CONNECTION_ATTEMPTS)
            {
                await this.ConnectAsync();
            }
            updateTimer.Start();
        }

        #region Server
        private HttpServer httpServer;
        private TcpListener listener;
        private readonly EventWaitHandle stopServerSignal = new AutoResetEvent(false);

        public void StartServer(bool https, int port, string password)
        {
            this.https = https;
            this.port = port;
            this.password = password;

            StartServer();
        }
        public void StartServer()
        {
            Thread serverThread = new Thread(() =>
            {
                Error = "";

                try
                {
                    X509Certificate2 serverCertificate = null;
                    if (https)
                    {
                        var rsa = RSA.Create(2048);
                        var req = new CertificateRequest("CN=LoUAM", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        var certificate = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(2));
                        serverCertificate = new X509Certificate2(certificate.Export(X509ContentType.Pfx, ":d,LpC)Uwx@QkRx9ePOBQau]OfT+Dh"), ":d,LpC)Uwx@QkRx9ePOBQau]OfT+Dh", X509KeyStorageFlags.MachineKeySet);
                    }

                    using (httpServer = new HttpServer(new HttpRequestProvider()))
                    {
                        listener = new TcpListener(IPAddress.Any, this.port);

                        if (https)
                        {
                            // Https decorator
                            httpServer.Use(new ListenerSslDecorator(new TcpListenerAdapter(listener), serverCertificate));
                        }
                        else
                        {
                            // Http only
                            httpServer.Use(new TcpListenerAdapter(listener));
                        }

                        // Exception handling (must be the first handler)
                        httpServer.Use(async (context, next) =>
                        {
                            try
                            {
                                await next().ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                context.Response = new HttpResponse(HttpResponseCode.InternalServerError, "Error while handling your request. " + e.Message, false);
                            }
                        });

                        // Enable authentication, if needed
                        if (!String.IsNullOrEmpty(password))
                        {
                            httpServer.Use(new SessionHandler<dynamic>(() => new ExpandoObject(), TimeSpan.FromMinutes(20)));
                            httpServer.Use(new BasicAuthenticationHandler("LoUAM", "LoUAM", password));
                        }

                        // Enable compression
                        httpServer.Use(new CompressionHandler(DeflateCompressor.Default, GZipCompressor.Default));

                        // Requests debugger
                        //httpServer.Use((context, next) =>
                        //{
                        //    Console.WriteLine("Got Request!");
                        //    return next();
                        //});

                        httpServer.Use(
                            new HttpRouter()
                            .With("", new AnonymousHttpRequestHandler(async (context, next) =>
                            {
                                HttpResponseCode responseCode;
                                JContainer responseContent;

                                if (context.Request.Method == HttpMethods.Get)
                                {
                                    responseCode = HttpResponseCode.Ok;
                                    responseContent = new JObject();
                                    responseContent["ver"] = "LoUAM v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
                                }
                                else
                                {
                                    responseCode = HttpResponseCode.BadRequest;
                                    responseContent = new JObject();
                                    responseContent["err"] = "Method not supported.";
                                }

                                context.Response = new HttpResponse(
                                    code: responseCode,
                                    contentType: "application/json",
                                    contentStream: new MemoryStream(Encoding.UTF8.GetBytes(responseContent.ToString())),
                                    keepAliveConnection: false
                                    );
                            }))
                            .With("players", new AnonymousHttpRequestHandler(async (context, next) =>
                            {
                                HttpResponseCode responseCode;
                                JContainer responseContent = new JObject();

                                if (context.Request.Method == HttpMethods.Get)
                                {
                                    var urlParts = context.Request.Uri.OriginalString.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                                    await OtherPlayersSemaphoreSlim.WaitAsync();
                                    await CurrentPlayerSemaphoreSlim.WaitAsync();
                                    try
                                    {
                                        if (urlParts.Length >= 2)
                                        {
                                            if (ulong.TryParse(urlParts[1], out ulong ObjectId))
                                            {
                                                if (CurrentPlayer.ObjectId == ObjectId)
                                                {
                                                    responseCode = HttpResponseCode.Ok;
                                                    responseContent = JObject.FromObject(CurrentPlayer);
                                                }
                                                else
                                                {
                                                    int i = OtherPlayers.FindIndex(p => p.ObjectId == ObjectId);
                                                    if (i > -1)
                                                    {
                                                        responseCode = HttpResponseCode.Ok;
                                                        responseContent = JObject.FromObject(OtherPlayers[i]);
                                                    }
                                                    else
                                                    {
                                                        responseCode = HttpResponseCode.BadRequest;
                                                        responseContent = new JObject();
                                                        responseContent["err"] = "ObjectId specified not found.";
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                responseCode = HttpResponseCode.BadRequest;
                                                responseContent = new JObject();
                                                responseContent["err"] = "Invalid ObjectId specified.";
                                            }
                                        }
                                        else
                                        {
                                            responseCode = HttpResponseCode.Ok;
                                            if (CurrentPlayer != null)
                                                responseContent = JArray.FromObject(OtherPlayers.Union(Enumerable.Repeat(CurrentPlayer, 1)));
                                            else
                                                responseContent = JArray.FromObject(OtherPlayers);
                                        }
                                    }
                                    finally
                                    {
                                        CurrentPlayerSemaphoreSlim.Release();
                                        OtherPlayersSemaphoreSlim.Release();
                                    }
                                }
                                else if (context.Request.Method == HttpMethods.Post)
                                {
                                    var json = Encoding.UTF8.GetString(context.Request.Post.Raw);
                                    try
                                    {
                                        var player = JsonConvert.DeserializeObject<Player>(json);
                                        player.LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                        await OtherPlayersSemaphoreSlim.WaitAsync();
                                        try
                                        {
                                            int i = OtherPlayers.FindIndex(p => p.ObjectId == player.ObjectId);
                                            if (i > -1)
                                                OtherPlayers[i] = player;
                                            else
                                                OtherPlayers.Add(player);
                                        }
                                        finally
                                        {
                                            OtherPlayersSemaphoreSlim.Release();
                                        }
                                        responseCode = HttpResponseCode.Ok;
                                        responseContent = new JObject();
                                    }
                                    catch (Exception ex)
                                    {
                                        responseCode = HttpResponseCode.BadRequest;
                                        responseContent = new JObject();
                                        responseContent["err"] = "Invalid payload.";
                                    }
                                }
                                else
                                {
                                    responseCode = HttpResponseCode.BadRequest;
                                    responseContent = new JObject();
                                    responseContent["err"] = "Method not supported.";
                                }

                                context.Response = new HttpResponse(
                                    code: responseCode,
                                    contentType: "application/json",
                                    contentStream: new MemoryStream(Encoding.UTF8.GetBytes(responseContent.ToString())),
                                    keepAliveConnection: false
                                    );
                            }))
                        );

                        httpServer.Use((context, next) =>
                        {
                            context.Response = HttpResponse.CreateWithMessage(HttpResponseCode.NotFound, "not found", false);
                            return Task.Factory.GetCompleted();
                        });

                        try
                        {
                            httpServer.Start();
                        }
                        catch (Exception ex)
                        {
                            Error = $"Cannot start server on port {port}.";
                            State = StateEnum.ServerListenFailed;
                            return;
                        }

                        State = StateEnum.ServerListening;

                        updateTimer.Start();

                        stopServerSignal.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                        Error = ex.InnerException.Message;
                    else
                        Error = ex.Message;
                    State = StateEnum.ServerListenFailed;
                }
            });
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        private async Task UpdatePlayersAsync()
        {
            if (State == StateEnum.ServerListening)
            {
                // Remove outdated players, i.e. players not seen within the last 5 seconds
                await OtherPlayersSemaphoreSlim.WaitAsync();
                try
                {
                    for (int i = OtherPlayers.Count - 1; i >= 0; i--)
                    {
                        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - OtherPlayers[i].LastUpdate >= 5000)
                        {
                            OtherPlayers.RemoveAt(i);
                        }
                    }
                }
                finally
                {
                    OtherPlayersSemaphoreSlim.Release();
                }
            }
        }

        public async Task StopServer()
        {
            stopServerSignal.Set();
            State = StateEnum.Idle;
            updateTimer.Stop();
            listener.Stop();
            this.listener = null;
            this.httpServer = null;
            Error = "";
            await OtherPlayersSemaphoreSlim.WaitAsync();
            try
            {
                OtherPlayers.Clear();
            }
            finally
            {
                OtherPlayersSemaphoreSlim.Release();
            }
        }
        #endregion Server

        #region Client
        private HttpClientHandler handler;
        private HttpClient client;

        private const int MAX_CONNECTION_ATTEMPTS = 3;
        public int ConnectionAttempts { get; private set; } = 0;

        public void ResetConnectionAttempts()
        {
            ConnectionAttempts = 0;
        }

        public async Task ConnectAsync(bool https, string host, int port, string password)
        {
            this.https = https;
            this.host = host;
            this.port = port;
            this.password = password;

            await ConnectAsync();
        }
        public async Task ConnectAsync()
        {
            updateTimer.Start();

            if (ConnectionAttempts >= MAX_CONNECTION_ATTEMPTS)
            {
                this.Error = "Too many connection attempts.";
                this.State = StateEnum.ClientConnectionFailed;
                return;
            }

            Error = "";
            State = StateEnum.ClientConnecting;
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
            }
            else
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
                BaseAddress = new Uri(URI),
                Timeout = TimeSpan.FromSeconds(5)
            };

            var username = "LoUAM";
            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(authToken));

            try
            {
                State = StateEnum.ClientConnecting;
                HttpResponseMessage response = await this.client.GetAsync("/");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Error = "Unauthorized: wrong password?";
                    State = StateEnum.ClientConnectionFailed;
                }

                response.EnsureSuccessStatusCode();
                State = StateEnum.ClientConnected;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    Error = ex.InnerException.Message;
                else
                    Error = ex.Message;
                State = StateEnum.ClientConnectionFailed;
            }
        }

        public async Task Disconnect()
        {
            this.State = StateEnum.Idle;
            this.updateTimer.Stop();
            this.client = null;
            this.handler = null;
            await OtherPlayersSemaphoreSlim.WaitAsync();
            try
            {
                this.OtherPlayers.Clear();
            }
            finally
            {
                OtherPlayersSemaphoreSlim.Release();
            }
        }

        public async Task UpdatePlayer(Player Player)
        {
            if (this.client == null || this.handler == null || this.State == StateEnum.ClientDisconnected)
            {
                Error = "UpdatePlayer() invoked, but client is not connected?";
                State = StateEnum.ClientConnectionFailed;
            }
            if (this.State == StateEnum.ClientConnecting)
            {
                Error = "UpdatePlayer() invoked, but client is still connecting?";
                State = StateEnum.ClientConnectionFailed;
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
                if (ex.InnerException != null)
                    Error = ex.InnerException.Message;
                else
                    Error = ex.Message;
                State = StateEnum.ClientConnectionFailed;
            }
        }

        public async Task<List<Player>> RetrievePlayers()
        {
            if (this.client == null || this.handler == null || this.State == StateEnum.ClientDisconnected)
            {
                Error = "RetrievePlayers() invoked, but client is not connected?";
                State = StateEnum.ClientConnectionFailed;
                return null;
            }
            if (this.State == StateEnum.ClientConnecting)
            {
                Error = "RetrievePlayers() invoked, but client is still connecting?";
                State = StateEnum.ClientConnectionFailed;
                return null;
            }
            try
            {
                HttpResponseMessage response = await this.client.GetAsync("/players");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Error = "Unauthorized: wrong password?";
                    State = StateEnum.ClientConnectionFailed;
                    return null;
                }
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(json))
                {
                    List<Player> players;
                    players = JsonConvert.DeserializeObject<List<Player>>(json);
                    return players;
                }

                Error = "Cannot retrieve other players info: empty result?";
                State = StateEnum.ClientConnectionFailed;
                return null;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    Error = ex.InnerException.Message;
                else
                    Error = ex.Message;
                State = StateEnum.ClientConnectionFailed;
                return null;
            }
        }

        public async Task UpdateServerAsync()
        {
            if (State == StateEnum.ClientConnected)
            {
                await OtherPlayersSemaphoreSlim.WaitAsync();
                await CurrentPlayerSemaphoreSlim.WaitAsync();
                try
                {
                    try
                    {
                        var RetrievedPlayers = await this.RetrievePlayers();
                        if (RetrievedPlayers != null)
                        {
                            OtherPlayers = RetrievedPlayers.ToList(); // .ToList() to force immediate execution
                        } else
                        {
                            OtherPlayers.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null)
                            Error = ex.InnerException.Message;
                        else
                            Error = ex.Message;
                        this.State = StateEnum.ClientConnectionFailed;
                        return;
                    }

                    if (CurrentPlayer != null)
                    {
                        // Make sure our player is not duplicated in the "OtherPlayers" list too
                        OtherPlayers = OtherPlayers
                            .Where(player =>
                                    player != null &&
                                    player.ObjectId != CurrentPlayer.ObjectId
                            ).ToList();  // .ToList() to force immediate execution

                        try
                        {
                            await UpdatePlayer(CurrentPlayer);
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null)
                                Error = ex.InnerException.Message;
                            else
                                Error = ex.Message;
                            this.State = StateEnum.ClientConnectionFailed;
                            return;
                        }
                    }
                }
                finally
                {
                    CurrentPlayerSemaphoreSlim.Release();
                    OtherPlayersSemaphoreSlim.Release();
                }
            }
        }
        #endregion Client
    }
}
