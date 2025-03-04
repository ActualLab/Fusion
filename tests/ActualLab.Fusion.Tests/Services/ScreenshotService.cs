using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
#if NET8_0_OR_GREATER
[MessagePackObject(true, SuppressSourceGeneration = true)]
#else
[MessagePackObject(true)]
#endif
public partial record Screenshot
{
    [DataMember, MemoryPackOrder(0), Key(0)] public int Width { get; init; }
    [DataMember, MemoryPackOrder(1), Key(1)] public int Height { get; init; }
    [DataMember, MemoryPackOrder(2), Key(2)] public Moment CapturedAt { get; init; }
    [DataMember, MemoryPackOrder(3), Key(3)] public byte[] Image { get; init; } = [];
}

public interface IScreenshotService : IComputeService
{
    [ComputeMethod, RemoteComputeMethod(CacheMode = RemoteComputedCacheMode.NoCache)]
    public Task<Screenshot> GetScreenshotAlt(int width, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 0.3)]
    public Task<Screenshot> GetScreenshot(int width, CancellationToken cancellationToken = default);
}

#pragma warning disable CA1416

public class ScreenshotService : IScreenshotService
{
    private readonly ImageCodecInfo _jpegEncoder;
    private readonly EncoderParameters _jpegEncoderParameters;
    private readonly Rectangle _displayDimensions;
    private volatile int _screenshotCount;

    public int ScreenshotCount => _screenshotCount;

    public ScreenshotService()
    {
        _jpegEncoder = ImageCodecInfo
            .GetImageDecoders()
            .Single(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        _jpegEncoderParameters = new EncoderParameters(1) {
            Param = {[0] = new EncoderParameter(Encoder.Quality, 50L)}
        };
        _displayDimensions = DisplayInfo.PrimaryDisplayDimensions
            ?? new Rectangle(0, 0, 1920, 1080);
    }

    // [ComputeMethod]
    public virtual Task<Screenshot> GetScreenshotAlt(int width, CancellationToken cancellationToken = default)
        => GetScreenshot(width, cancellationToken);

    // [ComputeMethod(MinCacheDuration = 0.3)]
    public virtual async Task<Screenshot> GetScreenshot(int width, CancellationToken cancellationToken = default)
    {
        var bScreen = await GetScreenshot(cancellationToken).ConfigureAwait(false);

        // The code below scales a full-resolution screenshot to a desirable resolution
        var (w, h) = (_displayDimensions.Width, _displayDimensions.Height);
        var ow = width;
        var oh = h * ow / w;
        using var bOut = new Bitmap(ow, oh);
        using var gOut = Graphics.FromImage(bOut);
        gOut.CompositingQuality = CompositingQuality.HighSpeed;
        gOut.InterpolationMode = InterpolationMode.Default;
        gOut.CompositingMode = CompositingMode.SourceCopy;
        gOut.DrawImage(bScreen, 0, 0, ow, oh);

        using var stream = new MemoryStream();
        bOut.Save(stream, _jpegEncoder, _jpegEncoderParameters);
        return new Screenshot {
            Width = ow,
            Height = oh,
            CapturedAt = Moment.Now,
            Image = stream.ToArray(),
        };
    }

    [ComputeMethod(AutoInvalidationDelay = 0.1)]
    protected virtual Task<Bitmap> GetScreenshot(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _screenshotCount);

        // This method takes a full-resolution screenshot
        var (w, h) = (_displayDimensions.Width, _displayDimensions.Height);
        var bScreen = new Bitmap(w, h);
        using var gScreen = Graphics.FromImage(bScreen);
        gScreen.CopyFromScreen(0, 0, 0, 0, bScreen.Size);
        Computed.GetCurrent().Invalidated += c => {
            _ = Task.Delay(2000, default).ContinueWith(_ => {
                // Let's dispose these values in 2 seconds
                var computed = (Computed<Bitmap>) c;
                if (computed.HasValue)
                    computed.Value.Dispose();
            }, TaskScheduler.Default);
        };
        return Task.FromResult(bScreen);
    }
}
