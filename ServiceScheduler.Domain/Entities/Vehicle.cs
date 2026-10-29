namespace ServiceScheduler.Domain.Entities;

public sealed class Vehicle
{
    private Vehicle()
    {
        Vin = string.Empty;
        Make = string.Empty;
        Model = string.Empty;
    }

    public Vehicle(Guid id, Guid customerId, string vin, string make, string model)
    {
        Id = Guard.NotEmpty(id, nameof(id));
        CustomerId = Guard.NotEmpty(customerId, nameof(customerId));
        Vin = Guard.NotBlank(vin, nameof(vin));
        Make = Guard.NotBlank(make, nameof(make));
        Model = Guard.NotBlank(model, nameof(model));
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Vin { get; private set; }
    public string Make { get; private set; }
    public string Model { get; private set; }
}
