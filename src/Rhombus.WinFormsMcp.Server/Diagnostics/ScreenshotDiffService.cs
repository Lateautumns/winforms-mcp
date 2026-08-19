using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Rhombus.WinFormsMcp.Server.Diagnostics;

internal sealed class ScreenshotDiffService {
    private const int MaxDimension = 8_192;
    private const long MaxPixels = 16_000_000;
    private const long MaxInputBytes = 96L * 1024 * 1024;
    private const int MaxBase64Characters = 128 * 1024 * 1024;
    private const int RegionTileSize = 8;

    public ScreenshotDiffResult Compare(
        string? beforePath,
        string? afterPath,
        string? beforeBase64,
        string? afterBase64,
        int maxRegions,
        int pixelThreshold,
        CancellationToken cancellationToken) {
        using var before = Load(beforePath, beforeBase64, "before");
        using var after = Load(afterPath, afterBase64, "after");
        var width = Math.Max(before.Width, after.Width);
        var height = Math.Max(before.Height, after.Height);
        if (width > MaxDimension || height > MaxDimension || (long)width * height > MaxPixels)
            throw new InvalidOperationException("Screenshot dimensions exceed the configured diff limit.");

        var boundedRegions = Math.Clamp(maxRegions, 1, 10_000);
        var threshold = Math.Clamp(pixelThreshold, 0, 255);
        var dimensionsMatch = before.Size == after.Size;
        var changedPixels = 0L;
        var comparedPixels = (long)width * height;
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        var beforePixels = PixelBuffer.Read(before);
        var afterPixels = PixelBuffer.Read(after);
        var tiles = new ChangedTileMap(width, height, RegionTileSize);

        for (var y = 0; y < height; y++) {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++) {
                if (!IsChanged(beforePixels, afterPixels, x, y, threshold))
                    continue;
                changedPixels++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                tiles.Mark(x, y);
            }
        }

        var (regions, regionCount) = tiles.BuildRegions(boundedRegions, cancellationToken);

