using System.Diagnostics;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Audio;

public class FFmpegAudioNormalizer : IAudioNormalizer
{
    public async Task<byte[]> NormalizeAsync(byte[] input, CancellationToken cancellationToken = default)
    {
        var outputPath = Path.GetTempFileName();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i pipe:0 -f wav -acodec pcm_s16le -ar 48000 -ac 1 \"{outputPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg process could not be started");

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.StandardInput.BaseStream.WriteAsync(input, cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken);
            var errors = await stderrTask;

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}: {errors}");

            if (!File.Exists(outputPath))
                throw new InvalidOperationException("FFmpeg did not produce an output file");

            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}