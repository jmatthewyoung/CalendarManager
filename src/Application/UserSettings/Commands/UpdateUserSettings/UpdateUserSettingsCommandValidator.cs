namespace CalendarManager.Application.UserSettings.Commands.UpdateUserSettings;

public class UpdateUserSettingsCommandValidator : AbstractValidator<UpdateUserSettingsCommand>
{
    public UpdateUserSettingsCommandValidator()
    {
        RuleFor(v => v.TimeZoneId)
            .NotEmpty()
            .Must(BeARecognizedTimeZone)
                .WithMessage("'{PropertyName}' must be a recognized IANA time zone id.");
    }

    private static bool BeARecognizedTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
