using Microsoft.Extensions.Options;
using Pv;
using UD1Chassis.Options;
using OpenTK.Audio.OpenAL;

namespace UD1Chassis;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly PorcupineOptions _porcupineOptions;
    private ALCaptureDevice _captureDevice;
    private int _frameLength;
    private int _sampleRate;

    public Worker(ILogger<Worker> logger, IOptions<PorcupineOptions> options)
    {
        _logger = logger;
        _porcupineOptions = options.Value;
        // Porcupine expects 16kHz mono, 16-bit signed PCM
        _sampleRate = 16000;
        _frameLength = 512; // Typical frame size for Porcupine
        _captureDevice = ALC.CaptureOpenDevice(null, _sampleRate, ALFormat.Mono16, _frameLength * 10); // 10 frames buffer
        if (_captureDevice == ALCaptureDevice.Null)
            throw new Exception("Failed to open default audio capture device");
        ALC.CaptureStart(_captureDevice);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker starting with Porcupine API");
        Porcupine porcupine = Porcupine.FromKeywordPaths(
            accessKey: _porcupineOptions.ApiKey,
            keywordPaths: ["Resources/U-D-One_en_linux_v3_0_0.ppn"],
            sensitivities: [0.9f]
        );
        while (!stoppingToken.IsCancellationRequested)
        {
            short[] audioFrame = GetNextAudioFrame();
            // Log first 10 values of the audio frame for debugging
            //_logger.LogInformation($"AudioFrame: [{string.Join(", ", audioFrame.Take(10))} ...]");
            int keywordIndex = porcupine.Process(audioFrame);
            if(keywordIndex == 0)
            {
                _logger.LogInformation("Detected 'U-D-One'");
            }
        }
    }

    short[] GetNextAudioFrame()
    {
        // Wait until enough samples are available
        int samplesAvailable = 0;
        do
        {
            samplesAvailable = ALC.GetInteger(_captureDevice, AlcGetInteger.CaptureSamples);
            _logger.LogDebug($"Samples available: {samplesAvailable}");
            if (samplesAvailable < _frameLength)
                Thread.Sleep(5);
        } while (samplesAvailable < _frameLength);

        byte[] buffer = new byte[_frameLength * 2]; // 2 bytes per sample (16-bit)
        ALC.CaptureSamples(_captureDevice, buffer, _frameLength);
        short[] frame = new short[_frameLength];
        Buffer.BlockCopy(buffer, 0, frame, 0, buffer.Length);
        return frame;
    }

    public override void Dispose()
    {
        if (_captureDevice != ALCaptureDevice.Null)
        {
            ALC.CaptureStop(_captureDevice);
            ALC.CaptureCloseDevice(_captureDevice);
            _captureDevice = ALCaptureDevice.Null;
        }
        base.Dispose();
    }
}
