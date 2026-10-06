namespace ZonaMatch.Application.Interfaces
{
    public interface IPasswordHasher
    {
        // Returns a self-contained, salted hash (algorithm, iterations and salt are embedded in the string)
        string Hash(string password);

        // hash == null means "the user does not exist": the check still costs the same and returns false,
        // so response times do not reveal which emails are registered
        bool Verify(string? hash, string password);
    }
}
