namespace ZonaMatch.Domain.Common;

/// <summary>Text limits shared by application validation and database configuration.</summary>
public static class TextLimits
{
    public const int ReviewTextMinimum = 20;
    public const int ReviewText = 1000;
    public const int ReviewRejectionReason = 300;
    public const int QuestionTextMinimum = 10;
    public const int QuestionText = 300;
    public const int AnswerTextMinimum = 2;
    public const int AnswerText = 1000;
    public const int PlaceName = 120;
    public const int OpeningHours = 120;
    public const int Address = 200;
    public const int Comment = 500;
}
