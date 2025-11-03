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

public static class FileSystemHelper
{
    private static readonly string _pathToRecordingsDirectory = $"{AppDomain.CurrentDomain.BaseDirectory}recordings/";
    
    public static string GetNormalizedRecordingFilePath(int recordingId, int recordingPartId)
    {
        return _pathToRecordingsDirectory + $"{recordingId}/" + $"{recordingId}_{recordingPartId}.normalized.wav";
    }
    
    public static async Task<string> SaveOriginalRecordingFileAsync(int recordingId, int recordingPartId, byte[] data)
    {
        CreateRecordingsDirectoryIfNotExists(recordingId);

        string path = _pathToRecordingsDirectory + $"{recordingId}/" + $"{recordingId}_{recordingPartId}.original.wav";
        await File.WriteAllBytesAsync(path, data);

        return path;
    }
    
    public static async Task<byte[]> ReadRecordingFileAsync(string path)
    {
        return await File.ReadAllBytesAsync(path);
    }
    
    public static async Task<string> SaveRecordingPhotoFileAsync(int recordingId, int photoId, string base64, string format)
    {
        CreateRecordingPhotosDirectoryIfNotExists(recordingId);

        string path = GetRecordingPhotoFilePath(recordingId, photoId, format);
        
        byte[] bytes = Convert.FromBase64String(base64);
        await File.WriteAllBytesAsync(path, bytes);

        return path;
    }

    private static string GetRecordingsDirectoryPath(int recordingId)
    {
        return _pathToRecordingsDirectory + $"{recordingId}/";
    }

    private static string GetRecordingPartFilePath(int recordingId, int recordingPartId)
    {
        return $"{GetRecordingsDirectoryPath(recordingId)}/{recordingId}_{recordingPartId}.";
    }

    private static string GetRecordingPhotosDirectoryPath(int recordingId)
    {
        return GetRecordingsDirectoryPath(recordingId) + "photos";
    }
    
    private static void CreateRecordingsDirectoryIfNotExists(int recordingId)
    {
        string path = GetRecordingsDirectoryPath(recordingId);

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private static void CreateRecordingPhotosDirectoryIfNotExists(int recordingId)
    {
        string path = GetRecordingPhotosDirectoryPath(recordingId);

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private static string GetRecordingPhotoFilePath(int recordingId, int photoId, string format)
    {
        return GetRecordingPhotosDirectoryPath(recordingId) + $"/{photoId}.{format}";
    }

    public static async Task<string> SaveUserPhotoFileAsync(int userId, string base64, string format)
    {
        CreateUserPhotosDirectoryIfNotExists();

        string filePath = CreateUserPhotoFilePath(userId, format);
        byte[] decoded = Convert.FromBase64String(base64);
        await File.WriteAllBytesAsync(filePath, decoded);
        return filePath;
    }

    private static string CreateUserPhotoFilePath(int userId, string format)
    {
        return $"{GetUserPhotosDirectoryPath()}u_{userId}.{format}";
    }

    private static void CreateUserPhotosDirectoryIfNotExists()
    {
        if (!Directory.Exists(GetUserPhotosDirectoryPath())) Directory.CreateDirectory(GetUserPhotosDirectoryPath());
    }
    
    private static string GetUserPhotosDirectoryPath()
    {
        return "users/";
    }

    public static async Task<byte[]?> ReadUserPhotoFileAsync(int userId, string format)
    { 
        string filePath = CreateUserPhotoFilePath(userId, format);
        if (!File.Exists(filePath))
            return null;

        byte[] content = await File.ReadAllBytesAsync(filePath);
        return content;
    }

    public static async Task<byte[]> ReadArticleFileAsync(int id, string fileName)
    {
        string filePath = CreateArticleAttachmentPath(id, fileName);
        return await File.ReadAllBytesAsync(filePath);
    }

    private static string CreateArticleDirectoryPath(int id)
    {
        return $"articles/{id}";
    }

    public static string CreateArticleAttachmentPath(int id, string fileName)
    {
        return $"articles/{id}/{fileName}";
    }

    public static bool ExistsArticleAttachment(int id, string fileName)
    {
        string filePath = CreateArticleAttachmentPath(id, fileName);
        return File.Exists(filePath);
    }

    public static async Task SaveArticleFileAsync(int articleId, string fileName, string base64)
    {
        if (!Directory.Exists(CreateArticleDirectoryPath(articleId)))
            Directory.CreateDirectory(CreateArticleDirectoryPath(articleId));
        
        string path = CreateArticleAttachmentPath(articleId, fileName);
        byte[] content = Convert.FromBase64String(base64);
        await File.WriteAllBytesAsync(path, content);
    }

    public static void DeleteArticleAttachment(int id, string fileName)
    {
        string path = CreateArticleAttachmentPath(id, fileName);
        File.Delete(path);
    }

}