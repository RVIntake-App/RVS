using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.API.Mappers;

/// <summary>
/// Maps a tenant's status vocabulary (<c>Spec C-8</c>) to response DTOs.
/// </summary>
public static class TenantStatusMapper
{
    /// <summary>Maps one status definition to its DTO.</summary>
    public static TenantStatusDto ToDto(this StatusDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new TenantStatusDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Color = definition.Color,
            IsFixed = definition.IsFixed,
        };
    }

    /// <summary>Maps a vocabulary to the response DTO, keeping board order and the redirects.</summary>
    public static TenantStatusesResponseDto ToResponseDto(this TenantStatusVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        return new TenantStatusesResponseDto
        {
            Statuses = [.. vocabulary.Statuses.Select(s => s.ToDto())],
            Redirects = new Dictionary<string, string>(vocabulary.Redirects),
        };
    }
}
