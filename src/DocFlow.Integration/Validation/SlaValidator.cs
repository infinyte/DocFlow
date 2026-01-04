using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DocFlow.Integration.Validation;

/// <summary>
/// Validates SLA compliance for API integrations.
/// 
/// Inspired by the 1200 Aero discovery where flight data was 2-5 hours stale
/// instead of the contracted 30-second freshness requirement.
/// </summary>
public sealed class SlaValidator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlaValidator>? _logger;
    
    public SlaValidator(HttpClient? httpClient = null, ILogger<SlaValidator>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;
    }
    
    /// <summary>
    /// Validate data freshness against SLA requirements.
    /// Samples the endpoint multiple times and analyzes data timestamps.
    /// </summary>
    public async Task<SlaValidationReport> ValidateDataFreshnessAsync(
        SlaValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "Starting SLA validation for {Endpoint} with {SampleCount} samples, expected max age: {MaxAge}",
            request.EndpointUrl, request.SampleCount, request.ExpectedMaxAge);
        
        var samples = new List<DataFreshnessSample>();
        var errors = new List<string>();
        
        for (int i = 0; i < request.SampleCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            try
            {
                var sample = await CollectSampleAsync(request, cancellationToken);
                samples.Add(sample);
                
                _logger?.LogDebug(
                    "Sample {Index}: Data age = {Age}, Response time = {ResponseTime}ms",
                    i + 1, sample.DataAge, sample.ResponseTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                errors.Add($"Sample {i + 1}: {ex.Message}");
                _logger?.LogWarning(ex, "Failed to collect sample {Index}", i + 1);
            }
            
            // Wait between samples
            if (i < request.SampleCount - 1 && request.SampleInterval > TimeSpan.Zero)
            {
                await Task.Delay(request.SampleInterval, cancellationToken);
            }
        }
        
        var report = AnalyzeSamples(samples, request, errors);
        
        _logger?.LogInformation(
            "SLA validation complete. Verdict: {Verdict}, Compliance: {Compliance:P1}",
            report.Verdict, report.CompliancePercentage / 100);
        
        return report;
    }
    
    /// <summary>
    /// Quick check - single sample to verify endpoint is returning fresh data
    /// </summary>
    public async Task<DataFreshnessSample> QuickCheckAsync(
        SlaValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CollectSampleAsync(request, cancellationToken);
    }
    
    private async Task<DataFreshnessSample> CollectSampleAsync(
        SlaValidationRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var sampledAt = DateTime.UtcNow;
        
        // Make the request
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, request.EndpointUrl);
        
        // Add auth headers if provided
        if (request.AuthHeaders != null)
        {
            foreach (var (key, value) in request.AuthHeaders)
            {
                httpRequest.Headers.TryAddWithoutValidation(key, value);
            }
        }
        
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        stopwatch.Stop();
        
        // Extract data timestamp from response
        var dataTimestamp = ExtractDataTimestamp(content, request.TimestampJsonPath);
        
        return new DataFreshnessSample
        {
            SampledAt = sampledAt,
            DataTimestamp = dataTimestamp,
            DataAge = dataTimestamp.HasValue ? sampledAt - dataTimestamp.Value : null,
            ResponseTime = stopwatch.Elapsed,
            HttpStatusCode = (int)response.StatusCode,
            Success = response.IsSuccessStatusCode
        };
    }
    
    private DateTime? ExtractDataTimestamp(string content, string? jsonPath)
    {
        if (string.IsNullOrEmpty(content))
            return null;
            
        try
        {
            using var doc = JsonDocument.Parse(content);
            
            // If a specific JSON path is provided, use it
            if (!string.IsNullOrEmpty(jsonPath))
            {
                var element = NavigateJsonPath(doc.RootElement, jsonPath);
                if (element.HasValue)
                {
                    return ParseTimestamp(element.Value);
                }
            }
            
            // Otherwise, search for common timestamp field names
            var timestampFields = new[]
            {
                "timestamp", "updated_at", "updatedAt", "lastUpdated", 
                "last_updated", "modified", "modifiedAt", "modified_at",
                "data_timestamp", "dataTimestamp", "asOf", "as_of"
            };
            
            var timestamp = FindTimestampField(doc.RootElement, timestampFields);
            if (timestamp.HasValue)
                return timestamp;
                
            // Check nested data object
            if (doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                timestamp = FindTimestampField(dataElement, timestampFields);
                if (timestamp.HasValue)
                    return timestamp;
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse JSON response for timestamp extraction");
        }
        
        return null;
    }
    
    private JsonElement? NavigateJsonPath(JsonElement root, string path)
    {
        // Simple JSON path navigation ($.data.timestamp format)
        var parts = path.TrimStart('$', '.').Split('.');
        var current = root;
        
        foreach (var part in parts)
        {
            // Handle array indexing [0]
            var match = Regex.Match(part, @"^(\w+)\[(\d+)\]$");
            if (match.Success)
            {
                var propName = match.Groups[1].Value;
                var index = int.Parse(match.Groups[2].Value);
                
                if (!current.TryGetProperty(propName, out var arrayElement))
                    return null;
                    
                if (arrayElement.ValueKind != JsonValueKind.Array || 
                    index >= arrayElement.GetArrayLength())
                    return null;
                    
                current = arrayElement[index];
            }
            else
            {
                if (!current.TryGetProperty(part, out var nextElement))
                    return null;
                current = nextElement;
            }
        }
        
        return current;
    }
    
    private DateTime? FindTimestampField(JsonElement element, string[] fieldNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
            
        foreach (var fieldName in fieldNames)
        {
            if (element.TryGetProperty(fieldName, out var value))
            {
                var timestamp = ParseTimestamp(value);
                if (timestamp.HasValue)
                    return timestamp;
            }
        }
        
        return null;
    }
    
    private DateTime? ParseTimestamp(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String when DateTime.TryParse(
                element.GetString(), out var dt) => dt.ToUniversalTime(),
                
            JsonValueKind.Number when element.TryGetInt64(out var unix) => 
                unix > 1_000_000_000_000 
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unix).UtcDateTime
                    : DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime,
                    
            _ => null
        };
    }
    
    private SlaValidationReport AnalyzeSamples(
        List<DataFreshnessSample> samples,
        SlaValidationRequest request,
        List<string> errors)
    {
        var validSamples = samples.Where(s => s.Success && s.DataAge.HasValue).ToList();
        
        if (validSamples.Count == 0)
        {
            return new SlaValidationReport
            {
                EndpointUrl = request.EndpointUrl,
                ExpectedMaxAge = request.ExpectedMaxAge,
                Verdict = SlaVerdict.Unknown,
                TotalSamples = samples.Count,
                ValidSamples = 0,
                Samples = samples,
                Errors = errors,
                Notes = ["No valid samples with extractable timestamps"]
            };
        }
        
        var dataAges = validSamples.Select(s => s.DataAge!.Value).ToList();
        var samplesOverSla = validSamples.Count(s => s.DataAge > request.ExpectedMaxAge);
        var compliancePercentage = (double)(validSamples.Count - samplesOverSla) / validSamples.Count * 100;
        
        // Determine verdict
        var avgAge = TimeSpan.FromTicks((long)dataAges.Average(a => a.Ticks));
        var maxAge = dataAges.Max();
        
        SlaVerdict verdict;
        if (compliancePercentage >= 99)
        {
            verdict = SlaVerdict.Compliant;
        }
        else if (compliancePercentage >= 95)
        {
            verdict = SlaVerdict.MarginallyCompliant;
        }
        else if (avgAge > request.ExpectedMaxAge * 10)
        {
            // Data is 10x older than expected - severe violation (like 1200 Aero!)
            verdict = SlaVerdict.SevereViolation;
        }
        else
        {
            verdict = SlaVerdict.MinorViolation;
        }
        
        var notes = new List<string>();
        
        if (verdict == SlaVerdict.SevereViolation)
        {
            notes.Add($"⚠️ SEVERE: Average data age ({avgAge}) is significantly higher than SLA ({request.ExpectedMaxAge})");
            notes.Add($"This may indicate stale data caching or upstream data delays.");
        }
        
        if (maxAge > request.ExpectedMaxAge * 2)
        {
            notes.Add($"Maximum observed data age ({maxAge}) is more than 2x the SLA requirement.");
        }
        
        return new SlaValidationReport
        {
            EndpointUrl = request.EndpointUrl,
            ExpectedMaxAge = request.ExpectedMaxAge,
            ActualAverageAge = avgAge,
            ActualMaxAge = maxAge,
            ActualMinAge = dataAges.Min(),
            SamplesOverSla = samplesOverSla,
            TotalSamples = samples.Count,
            ValidSamples = validSamples.Count,
            CompliancePercentage = compliancePercentage,
            Verdict = verdict,
            Samples = samples,
            Errors = errors,
            Notes = notes,
            
            // Response time stats
            AverageResponseTime = TimeSpan.FromMilliseconds(
                validSamples.Average(s => s.ResponseTime.TotalMilliseconds)),
            MaxResponseTime = validSamples.Max(s => s.ResponseTime),
            MinResponseTime = validSamples.Min(s => s.ResponseTime)
        };
    }
}

