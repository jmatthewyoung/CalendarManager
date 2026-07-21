using CalendarManager.Application.Common.Exceptions;
using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Application.Common.Security;
using CalendarManager.Application.Sync.Commands.SyncCalendarConnection;
using CalendarManager.Domain.Entities;
using CalendarManager.Domain.Enums;
using CalendarManager.Domain.ValueObjects;

namespace CalendarManager.Application.CalendarConnections.Commands.CompleteCalendarConnection;

[Authorize]
public record CompleteCalendarConnectionCommand(CalendarProvider Provider, string Code, string State, string RedirectUri) : IRequest;

public class CompleteCalendarConnectionCommandHandler : IRequestHandler<CompleteCalendarConnectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IOAuthStateStore _stateStore;
    private readonly ICalendarProviderClientFactory _clientFactory;
    private readonly IRefreshTokenProtector _tokenProtector;
    private readonly ISender _sender;

    public CompleteCalendarConnectionCommandHandler(
        IApplicationDbContext context,
        IUser user,
        IOAuthStateStore stateStore,
        ICalendarProviderClientFactory clientFactory,
        IRefreshTokenProtector tokenProtector,
        ISender sender)
    {
        _context = context;
        _user = user;
        _stateStore = stateStore;
        _clientFactory = clientFactory;
        _tokenProtector = tokenProtector;
        _sender = sender;
    }

    public async Task Handle(CompleteCalendarConnectionCommand request, CancellationToken cancellationToken)
    {
        var state = _stateStore.Validate(request.State);

        if (state is null || state.UserId != _user.Id || state.Provider != request.Provider)
        {
            throw new ForbiddenAccessException();
        }

        var client = _clientFactory.Get(request.Provider);
        var tokens = await client.ExchangeAuthorizationCodeAsync(request.Code, request.RedirectUri, cancellationToken);
        var email = await client.GetAccountEmailAsync(tokens.AccessToken, cancellationToken);

        var usedColours = await _context.CalendarConnections
            .Where(c => c.UserId == _user.Id)
            .Select(c => c.Colour)
            .ToListAsync(cancellationToken);

        var colour = Colour.SupportedColours.FirstOrDefault(c => !usedColours.Contains(c)) ?? Colour.Grey;

        var connection = new CalendarConnection
        {
            UserId = _user.Id!,
            Provider = request.Provider,
            AccountEmail = email,
            EncryptedRefreshToken = _tokenProtector.Protect(tokens.RefreshToken),
            Colour = colour
        };

        _context.CalendarConnections.Add(connection);

        await _context.SaveChangesAsync(cancellationToken);

        await _sender.Send(new SyncCalendarConnectionCommand(connection.Id), cancellationToken);
    }
}
