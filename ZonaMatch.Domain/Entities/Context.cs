namespace ZonaMatch.Domain.Entities
{
    // A user's analysis criteria (app.contextos table): household, criterion priorities, places, and
    // rental or purchase limits. ContextType defines two types:
    // - perfil: the user's base criteria, one per user and shared by all their analyses.
    // - analisis: a snapshot used to calculate a saved analysis; profile edits do not change it.
    // Details are normalized across the personas, puntos_interes, and contexto_criterios tables.
    public class Context
    {
        // UUIDv7: unique and time-ordered, so the primary key index stays compact
        public Guid Id { get; set; } = Guid.CreateVersion7();

        public Guid UserId { get; set; }

        // Existing type codes: perfil | analisis (ContextType).
        public string Type { get; set; } = ContextType.Profile;

        // Number of people in the household (1..8); age ranges are stored in Person records.
        public int PeopleCount { get; set; }

        // Selected pet codes (perro, gato, otras, ninguna), stored as a text array.
        public string[]? Pets { get; set; }

        // Common mobility options: a-pie, transporte-publico, vehiculo, bici.
        // Like pets, a simple list of user-selected mobility options without a catalog.
        public string[]? Mobility { get; set; }

        // User-entered monthly budget, stored verbatim (for example, "800000"); null if omitted.
        // Includes rent and expenses for renters, and expenses only for buyers.
        public string? MaximumRent { get; set; }

        // Existing operation codes: alquiler | compra (Scoring.HousingOperations).
        public string Operation { get; set; } = "alquiler";

        // Maximum purchase price in US dollars; used only when buying.
        public decimal? MaximumPurchasePriceUsd { get; set; }

        // apartment | house | ph (Scoring.HousingTypes); null means any type is acceptable.
        public string? HousingType { get; set; }

        // Desired area in square meters; prices are estimated per square meter. Null uses zone averages.
        public int? AreaM2 { get; set; }

        // Number of rooms (1 to 4, 5 = 5 or more): prices are those of that size. Null uses every size.
        public int? Rooms { get; set; }

        // Buying with a mortgage: the purchase limit is compared with the sales eligible for it ("apto crédito").
        // Always false when renting.
        public bool CreditEligible { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        // app.personas rows: one per household member, including their age range.
        public ICollection<Person> People { get; set; } = new List<Person>();

        // app.puntos_interes rows: places the user visits.
        public ICollection<ContextPlace> Places { get; set; } = new List<ContextPlace>();

        // app.contexto_criterios rows: selected criteria and their priorities (baja|media|alta).
        public ICollection<ContextCriterion> Criteria { get; set; } = new List<ContextCriterion>();

        // Create a full snapshot for a saved analysis so it remains unchanged if the profile changes.
        public Context CopyForAnalysis(DateTimeOffset timestamp) => new()
        {
            UserId = UserId,
            Type = ContextType.Analysis,
            PeopleCount = PeopleCount,
            Pets = Pets?.ToArray(),
            Mobility = Mobility?.ToArray(),
            MaximumRent = MaximumRent,
            Operation = Operation,
            MaximumPurchasePriceUsd = MaximumPurchasePriceUsd,
            HousingType = HousingType,
            AreaM2 = AreaM2,
            Rooms = Rooms,
            CreditEligible = CreditEligible,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            People = People
                .Select(person => new Person { Order = person.Order, Name = person.Name, AgeRange = person.AgeRange })
                .ToList(),
            Places = Places
                .Select(place => new ContextPlace
                {
                    Order = place.Order,
                    Name = place.Name,
                    Address = place.Address,
                    Latitude = place.Latitude,
                    Longitude = place.Longitude,
                    Type = place.Type,
                    WhoGoes = place.WhoGoes,
                    Frequency = place.Frequency,
                    Priority = place.Priority,
                    MaximumTravelTime = place.MaximumTravelTime,
                })
                .ToList(),
            Criteria = Criteria
                .Select(criterion => new ContextCriterion { CriterionId = criterion.CriterionId, Priority = criterion.Priority })
                .ToList(),
        };
    }

    // Values used by Context.Type.
    public static class ContextType
    {
        public const string Profile = "perfil";
        public const string Analysis = "analisis";

        // The investor profile: capital, credit, type, rooms and investment criteria (no household or places).
        public const string Investor = "inversor";
    }
}
