using System;
using System.Collections.Generic;
using System.Text;

namespace ZonaMatch.Domain.Entities;

public class Participant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Llave foránea hacia tu Grupo
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    // Llave foránea hacia la tabla de tu compañero. 
    // EF Core detectará esto automáticamente sin tocar la clase Usuario.
    public Guid UserId { get; set; }

    public required string Role { get; set; }
}