/// <summary>
/// Request for SLA validation
/// </summary>
public sealed class SlaValidationRequest
{
    /// <summary>
    /// The endpoint URL to validate
    /// </summary>
    public required string EndpointUrl { get; init; }
    
    /// <summary>
    /// Expected maximum data age per SLA (e.g., 30 seconds)
    /// </summary>
    public required TimeSpan ExpectedMaxAge { get; init; }
    
    /// <summary>
    /// Number of samples to collect
    /// </summary>
    public int SampleCount { get; init; } = 10;
    
    /// <summary>
    /// Time to wait between samples
    /// </summary>
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// JSON path to the timestamp field (e.g., "$.data.updated_at")
    /// If not specified, common timestamp fields will be searched
    /// </summary>
    public string? TimestampJsonPath { get; init; }
    
    /// <summary>
    /// Authentication headers to include
    /// </summary>
    public Dictionary<string, string>? AuthHeaders { get; init; }
}

/// <summary>
/// Individual sample result
/// </summary>
public sealed class DataFreshnessSample
{
    /// <summary>
    /// When this sample was taken
    /// </summary>
    public DateTime SampledAt { get; init; }
    
    /// <summary>
    /// Timestamp extracted from the data
    /// </summary>
    public DateTime? DataTimestamp { get; init; }
    
