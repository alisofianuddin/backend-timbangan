using System.IO.Ports;
using System.Text.RegularExpressions;

namespace Backend.Services;

public class ScaleData
{
    public double Weight { get; set; } = 0.0;
    public string Unit { get; set; } = "g";
    public string Status { get; set; } = "DISCONNECTED"; // STABLE, DISCONNECTED
    public string Raw { get; set; } = "";
    public string LastUpdate { get; set; } = "";
}

/// <summary>Event fired when scale sends a stable reading (SS mode)</summary>
public class StableReadingEvent
{
    public double Weight { get; set; }
    public string Unit { get; set; } = "g";
    public DateTime Timestamp { get; set; }
}

public class ScaleBackgroundService : BackgroundService
{
    private readonly ILogger<ScaleBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    
    private readonly object _lock = new object();
    private readonly ScaleData _liveData = new ScaleData();

    // Queue of stable readings waiting to be consumed
    private readonly Queue<StableReadingEvent> _stableReadings = new Queue<StableReadingEvent>();
    private readonly object _queueLock = new object();

    public ScaleBackgroundService(ILogger<ScaleBackgroundService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public ScaleData GetLiveData()
    {
        lock (_lock)
        {
            return new ScaleData
            {
                Weight = _liveData.Weight,
                Unit = _liveData.Unit,
                Status = _liveData.Status,
                Raw = _liveData.Raw,
                LastUpdate = _liveData.LastUpdate
            };
        }
    }

    /// <summary>Dequeue next stable reading, or null if none available</summary>
    public StableReadingEvent? DequeueStableReading()
    {
        lock (_queueLock)
        {
            return _stableReadings.Count > 0 ? _stableReadings.Dequeue() : null;
        }
    }

    /// <summary>Check if there are pending stable readings</summary>
    public int PendingReadingsCount()
    {
        lock (_queueLock)
        {
            return _stableReadings.Count;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var portName = _configuration["ScaleSettings:PortName"] ?? "COM8";
        var baudRate = _configuration.GetValue<int>("ScaleSettings:BaudRate", 9600);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                serialPort.Handshake = Handshake.XOnXOff;
                serialPort.ReadTimeout = 2000;
                serialPort.Open();

                _logger.LogInformation($"🔌 Serial connected ke {portName} (SS Mode)");

                lock (_lock)
                {
                    _liveData.Status = "STABLE";
                }

                while (!stoppingToken.IsCancellationRequested && serialPort.IsOpen)
                {
                    try
                    {
                        var raw = serialPort.ReadLine();
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            raw = raw.Trim();
                            _logger.LogDebug($"📡 Raw data: [{raw}]");

                            // Parse weight from any data received
                            // SS mode: every data received = stable reading
                            var numberMatch = Regex.Match(raw, @"[\d]+\.[\d]+|[\d]+");
                            double weight = 0.0;
                            if (numberMatch.Success && double.TryParse(numberMatch.Value, 
                                System.Globalization.NumberStyles.Any, 
                                System.Globalization.CultureInfo.InvariantCulture, 
                                out double w))
                            {
                                weight = w;
                            }

                            // Parse unit
                            var unitMatch = Regex.Match(raw, @"\b(g|kg)\b", RegexOptions.IgnoreCase);
                            string unit = unitMatch.Success ? unitMatch.Groups[1].Value.ToLower() : "g";

                            var now = DateTime.Now;

                            // Update live data
                            lock (_lock)
                            {
                                _liveData.Weight = weight;
                                _liveData.Unit = unit;
                                _liveData.Status = "STABLE";
                                _liveData.Raw = raw;
                                _liveData.LastUpdate = now.ToString("yyyy-MM-dd HH:mm:ss");
                            }

                            // SS mode: every reading is stable, enqueue it
                            if (weight > 0)
                            {
                                lock (_queueLock)
                                {
                                    _stableReadings.Enqueue(new StableReadingEvent
                                    {
                                        Weight = weight,
                                        Unit = unit,
                                        Timestamp = now
                                    });
                                }
                                _logger.LogInformation($"✅ Stable reading: {weight} {unit} (queue: {PendingReadingsCount()})");
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        // No data received — normal for SS mode (only sends on button press)
                    }
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning($"⚠️ Serial port error ({portName}): {ex.Message}. Check if port is in use or disconnected.");
                lock (_lock)
                {
                    _liveData.Status = "DISCONNECTED";
                }
                await Task.Delay(5000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Unexpected error in ScaleBackgroundService: {ex.Message}");
                lock (_lock)
                {
                    _liveData.Status = "DISCONNECTED";
                }
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
