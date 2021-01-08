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

    class Server
    {
        private readonly int port;
        private readonly string password;
        private bool keepRunning = false;

        public object PlayersLock { get; set; } = new object();
        public IDictionary<ulong, Player> Players { get; } = new Dictionary<ulong, Player>();

        public Server() : this(8080)
        {
        }

        public Server(int port) : this(port, "")
        {
        }

        public Server(int port, string password)
        {
            this.port = port;
            this.password = password;
        }

        public async void StartServer()
        {
            var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=LoUAM", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var certificate = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(2));
            var serverCertificate = new X509Certificate2(certificate.Export(X509ContentType.Pfx, ":d,LpC)Uwx@QkRx9ePOBQau]OfT+Dh"), ":d,LpC)Uwx@QkRx9ePOBQau]OfT+Dh", X509KeyStorageFlags.MachineKeySet);

            using (var httpServer = new HttpServer(new HttpRequestProvider()))
            {
                var listener = new TcpListener(IPAddress.Loopback, this.port);

                // Https decorator
                httpServer.Use(new ListenerSslDecorator(new TcpListenerAdapter(listener), serverCertificate));

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
                            lock (PlayersLock)
                            {
                                if (urlParts.Length >= 2)
                                {
                                    if (ulong.TryParse(urlParts[1], out ulong ObjectId))
                                    {
                                        if (Players.ContainsKey(ObjectId))
                                        {
                                            responseCode = HttpResponseCode.Ok;
                                            responseContent = JObject.FromObject(Players[ObjectId]);
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
                                    responseContent = JArray.FromObject(Players.Values);
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
                                lock (PlayersLock)
                                {
                                    Players[player.ObjectId] = player;
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

                keepRunning = true;

                long lastCleanup  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                while (keepRunning)
                {
                    // Every second
                    if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastCleanup >= 1000)
                    {
                        lastCleanup = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        // Remove outdated players, i.e. players not seen within the last 5 seconds
                        lock (PlayersLock)
                        {
                            var PlayerIds = Players.Keys.ToList();
                            foreach (var PlayerId in PlayerIds)
                            {
                                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Players[PlayerId].LastUpdate >= 5000)
                                {
                                    Players.Remove(PlayerId);
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
            keepRunning = false;
            Players.Clear();
        }
    }
}