    /// <summary>
    /// Age of the data at sample time
    /// </summary>
    public TimeSpan? DataAge { get; init; }
    
    /// <summary>
    /// How long the HTTP request took
    /// </summary>
    public TimeSpan ResponseTime { get; init; }
    
    /// <summary>
    /// HTTP status code
    /// </summary>
    public int HttpStatusCode { get; init; }
    
    /// <summary>
    /// Was the request successful?
    /// </summary>
    public bool Success { get; init; }
}

/// <summary>
/// Complete SLA validation report
/// </summary>
public sealed class SlaValidationReport
{
    public required string EndpointUrl { get; init; }
    public TimeSpan ExpectedMaxAge { get; init; }
    
    // Data freshness stats
    public TimeSpan? ActualAverageAge { get; init; }
    public TimeSpan? ActualMaxAge { get; init; }
    public TimeSpan? ActualMinAge { get; init; }
    
    // Compliance stats
    public int SamplesOverSla { get; init; }
    public int TotalSamples { get; init; }
    public int ValidSamples { get; init; }
    public double CompliancePercentage { get; init; }
    
    // Verdict
    public SlaVerdict Verdict { get; init; }
    
    // Response time stats
    public TimeSpan? AverageResponseTime { get; init; }
    public TimeSpan? MaxResponseTime { get; init; }
    public TimeSpan? MinResponseTime { get; init; }
    
    // Details
    public List<DataFreshnessSample> Samples { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public List<string> Notes { get; init; } = [];
    
    /// <summary>
    /// Generate a summary for display
    /// </summary>
    public string GetSummary()
    {
        var emoji = Verdict switch
        {
            SlaVerdict.Compliant => "✅",
            SlaVerdict.MarginallyCompliant => "⚠️",
            SlaVerdict.MinorViolation => "❌",
            SlaVerdict.SevereViolation => "🚨",
            _ => "❓"
        };
        
        return $"""
            {emoji} SLA Validation: {Verdict}
            
            Endpoint: {EndpointUrl}
            Expected Max Age: {ExpectedMaxAge}
            Actual Average Age: {ActualAverageAge?.ToString() ?? "N/A"}
            Actual Max Age: {ActualMaxAge?.ToString() ?? "N/A"}
            
            Compliance: {CompliancePercentage:F1}% ({ValidSamples - SamplesOverSla}/{ValidSamples} samples within SLA)
            
            Response Time: avg {AverageResponseTime?.TotalMilliseconds:F0}ms, max {MaxResponseTime?.TotalMilliseconds:F0}ms
            """;
    }
}

/// <summary>
/// SLA compliance verdict
/// </summary>
public enum SlaVerdict
{
    /// <summary>Could not determine compliance</summary>
    Unknown,
    
    /// <summary>All samples within SLA requirements</summary>
    Compliant,
    
    /// <summary>Minor violations but generally compliant (95%+)</summary>
    MarginallyCompliant,
    
    /// <summary>Some samples exceed SLA</summary>
    MinorViolation,
    
    /// <summary>Data is significantly older than SLA (like 1200 Aero!)</summary>
    SevereViolation
}
