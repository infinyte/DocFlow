namespace Hospital.Domain.Domain;

public class Appointment
{
    public object recordId { get; set; }
    public object racordId { get; set; }
    public object diagnosis { get; set; }
    public object treatment { get; set; }
    public object pressicratuss { get; set; }
}

public class AppointmentId
{
    public object date { get; set; }
    public object specialty { get; set; }
    public object licenseNumber { get; set; }
}

public class BSchedule
{
    public object diagnoseL { get; set; }
    public object preserbbe { get; set; }
}

public class Departmecord
{
    public object deptId { get; set; }
    public object staff { get; set; }
    public object name { get; set; }
    public object prepertabess { get; set; }
}

public class Ditopper
{
    public object neabor { get; set; }
    public object specialty { get; set; }
    public object preserrber { get; set; }
}

public class Hospital
{
    public object poclayeemiee { get; set; }
    public object hiredate { get; set; }
    public object salay { get; set; }
}

public class Insurance
{
    public object hospitalNabel { get; set; }
    public object provides { get; set; }
    public object salary { get; set; }
}

public class InsurancePolicy
{
    public object policyNumber { get; set; }
    public object name { get; set; }
    public object coverage { get; set; }
}

public class Nourise
{
    public object durpooe { get; set; }
    public object pepartbent { get; set; }
}

public class Patient
{
    public object name { get; set; }
    public object dateOfBirth { get; set; }
    public object bloodType { get; set; }
    public object allergies { get; set; }
}

public class Schedule
{
    public object diagnoses { get; set; }
    public object duriebse { get; set; }
}
