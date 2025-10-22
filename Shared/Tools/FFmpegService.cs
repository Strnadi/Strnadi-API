using System.Diagnostics;
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
        await ffmpeg.StandardInput.BaseStream.WriteAsync(content);
        ffmpeg.StandardInput.Close();

        using var memoryStream = new MemoryStream();
        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(memoryStream);
        
        string ffmpegErrors = await ffmpeg.StandardError.ReadToEndAsync();
        if (!string.IsNullOrEmpty(ffmpegErrors))
            Logger.Log($"FFmpegService::NormalizeAudioAsync: {ffmpeg}");

        return memoryStream.ToArray();
    }
}