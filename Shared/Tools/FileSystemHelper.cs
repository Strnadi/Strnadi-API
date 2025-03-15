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
namespace Shared.Tools;

public class FileSystemHelper
{
    private readonly string _pathToRecordingsDirectory = $"{AppDomain.CurrentDomain.BaseDirectory}recordings/";

    private const string file_extension = "wav";
    
    /// <returns>Path of the generated file</returns>
    public string SaveRecordingFile(int recordingId, int recordingPartId, byte[] data)
    {
        CreateDirectoryIfNotExists(recordingId);
        
        string path = _pathToRecordingsDirectory + $"{recordingId}/" + $"{recordingId}_{recordingPartId}.{file_extension}";
        File.WriteAllBytes(path, data);

        return path;
    }
    
    public byte[] ReadRecordingFile(int recordingId, int recordingPartId)
    {
        string path = _pathToRecordingsDirectory + $"{recordingId}/" + $"{recordingId}_{recordingPartId}.{file_extension}";
        return File.ReadAllBytes(path);
    }
    
    private void CreateDirectoryIfNotExists(int recordingId)
    {
        string path = _pathToRecordingsDirectory + recordingId;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}