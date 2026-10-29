using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Connects to DomyosTS100Connector's TCP server (127.0.0.1:5005) and keeps
// the latest treadmill reading available to the rest of the game.
//
// Runs its own background thread doing a blocking connect-then-read loop,
// since a blocking socket read on the main thread would freeze every frame
// while waiting for the next packet. The thread only ever writes the three
// latest values plus a timestamp, guarded by a lock; Update() on the main
// thread reads them back out. At ~4Hz this lock is essentially uncontended,
// so a plain lock is simpler than anything lock-free and costs nothing
// noticeable.
//
// If the connector app isn't running yet, or the connection drops (e.g. the
// connector was restarted), this reconnects automatically every second
// rather than giving up -- so you can start the two programs in either
// order and this just catches up whenever the server appears.
public class TreadmillClient : MonoBehaviour
{
    public static TreadmillClient Instance { get; private set; }

    [Header("Server (DomyosTS100Connector)")]
    public string host = "127.0.0.1";
    public int port = 5005;

    [Header("Staleness")]
    [Tooltip("If no data arrives for longer than this, IsDataFresh reports false so callers can fall back to another input source.")]
    public float staleAfterSeconds = 1.5f;

    // UnityEngine.Time is main-thread-only; ParseLine runs on the background
    // socket thread, so staleness is timed with a plain Stopwatch instead.
    private static readonly Stopwatch clock = Stopwatch.StartNew();

    private readonly object dataLock = new object();
    private float speedKmh;
    private float distanceKm;
    private float inclineDeg;
    private double lastReceiveSeconds = -1;
    private bool isConnected;

    private Thread workerThread;
    private volatile bool shouldStop;

    public float SpeedKmh { get { lock (dataLock) return speedKmh; } }
    public float SpeedMetersPerSecond { get { lock (dataLock) return speedKmh / 3.6f; } }
    public float DistanceKm { get { lock (dataLock) return distanceKm; } }
    public float InclineDeg { get { lock (dataLock) return inclineDeg; } }
    public bool IsConnected { get { lock (dataLock) return isConnected; } }

    public bool IsDataFresh
    {
        get
        {
            lock (dataLock)
            {
                if (!isConnected || lastReceiveSeconds < 0) return false;
                return (clock.Elapsed.TotalSeconds - lastReceiveSeconds) <= staleAfterSeconds;
            }
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        shouldStop = false;
        workerThread = new Thread(WorkerLoop) { IsBackground = true, Name = "TreadmillClient" };
        workerThread.Start();
    }

    void OnDisable()
    {
        shouldStop = true;
        workerThread?.Join(500);
    }

    private void WorkerLoop()
    {
        while (!shouldStop)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(host, port);

                    lock (dataLock) { isConnected = true; }
                    Debug.Log($"[TreadmillClient] Connected to {host}:{port}");

                    using (NetworkStream stream = client.GetStream())
                    {
                        ReadLinesUntilDisconnected(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[TreadmillClient] Not connected ({ex.GetType().Name}: {ex.Message}). Retrying...");
            }

            lock (dataLock) { isConnected = false; }

            // Wait a second before retrying, but check shouldStop frequently
            // so OnDisable's Join(500) doesn't have to wait a full second.
            for (int i = 0; i < 10 && !shouldStop; i++)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void ReadLinesUntilDisconnected(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        StringBuilder lineBuffer = new StringBuilder();

        while (!shouldStop)
        {
            int bytesRead;
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }
            catch (Exception)
            {
                return; // connection dropped -- outer loop will reconnect
            }

            if (bytesRead == 0) return; // remote closed the connection

            lineBuffer.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            int newlineIndex;
            while ((newlineIndex = IndexOfNewline(lineBuffer)) >= 0)
            {
                string line = lineBuffer.ToString(0, newlineIndex).Trim();
                lineBuffer.Remove(0, newlineIndex + 1);
                if (line.Length > 0) ParseLine(line);
            }
        }
    }

    private static int IndexOfNewline(StringBuilder sb)
    {
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '\n') return i;
        }
        return -1;
    }

    // Parses "speed=8.50;distance=1.234;incline=15.00" -- see
    // DomyosTS100Connector's SendLineToUnity for the producing side.
    private void ParseLine(string line)
    {
        float newSpeed = 0f, newDistance = 0f, newIncline = 0f;

        string[] fields = line.Split(';');
        foreach (string field in fields)
        {
            int eq = field.IndexOf('=');
            if (eq < 0) continue;

            string key = field.Substring(0, eq);
            string value = field.Substring(eq + 1);

            if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            {
                continue;
            }

            switch (key)
            {
                case "speed": newSpeed = parsed; break;
                case "distance": newDistance = parsed; break;
                case "incline": newIncline = parsed; break;
            }
        }

        lock (dataLock)
        {
            speedKmh = newSpeed;
            distanceKm = newDistance;
            inclineDeg = newIncline;
            lastReceiveSeconds = clock.Elapsed.TotalSeconds;
        }
    }
}
