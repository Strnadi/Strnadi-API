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
namespace ApiGateway.Services;

public class FileSystemHelper
{
    private readonly string _pathToRecordingsDirectory = $"{AppDomain.CurrentDomain.BaseDirectory}/binRecordings/";
    
    /// <returns>Path of the generated file</returns>
    public string SaveRecordingSoundFile(int recordingId, int recordingPartId, byte[] data)
    {
        CreateDirectoryIfNotExists(recordingId);

        string path = _pathToRecordingsDirectory + $"{recordingId}_{recordingPartId}.wav";
        File.WriteAllBytes(path, data);

        return path;
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