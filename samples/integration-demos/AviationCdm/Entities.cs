namespace AviationCdm;

/// <summary>
/// Core flight operation entity - represents a single leg of a trip.
/// Maps to external "Flight" entities from partner APIs.
/// </summary>
public class FlightLeg
{
    public Guid FlightLegId { get; set; }
    public string TailNumber { get; set; } = "";
    public string OriginAirportCode { get; set; } = "";  // ICAO code
    public string DestinationAirportCode { get; set; } = "";  // ICAO code
    public DateTime ScheduledDeparture { get; set; }
    public DateTime ScheduledArrival { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public DateTime? ActualArrival { get; set; }
    public FlightStatus Status { get; set; }
    public int PassengerCount { get; set; }
    public List<CrewAssignment> Crew { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum FlightStatus
{
    Scheduled,
    Boarding,
    Departed,
    EnRoute,
    Arrived,
    Cancelled,
    Diverted
}

/// <summary>
/// Aircraft registry and specifications.
/// </summary>
public class Aircraft
{
    public string TailNumber { get; set; } = "";  // FAA N-number
    public string AircraftType { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public int MaxPassengers { get; set; }
    public string HomeBaseIcao { get; set; } = "";
    public AircraftStatus MaintenanceStatus { get; set; }
}

public enum AircraftStatus
{
    Operational,
    InMaintenance,
    AOG  // Aircraft on Ground
}

/// <summary>
/// Fixed Base Operator - ground services provider at airports.
/// Maps to external "FBO" entities.
/// </summary>
public class FixedBaseOperator
{
    public Guid FboId { get; set; }
    public string IcaoCode { get; set; } = "";
    public string FacilityName { get; set; } = "";
    public Address PhysicalAddress { get; set; } = null!;
    public List<FuelType> AvailableFuels { get; set; } = [];
    public List<string> Services { get; set; } = [];
}

public enum FuelType
{
    JetA,
    Avgas100LL,
    Avgas100
}

/// <summary>
/// Physical address - Value Object.
/// </summary>
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
}

/// <summary>
/// Fuel pricing quote from an FBO.
/// Maps to external "FuelQuote" entities.
/// </summary>
public class FuelPricing
{
    public Guid QuoteId { get; set; }
    public Guid FboId { get; set; }
    public FuelType FuelType { get; set; }
    public decimal PricePerGallon { get; set; }
    public DateTime ValidUntil { get; set; }
    public int MinimumGallons { get; set; }
}

/// <summary>
/// Crew assignment to a flight.
/// </summary>
public class CrewAssignment
{
    public Guid CrewMemberId { get; set; }
    public string CrewMemberName { get; set; } = "";
    public CrewRole Role { get; set; }
}

public enum CrewRole
{
    Captain,       // PIC
    FirstOfficer,  // SIC
    FlightAttendant,
    Dispatcher
}

/// <summary>
/// Trip reservation - passenger booking on a flight.
/// Maps to external "Reservation" entities.
/// </summary>
public class TripReservation
{
    public Guid ReservationId { get; set; }
    public Guid FlightLegId { get; set; }
    public Guid CustomerId { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PassengerInfo> Passengers { get; set; } = [];
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled
}

/// <summary>
/// Passenger information for weight & balance calculations.
/// </summary>
public class PassengerInfo
{
    public string FullName { get; set; } = "";
    public decimal Weight { get; set; }  // pounds, for W&B
    public decimal BaggageWeight { get; set; }  // pounds
}

/// <summary>
/// Geographic position with altitude (for flight tracking).
/// </summary>
public class Position
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int AltitudeFt { get; set; }
    public int GroundSpeedKts { get; set; }
    public int HeadingDeg { get; set; }
    public DateTime Timestamp { get; set; }
}
