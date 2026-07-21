using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.Events.Commands.CreateLocalEvent;

public class CreateLocalEventCommandValidator : AbstractValidator<CreateLocalEventCommand>
{
    public CreateLocalEventCommandValidator()
    {
        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(v => v.EndUtc)
            .GreaterThan(v => v.StartUtc);

        RuleFor(v => v.Colour)
            .NotEmpty()
            .Must(code => Colour.SupportedColours.Any(c => c.Code == code))
                .WithMessage("'{PropertyName}' must be one of the supported colours.");
    }
}
