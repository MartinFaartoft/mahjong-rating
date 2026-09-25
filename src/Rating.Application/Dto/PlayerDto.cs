namespace Rating.Application.Dto;

public sealed record PlayerDto(Guid Id, string DisplayName);

public sealed record CreatePlayerRequest(string DisplayName);
