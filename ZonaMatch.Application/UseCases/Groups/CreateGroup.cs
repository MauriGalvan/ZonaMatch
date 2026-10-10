using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.DTOs.Group;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;


namespace ZonaMatch.Application.UseCases.Groups;

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
                        UserId = userId,      // El ID que extrajimos del Token
                        Role = GroupRole.Owner,       // Le damos el poder total
                    }
                }
        };

        // 2. Lo enviamos al repositorio para que lo guarde
        // Al guardar el grupo, EF Core detecta la lista de Participants y los guarda también.
        try
        {
            await _groupRepository.AddAsync(nuevoGrupo, cancellationToken); // O tu método de guardado
        }
        catch (Exception ex)
        {
            // ¡Acá está el oro! Esto te dirá exactamente qué regla de la base de datos se rompió
            Console.WriteLine("ERROR REAL DE POSTGRES: " + ex.InnerException?.Message);
            throw;
        }

        // 3. Mapeamos la entidad de dominio al DTO de respuesta
        return new GroupResponse
        {
            Id = nuevoGrupo.Id,
            Name = nuevoGrupo.Name,
            CreatedAt = nuevoGrupo.CreatedAt

        };
    }
}
