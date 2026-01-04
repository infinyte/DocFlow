using DocFlow.IMS;
using DocFlow.Integration.Models;

namespace DocFlow.Integration.Patterns;

/// <summary>
/// Pre-built patterns for common API field mapping scenarios.
/// These seed the IMS with domain knowledge to improve auto-mapping accuracy.
/// </summary>
public static class ApiMappingPatterns
{
    /// <summary>
    /// Common aviation industry field mappings.
    /// Based on patterns observed across FlightBridge, 1200Aero, and other aviation APIs.
    /// </summary>
    public static class Aviation
    {
        public static readonly PatternRule TailNumber = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(tail[_-]?(num|number|no)|aircraft[_-]?id|reg(istration)?|ac[_-]?reg)",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "TailNumber",
                ["description"] = "Aircraft tail number / registration"
            }
        };
        
        public static readonly PatternRule FlightNumber = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(flight[_-]?(num|number|no|id)|flt[_-]?(num|no)|callsign)",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "FlightNumber",
                ["description"] = "Flight number identifier"
            }
        };
        
        public static readonly PatternRule ArrivalTime = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(arr(ival)?[_-]?(time|dt|date|datetime)?|eta|ata|landing[_-]?time)",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "ArrivalDateTime",
                ["description"] = "Arrival date/time"
            }
        };
        
        public static readonly PatternRule DepartureTime = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(dep(arture)?[_-]?(time|dt|date|datetime)?|etd|atd|takeoff[_-]?time)",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "DepartureDateTime",
                ["description"] = "Departure date/time"
            }
        };
        
        public static readonly PatternRule OriginAirport = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(origin|departure|from)[_-]?(icao|iata|airport|apt)?[_-]?(code|id)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "OriginAirportCode",
                ["description"] = "Origin airport ICAO/IATA code"
            }
        };
        
        public static readonly PatternRule DestinationAirport = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(dest(ination)?|arrival|to)[_-]?(icao|iata|airport|apt)?[_-]?(code|id)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "DestinationAirportCode",
                ["description"] = "Destination airport ICAO/IATA code"
            }
        };
        
        public static readonly PatternRule PassengerCount = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(pax|passenger)[_-]?(count|num|qty|number)?|num[_-]?passengers|souls",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "PassengerCount",
                ["description"] = "Number of passengers"
            }
        };
        
        public static readonly PatternRule FuelQuantity = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(fuel)[_-]?(qty|quantity|amount|gallons|liters|lbs|weight)",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "FuelQuantity",
                ["description"] = "Fuel quantity"
            }
        };
        
        public static readonly PatternRule FboIdentifier = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(fbo|handler)[_-]?(id|code|name)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "FboId",
                ["description"] = "Fixed Base Operator identifier"
            }
        };
        
        public static readonly PatternRule ReservationId = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(res(ervation)?|booking)[_-]?(id|num|number|code)",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "ReservationId",
                ["description"] = "Reservation identifier"
            }
        };
        
        /// <summary>
        /// Get all aviation patterns
        /// </summary>
        public static IEnumerable<PatternRule> All => new[]
        {
            TailNumber, FlightNumber, ArrivalTime, DepartureTime,
            OriginAirport, DestinationAirport, PassengerCount,
            FuelQuantity, FboIdentifier, ReservationId
        };
    }
    
    /// <summary>
    /// Common date/time transformation patterns
    /// </summary>
    public static class DateTime
    {
        public static readonly TransformationRule IsoToDateTime = new()
        {
            Id = "iso-to-datetime",
            Type = TransformationType.TypeConversion,
            Expression = "DateTime.Parse(source, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)"
        };
        
        public static readonly TransformationRule UnixSecondsToDateTime = new()
        {
            Id = "unix-seconds-to-datetime",
            Type = TransformationType.TypeConversion,
            Expression = "DateTimeOffset.FromUnixTimeSeconds((long)source).UtcDateTime"
        };
        
        public static readonly TransformationRule UnixMillisToDateTime = new()
        {
            Id = "unix-millis-to-datetime",
            Type = TransformationType.TypeConversion,
            Expression = "DateTimeOffset.FromUnixTimeMilliseconds((long)source).UtcDateTime"
        };
        
        public static readonly TransformationRule DateTimeToIso = new()
        {
            Id = "datetime-to-iso",
            Type = TransformationType.TypeConversion,
            Expression = "((DateTime)source).ToString(\"O\")"
        };
        
        public static readonly TransformationRule DateTimeToUnixSeconds = new()
        {
            Id = "datetime-to-unix-seconds",
            Type = TransformationType.TypeConversion,
            Expression = "new DateTimeOffset((DateTime)source).ToUnixTimeSeconds()"
        };
        
        /// <summary>
        /// Common date format conversions
        /// </summary>
        public static readonly TransformationRule UsDateToIso = new()
        {
            Id = "us-date-to-iso",
            Type = TransformationType.Format,
            Expression = "DateTime.ParseExact(source, \"MM/dd/yyyy\", CultureInfo.InvariantCulture).ToString(\"yyyy-MM-dd\")"
        };
        
        public static IEnumerable<TransformationRule> All => new[]
        {
            IsoToDateTime, UnixSecondsToDateTime, UnixMillisToDateTime,
            DateTimeToIso, DateTimeToUnixSeconds, UsDateToIso
        };
    }
    
    /// <summary>
    /// Common identifier patterns
    /// </summary>
    public static class Identifiers
    {
        public static readonly PatternRule PrimaryKey = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)^(id|[a-z]+[_-]?id|pk|primary[_-]?key|guid|uuid)$",
            Parameters = new Dictionary<string, object>
            {
                ["semantics"] = "Identity",
                ["description"] = "Primary key identifier"
            }
        };
        
        public static readonly PatternRule ForeignKey = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)^([a-z]+)[_-]?id$",
            Parameters = new Dictionary<string, object>
            {
                ["semantics"] = "Navigation",
                ["description"] = "Foreign key reference"
            }
        };
        
        public static readonly PatternRule ExternalReference = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(external|ext|ref|reference|source)[_-]?(id|key|num|code)",
            Parameters = new Dictionary<string, object>
            {
                ["description"] = "External system reference"
            }
        };
        
        public static readonly PatternRule CorrelationId = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(correlation|tracking|trace|request)[_-]?id",
            Parameters = new Dictionary<string, object>
            {
                ["description"] = "Correlation/tracking identifier"
            }
        };
        
        public static IEnumerable<PatternRule> All => new[]
        {
            PrimaryKey, ForeignKey, ExternalReference, CorrelationId
        };
    }
    
    /// <summary>
    /// Common contact/person patterns
    /// </summary>
    public static class Contact
    {
        public static readonly PatternRule Email = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(email|e[_-]?mail)[_-]?(address)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "EmailAddress",
                ["validation"] = @"^[^@]+@[^@]+\.[^@]+$"
            }
        };
        
        public static readonly PatternRule Phone = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(phone|tel(ephone)?|mobile|cell)[_-]?(num|number)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "PhoneNumber"
            }
        };
        
        public static readonly PatternRule FirstName = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(first|given)[_-]?name|fname",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "FirstName"
            }
        };
        
        public static readonly PatternRule LastName = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(last|family|sur)[_-]?name|lname",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "LastName"
            }
        };
        
        public static IEnumerable<PatternRule> All => new[]
        {
            Email, Phone, FirstName, LastName
        };
    }
    
    /// <summary>
    /// Common audit/timestamp patterns
    /// </summary>
    public static class Audit
    {
        public static readonly PatternRule CreatedAt = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(created|create|inserted|insert)[_-]?(at|on|date|time|datetime|timestamp)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "CreatedAt",
                ["semantics"] = "Audit"
            }
        };
        
        public static readonly PatternRule UpdatedAt = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(updated|update|modified|modify|changed)[_-]?(at|on|date|time|datetime|timestamp)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "UpdatedAt",
                ["semantics"] = "Audit"
            }
        };
        
        public static readonly PatternRule CreatedBy = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(created|inserted)[_-]?by[_-]?(user|id)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "CreatedBy",
                ["semantics"] = "Audit"
            }
        };
        
        public static readonly PatternRule UpdatedBy = new()
        {
            Type = PatternRuleType.NamePattern,
            Condition = @"(?i)(updated|modified|changed)[_-]?by[_-]?(user|id)?",
            Parameters = new Dictionary<string, object>
            {
                ["targetField"] = "UpdatedBy",
                ["semantics"] = "Audit"
            }
        };
        
        public static IEnumerable<PatternRule> All => new[]
        {
            CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
        };
    }
    
    /// <summary>
    /// Get all built-in patterns
    /// </summary>
    public static IEnumerable<PatternRule> GetAllPatterns()
    {
        foreach (var pattern in Aviation.All) yield return pattern;
        foreach (var pattern in Identifiers.All) yield return pattern;
        foreach (var pattern in Contact.All) yield return pattern;
        foreach (var pattern in Audit.All) yield return pattern;
    }
    
    /// <summary>
    /// Get all built-in transformations
    /// </summary>
    public static IEnumerable<TransformationRule> GetAllTransformations()
    {
        return DateTime.All;
    }
}
