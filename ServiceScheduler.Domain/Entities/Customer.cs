namespace ServiceScheduler.Domain.Entities;

public sealed class Customer
{
    private Customer()
    {
        Name = string.Empty;
        Email = string.Empty;
    }

    public Customer(Guid id, string name, string email)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        Name = Guard.NotBlank(name, nameof(name));
        Email = Guard.NotBlank(email, nameof(email));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
}
