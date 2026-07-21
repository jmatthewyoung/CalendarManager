using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.CalendarConnections.Commands.SetCalendarConnectionColor;

public class SetCalendarConnectionColorCommandValidator : AbstractValidator<SetCalendarConnectionColorCommand>
{
    public SetCalendarConnectionColorCommandValidator()
    {
        RuleFor(v => v.Colour)
            .NotEmpty()
            .Must(code => Colour.SupportedColours.Any(c => c.Code == code))
                .WithMessage("'{PropertyName}' must be one of the supported colours.");
    }
}
