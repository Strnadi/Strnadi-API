namespace Strnadi.Domain.Enums;

public enum FilteredRecordingPartState
{
    AwaitingProcession = 1,
    ConfirmedWithCorrectGuess = 2,
    ConfirmedWithWrongGuess = 3,
    UnableToConfirm = 4,
    ConfirmedManually = 5,
    DetectedByAi = 6,
    DetectedByAiAndConfirmed = 7,
}
