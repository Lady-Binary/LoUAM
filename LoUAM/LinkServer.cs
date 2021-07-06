using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace LoUAM
{
    public class CannotStartServerException : Exception
    {
        public CannotStartServerException()
        {
        }

        public CannotStartServerException(string message) : base(message)
        {
        }

        public CannotStartServerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class LinkServer
    {
        private readonly bool https;
        private readonly int port;
        private readonly string password;
        private bool isRunning = false;
        public bool IsRunning { get => isRunning; set => isRunning = value; }

        public readonly object CurrentPlayerLock = new object();
        public Player CurrentPlayer;

        public readonly object OtherPlayersLock = new object();
        public IDictionary<ulong, Player> OtherPlayers { get; } = new Dictionary<ulong, Player>();

        public LinkServer() : this(true, 4443, "s3cr3t")
        {
        }

        public LinkServer(bool https, int port, string password)
        {
            this.https = https;
            this.port = port;
            this.password = password;
        }

        public async void StartServer()
        {
            X509Certificate2 serverCertificate = null;
            if (https)
            {
                var rsa = RSA.Create(2048);
                var req = new CertificateRequest("CN=LoUAM", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var certificate = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(2));
                serverCertificate = new X509Certificate2(certificate.Export(X509ContentType.Pfx, ":d,LpC)Uwx@QkRx9ePOBQau]OfT+Dh"), ":d,LpC)Uwx@QkRx9ePOBQau]OfT+Dh", X509KeyStorageFlags.MachineKeySet);
            }

            using (var httpServer = new HttpServer(new HttpRequestProvider()))
            {
                var listener = new TcpListener(IPAddress.Any, this.port);

                if (https)
                {
                    // Https decorator
                    httpServer.Use(new ListenerSslDecorator(new TcpListenerAdapter(listener), serverCertificate));
                } else
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
                            lock (OtherPlayersLock)
                            {
                                lock (CurrentPlayerLock)
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
                                            else if (OtherPlayers.ContainsKey(ObjectId))
                                            {
                                                responseCode = HttpResponseCode.Ok;
                                                responseContent = JObject.FromObject(OtherPlayers[ObjectId]);
                                            }
                                            else
                                            {
                                                responseCode = HttpResponseCode.BadRequest;
                                                responseContent = new JObject();
                                                responseContent["err"] = "ObjectId specified not found.";
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
                                            responseContent = JArray.FromObject(OtherPlayers.Values.Union(Enumerable.Repeat(CurrentPlayer, 1)));
                                        else
                                            responseContent = JArray.FromObject(OtherPlayers.Values);
                                    }
                                }
                            }

                        }
                        else if (context.Request.Method == HttpMethods.Post)
                        {
                            var json = Encoding.UTF8.GetString(context.Request.Post.Raw);
                            try
                            {
                                var player = JsonConvert.DeserializeObject<Player>(json);
                                player.LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                lock (OtherPlayersLock)
                                {
                                    OtherPlayers[player.ObjectId] = player;
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
                    throw new CannotStartServerException($"Cannot start server on port {port}.", ex);
                }

                IsRunning = true;

                long lastCleanup  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                while (IsRunning)
                {
                    // Every second
                    if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastCleanup >= 1000)
                    {
                        lastCleanup = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        // Remove outdated players, i.e. players not seen within the last 5 seconds
                        lock (OtherPlayersLock)
                        {
                            var PlayerIds = OtherPlayers.Keys.ToList();
                            foreach (var PlayerId in PlayerIds)
                            {
                                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - OtherPlayers[PlayerId].LastUpdate >= 5000)
                                {
                                    OtherPlayers.Remove(PlayerId);
                                }
                            }
                        }
                    }
                    await Task.Delay(100);
                }

                listener.Stop();
            }
        }

        public void StopServer()
        {
            IsRunning = false;
            OtherPlayers.Clear();
        }
    }
}