        var changedBounds = maxX < 0
            ? new DiffRect()
            : new DiffRect { X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1 };
        return new ScreenshotDiffResult {
            DimensionsMatch = dimensionsMatch,
            BeforeWidth = before.Width,
            BeforeHeight = before.Height,
            AfterWidth = after.Width,
            AfterHeight = after.Height,
            ChangedPixelCount = changedPixels,
            ComparedPixelCount = comparedPixels,
            ChangedPixelRatio = comparedPixels == 0 ? 0d : changedPixels / (double)comparedPixels,
            ChangedBounds = changedBounds,
            ChangedRegions = regions,
            RegionsTruncated = regionCount > boundedRegions,
            PixelThreshold = threshold,
            RegionGranularity = RegionTileSize
        };
    }

    private static Bitmap Load(string? path, string? base64, string label) {
        if (string.IsNullOrWhiteSpace(path) == string.IsNullOrWhiteSpace(base64))
            throw new ArgumentException($"Exactly one of '{label}Path' or '{label}Base64' must be provided.");
        try {
            if (!string.IsNullOrWhiteSpace(base64)) {
                if (base64.Length > MaxBase64Characters)
                    throw new InvalidOperationException("Screenshot payload exceeds the configured input size limit.");
                using var base64Stream = new MemoryStream(Convert.FromBase64String(base64), writable: false);
                using var base64Source = new Bitmap(base64Stream);
                ValidateSource(base64Source);
                return new Bitmap(base64Source);
            }
            if (!File.Exists(path))
                throw new FileNotFoundException($"Screenshot file '{path}' was not found.", path);
            if (new FileInfo(path!).Length > MaxInputBytes)
                throw new InvalidOperationException("Screenshot file exceeds the configured input size limit.");
            using var fileStream = File.OpenRead(path!);
            using var fileSource = new Bitmap(fileStream);
            ValidateSource(fileSource);
            return new Bitmap(fileSource);
        }
        catch (FormatException ex) {
            throw new ArgumentException($"'{label}Base64' is not valid base64.", ex);
        }
        catch (ArgumentException ex) {
            throw new ArgumentException($"'{label}' is not a supported image.", ex);
        }
    }

    private static void ValidateSource(Bitmap source) {
        if (source.Width > MaxDimension || source.Height > MaxDimension ||
            (long)source.Width * source.Height > MaxPixels)
            throw new InvalidOperationException("Screenshot dimensions exceed the configured diff limit.");
    }

    private static bool IsChanged(PixelBuffer before, PixelBuffer after, int x, int y, int threshold) {
        if (x >= before.Width || y >= before.Height || x >= after.Width || y >= after.Height)
            return true;
        var left = before.GetOffset(x, y);
        var right = after.GetOffset(x, y);
        return Math.Abs(before.Bytes[left] - after.Bytes[right]) > threshold ||
               Math.Abs(before.Bytes[left + 1] - after.Bytes[right + 1]) > threshold ||
               Math.Abs(before.Bytes[left + 2] - after.Bytes[right + 2]) > threshold ||
               Math.Abs(before.Bytes[left + 3] - after.Bytes[right + 3]) > threshold;
    }

    private sealed class PixelBuffer {
        private PixelBuffer(int width, int height, byte[] bytes) {
            Width = width;
            Height = height;
            Bytes = bytes;
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] Bytes { get; }

        public int GetOffset(int x, int y) => ((y * Width) + x) * 4;

        public static PixelBuffer Read(Bitmap source) {
            using var normalized = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(normalized))
                graphics.DrawImageUnscaled(source, 0, 0);

            var bounds = new Rectangle(0, 0, normalized.Width, normalized.Height);
            var data = normalized.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try {
                var rowBytes = checked(normalized.Width * 4);
                var bytes = new byte[checked(rowBytes * normalized.Height)];
                for (var y = 0; y < normalized.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * rowBytes, rowBytes);
                return new PixelBuffer(normalized.Width, normalized.Height, bytes);
            }
            finally {
                normalized.UnlockBits(data);
            }
        }
    }

    private sealed class ChangedTileMap {
        private readonly int _columns;
        private readonly int _rows;
        private readonly int _tileSize;
        private readonly int[] _pixelCounts;
        private readonly int[] _minX;
        private readonly int[] _minY;
        private readonly int[] _maxX;
        private readonly int[] _maxY;

        public ChangedTileMap(int width, int height, int tileSize) {
            _tileSize = tileSize;
            _columns = (width + tileSize - 1) / tileSize;
            _rows = (height + tileSize - 1) / tileSize;
            var count = checked(_columns * _rows);
            _pixelCounts = new int[count];
            _minX = Enumerable.Repeat(int.MaxValue, count).ToArray();
            _minY = Enumerable.Repeat(int.MaxValue, count).ToArray();
            _maxX = Enumerable.Repeat(-1, count).ToArray();
            _maxY = Enumerable.Repeat(-1, count).ToArray();
        }

        public void Mark(int x, int y) {
            var index = ((y / _tileSize) * _columns) + (x / _tileSize);
            _pixelCounts[index]++;
            _minX[index] = Math.Min(_minX[index], x);
            _minY[index] = Math.Min(_minY[index], y);
            _maxX[index] = Math.Max(_maxX[index], x);
            _maxY[index] = Math.Max(_maxY[index], y);
        }

        public (List<DiffRegion> Regions, int RegionCount) BuildRegions(
            int maxRegions,
            CancellationToken cancellationToken) {
            var visited = new bool[_pixelCounts.Length];
            var regions = new List<DiffRegion>(Math.Min(maxRegions, 256));
            var regionCount = 0;
            for (var index = 0; index < _pixelCounts.Length; index++) {
                cancellationToken.ThrowIfCancellationRequested();
                if (visited[index] || _pixelCounts[index] == 0)
                    continue;
                regionCount++;
                var region = Flood(index, visited, cancellationToken);
                if (regions.Count < maxRegions)
                    regions.Add(region);
            }
            return (regions, regionCount);
        }

        private DiffRegion Flood(int start, bool[] visited, CancellationToken cancellationToken) {
            var queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            var pixels = 0;
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = -1;
            var maxY = -1;
            while (queue.Count > 0) {
                cancellationToken.ThrowIfCancellationRequested();
                var index = queue.Dequeue();
                pixels += _pixelCounts[index];
                minX = Math.Min(minX, _minX[index]);
                minY = Math.Min(minY, _minY[index]);
                maxX = Math.Max(maxX, _maxX[index]);
                maxY = Math.Max(maxY, _maxY[index]);
                var tileX = index % _columns;
                var tileY = index / _columns;
                Enqueue(tileX - 1, tileY, visited, queue);
                Enqueue(tileX + 1, tileY, visited, queue);
                Enqueue(tileX, tileY - 1, visited, queue);
                Enqueue(tileX, tileY + 1, visited, queue);
            }
            return new DiffRegion {
                Bounds = new DiffRect { X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1 },
                PixelCount = pixels
            };
        }

        private void Enqueue(int x, int y, bool[] visited, Queue<int> queue) {
            if (x < 0 || y < 0 || x >= _columns || y >= _rows)
                return;
            var index = (y * _columns) + x;
            if (visited[index] || _pixelCounts[index] == 0)
                return;
            visited[index] = true;
            queue.Enqueue(index);
        }
    }
}

internal sealed class ScreenshotDiffResult {
    public bool DimensionsMatch { get; set; }
    public int BeforeWidth { get; set; }
    public int BeforeHeight { get; set; }
    public int AfterWidth { get; set; }
    public int AfterHeight { get; set; }
    public long ChangedPixelCount { get; set; }
    public long ComparedPixelCount { get; set; }
    public double ChangedPixelRatio { get; set; }
    public DiffRect ChangedBounds { get; set; } = new();
    public List<DiffRegion> ChangedRegions { get; set; } = new();
    public bool RegionsTruncated { get; set; }
    public int PixelThreshold { get; set; }
    public int RegionGranularity { get; set; }
}

internal sealed class DiffRegion {
    public DiffRect Bounds { get; set; } = new();
    public int PixelCount { get; set; }
}

internal sealed class DiffRect {
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}