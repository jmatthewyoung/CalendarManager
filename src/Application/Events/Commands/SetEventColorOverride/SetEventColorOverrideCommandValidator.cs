using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.Events.Commands.SetEventColorOverride;

public class SetEventColorOverrideCommandValidator : AbstractValidator<SetEventColorOverrideCommand>
{
    public SetEventColorOverrideCommandValidator()
    {
        RuleFor(v => v.Colour)
            .NotEmpty()
            .Must(code => Colour.SupportedColours.Any(c => c.Code == code))
                .WithMessage("'{PropertyName}' must be one of the supported colours.");
    }
}
