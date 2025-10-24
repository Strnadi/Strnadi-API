using System.Diagnostics;
using System.Text;
using Shared.Logging;

namespace Shared.Tools;

public static class FFmpegService
{
    private static string ExecuteFFmpegOnce(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new Exception("FFmpeg process could not be started");
        string result = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return result;
    }
    
    private static Process ExecuteFFmpeg(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return Process.Start(psi) ?? throw new Exception("FFmpeg process could not be started");
    }

    private static string ExecuteFFprobe(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new Exception("FFmpeg process could not be started");
        string result = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return result;
    }

    public static string DetectFileFormat(string filePath) => ExecuteFFprobe($"-v error -show_entries format=format_name -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"");
    
    public static string GetFileDuration(string filePath) => ExecuteFFprobe($"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"");
    
    public static async Task NormalizeAudioAsync(byte[] binary, string outputPath)
    {
        if (binary == null || binary.Length == 0)
            throw new ArgumentException("Audio binary data cannot be null or empty", nameof(binary));

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        Process ffmpeg = ExecuteFFmpeg($"-i pipe:0 -f wav -acodec pcm_s16le -ar 48000 -ac 1 \"{outputPath}\"");
        
        Logger.Log($"FFmpegService::NormalizeAudioAsync: Started FFmpeg process for audio normalization to {outputPath}");
        
        var stderrTask = Task.Run(async () =>
        {
            var sb = new StringBuilder();
            while (!ffmpeg.StandardError.EndOfStream)
            {
                var line = await ffmpeg.StandardError.ReadLineAsync();
                if (line != null)
                    sb.AppendLine(line);
            }
            return sb.ToString();
        });
        
        var writeTask = Task.Run(async () =>
        {
            try
            {
                await ffmpeg.StandardInput.BaseStream.WriteAsync(binary);
                await ffmpeg.StandardInput.FlushAsync();
                ffmpeg.StandardInput.Close();
                Logger.Log("FFmpegService::NormalizeAudioAsync: Written input audio data to FFmpeg stdin");
            }
            catch (Exception ex)
            {
                Logger.Log($"FFmpegService::NormalizeAudioAsync: Error writing to stdin: {ex.Message}");
                throw;
            }
        });

        await writeTask;
        await ffmpeg.WaitForExitAsync();
        
        Logger.Log("FFmpegService::NormalizeAudioAsync: FFmpeg process exited");

        var errors = await stderrTask;
        if (!string.IsNullOrWhiteSpace(errors))
            Logger.Log($"FFmpegService::NormalizeAudioAsync: FFmpeg stderr: {errors}");

        if (ffmpeg.ExitCode != 0)
        {
            throw new Exception($"FFmpeg exited with code {ffmpeg.ExitCode}: {errors}");
        }

        if (!File.Exists(outputPath))
        {
            throw new Exception($"FFmpeg did not create output file: {outputPath}");
        }

        Logger.Log($"FFmpegService::NormalizeAudioAsync: Successfully normalized audio to {outputPath}");
    }
}