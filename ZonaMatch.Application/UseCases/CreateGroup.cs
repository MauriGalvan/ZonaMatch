using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases;

public class CreateGroup
{
    private readonly IGroupRepository _groupRepository;

    // Inyectamos la interfaz del repositorio
    public CreateGroup(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<GroupResponse> ExecuteAsync(CreateGroupRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var nuevoGrupo = new Group
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            Participants = new List<Participant>

            {
                    new Participant
                    {
                        UsuarioId = userId,      // El ID que extrajimos del Token
                        Role = "Propietario",       // Le damos el poder total
                    }
                }
        };

        // 2. Lo enviamos al repositorio para que lo guarde
        // Al guardar el grupo, EF Core detecta la lista de Participants y los guarda también.
        await _groupRepository.AddAsync(nuevoGrupo, cancellationToken);

        // 3. Mapeamos la entidad de dominio al DTO de respuesta
        return new GroupResponse
        {
            Id = nuevoGrupo.Id,
            Name = nuevoGrupo.Name,
            CreatedAt = nuevoGrupo.CreatedAt

        };
    }
}
