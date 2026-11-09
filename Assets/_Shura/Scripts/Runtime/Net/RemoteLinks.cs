using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shura
{
    /// <summary>A guest's line to the host's game: Steam P2P over the internet, or a WebSocket on a LAN / Radmin.</summary>
    public interface IRemoteLink
    {
        /// <summary>Open and ready to carry messages.</summary>
        bool Connected { get; }
        /// <summary>Was open (or tried to open) and is gone for good.</summary>
        bool Lost { get; }
        string Describe { get; }
        bool TryReceive(out string text);
        void Send(string text);
        void Close();
    }

    /// <summary>The Steam tunnel (see <see cref="SteamLink"/>): already low-latency, compressed and unreliable where it can be.</summary>
    public sealed class SteamRemoteLink : IRemoteLink
    {
        readonly SteamLink steam;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        bool lost;
        bool wasConnected;

        public SteamRemoteLink(SteamLink steam)
        {
            this.steam = steam;
            steam.GuestText += OnText;
            steam.GuestLost += OnLost;
        }

        void OnText(string text) => inbox.Enqueue(text);

        void OnLost() => lost = true;

        public bool Connected
        {
            get
            {
                if (steam.GuestConnected) wasConnected = true;
                return steam.GuestConnected;
            }
        }

        public bool Lost => lost || (wasConnected && !steam.GuestConnected);
        public string Describe => "Steam · " + steam.JoinedHost;
        public bool TryReceive(out string text) => inbox.TryDequeue(out text);
        public void Send(string text) => steam.SendToHost(text);

        public void Close()
        {
            steam.GuestText -= OnText;
            steam.GuestLost -= OnLost;
            steam.LeaveGuest();
        }
    }

    /// <summary>
    /// Straight to the host's game by address (same house, or a Radmin VPN): the very WebSocket
    /// the browser client uses, opened by this game instead. The host already turns Nagle off.
    /// </summary>
    public sealed class SocketRemoteLink : IRemoteLink
    {
        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly BlockingCollection<string> outbox = new BlockingCollection<string>(new ConcurrentQueue<string>());
        readonly CancellationTokenSource stop = new CancellationTokenSource();
        readonly string address;
        volatile bool open;
        volatile bool lost;

        public SocketRemoteLink(string hostAndPort)
        {
            address = hostAndPort;
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            Task.Run(Run);
        }

        public bool Connected => open;
        public bool Lost => lost;
        public string Describe => address;
        public string Error { get; private set; }

        async Task Run()
        {
            try
            {
                await socket.ConnectAsync(new Uri("ws://" + address + "/ws"), stop.Token);
                open = true;
                Task writer = Task.Run(Write);
                var buffer = new byte[64 * 1024];
                var message = new StringBuilder();
                var utf8 = new UTF8Encoding(false);
                while (!stop.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), stop.Token);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    message.Append(utf8.GetString(buffer, 0, r.Count));
                    if (!r.EndOfMessage) continue;
                    inbox.Enqueue(message.ToString());
                    message.Clear();
                }
                stop.Cancel();
                await writer.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
            open = false;
            lost = true;
        }

        void Write()
        {
            try
            {
                foreach (string text in outbox.GetConsumingEnumerable(stop.Token))
                {
                    if (socket.State != WebSocketState.Open) break;
                    byte[] data = Encoding.UTF8.GetBytes(text);
                    socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, stop.Token).Wait();
                }
            }
            catch
            {
                // Closing.
            }
        }

        public bool TryReceive(out string text) => inbox.TryDequeue(out text);

        public void Send(string text)
        {
            if (open && !outbox.IsAddingCompleted) outbox.Add(text);
        }

        public void Close()
        {
            open = false;
            lost = true;
            try
            {
                outbox.CompleteAdding();
                stop.Cancel();
                socket.Abort();
            }
            catch
            {
                // Already gone.
            }
        }
    }
}
