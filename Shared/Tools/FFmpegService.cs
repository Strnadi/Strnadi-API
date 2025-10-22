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
    
    public static async Task<byte[]> NormalizeAudioAsync(byte[] content)
    {
        Process ffmpeg = ExecuteFFmpeg("-i pipe:0 -f wav -acodec pcm_s16le -ar 48000 -ac 1 pipe:1");
        
        Logger.Log("FFmpegService::NormalizeAudioAsync: Started FFmpeg process for audio normalization");
        
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
        
        // write input audio to stdin and close it 
        await ffmpeg.StandardInput.BaseStream.WriteAsync(content);
        await ffmpeg.StandardInput.FlushAsync();
        ffmpeg.StandardInput.Close();
        
        Logger.Log("FFmpegService::NormalizeAudioAsync: Written input audio to FFmpeg stdin");

        Logger.Log("FFmpegService::NormalizeAudioAsync: Started reading FFmpeg stderr");

        // read stdout (normalized audio)
        using var ms = new MemoryStream();
        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(ms);
        
        Logger.Log("FFmpegService::NormalizeAudioAsync: Read normalized audio from FFmpeg stdout");

        // await process exit
        await ffmpeg.WaitForExitAsync();
        
        Logger.Log("FFmpegService::NormalizeAudioAsync: FFmpeg process exited");

        var errors = await stderrTask;
        if (!string.IsNullOrWhiteSpace(errors))
            Logger.Log($"FFmpegService::NormalizeAudioAsync: {errors}");

        return ms.ToArray();    
    }
}