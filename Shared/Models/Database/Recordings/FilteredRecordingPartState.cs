namespace Shared.Models.Database.Recordings;

public enum FilteredRecordingPartState
{
    AwaitingProcession = 1,
    ConfirmedWithCorrectGuess = 2,
    ConfirmedWithWrongGuess = 3,
    UnableToConfirm = 4,
    // On recording part that user did not marked as containing bird song
    ConfirmedManually = 5, 
    DetectedByAi = 6,
    DetectedByAiAndConfirmed = 7,
}