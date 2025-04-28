/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System.Text;

namespace Shared.Tools;

public class FileSystemHelper
{
    private readonly string _pathToRecordingsDirectory = $"{AppDomain.CurrentDomain.BaseDirectory}recordings/";
    
    private const string recording_file_extension = "wav";
    
    /// <returns>Path of the generated file</returns>
    public async Task<string> SaveRecordingFileAsync(int recordingId, int recordingPartId, byte[] data)
    {
        CreateRecordingsDirectoryIfNotExists(recordingId);
        
        string path = _pathToRecordingsDirectory + $"{recordingId}/" + $"{recordingId}_{recordingPartId}.{recording_file_extension}";
        await File.WriteAllBytesAsync(path, data);

        return path;
    }
    
    public async Task<byte[]> ReadRecordingFileAsync(int recordingId, int recordingPartId)
    {
        string path = GetRecordingPartFilePath(recordingId, recordingPartId);
        return await File.ReadAllBytesAsync(path);
    }
    
    public async Task<string> SaveRecordingPhotoFileAsync(int recordingId, int photoId, string base64, string format)
    {
        CreateRecordingPhotosDirectoryIfNotExists(recordingId);

        string path = GetRecordingPhotoFilePath(recordingId, photoId, format);
        
        byte[] bytes = Convert.FromBase64String(base64);
        await File.WriteAllBytesAsync(path, bytes);

        return path;
    }

    private string GetRecordingsDirectoryPath(int recordingId)
    {
        return _pathToRecordingsDirectory + $"{recordingId}/";
    }

    private string GetRecordingPartFilePath(int recordingId, int recordingPartId)
    {
        return $"{GetRecordingsDirectoryPath(recordingId)}/{recordingId}_{recordingPartId}.{recording_file_extension}";
    }

    private string GetRecordingPhotosDirectoryPath(int recordingId)
    {
        return GetRecordingsDirectoryPath(recordingId) + "photos";
    }
    
    private void CreateRecordingsDirectoryIfNotExists(int recordingId)
    {
        string path = GetRecordingsDirectoryPath(recordingId);

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private void CreateRecordingPhotosDirectoryIfNotExists(int recordingId)
    {
        string path = GetRecordingPhotosDirectoryPath(recordingId);

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private string GetRecordingPhotoFilePath(int recordingId, int photoId, string format)
    {
        return GetRecordingPhotosDirectoryPath(recordingId) + $"/{photoId}.{format}";
    }

    public async Task<string> SaveUserPhotoFileAsync(int userId, string base64, string format)
    {
        CreateUserPhotosDirectoryIfNotExists();

        string filePath = CreateUserPhotoFilePath(userId, format);
        byte[] decoded = Convert.FromBase64String(base64);
        await File.WriteAllBytesAsync(filePath, decoded);
        return filePath;
    }

    private string CreateUserPhotoFilePath(int userId, string format)
    {
        return $"{GetUserPhotosDirectoryPath()}u_{userId}.{format}";
    }

    private void CreateUserPhotosDirectoryIfNotExists()
    {
        if (!Directory.Exists(GetUserPhotosDirectoryPath())) Directory.CreateDirectory(GetUserPhotosDirectoryPath());
    }
    
    private string GetUserPhotosDirectoryPath()
    {
        return "users/";
    }

    public async Task<byte[]?> ReadUserPhotoFileAsync(int userId, string format)
    { 
        string filePath = CreateUserPhotoFilePath(userId, format);
        if (!File.Exists(filePath))
            return null;

        byte[] content = await File.ReadAllBytesAsync(filePath);
        return content;
    }
}