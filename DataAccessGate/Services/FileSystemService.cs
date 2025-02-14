namespace DataAccessGate.Services;

public class FileSystemHelper
{
    private readonly string _pathToRecordingsDirectory = $"{AppDomain.CurrentDomain.BaseDirectory}binRecordings/";
    
    /// <returns>Path of the generated file</returns>
    public string SaveRecordingSoundFile(int recordingId, int recordingPartId, byte[] data)
    {
        CreateDirectoryIfNotExists(recordingId);

        string path = _pathToRecordingsDirectory + $"{recordingId}/" + $"{recordingId}_{recordingPartId}.mp3";
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