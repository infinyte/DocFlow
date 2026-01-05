# SLA Validation Testing Guide

This guide explains how to test the `docflow integrate sla` command for validating API data freshness.

## Quick Start

```bash
# Test with a public time API (should pass with 5s SLA)
docflow integrate sla "https://worldtimeapi.org/api/timezone/America/New_York" \
  --expected 5s --samples 5 --timestamp-path "$.datetime" -v

# Test with a stricter SLA (may fail)
docflow integrate sla "https://worldtimeapi.org/api/timezone/UTC" \
  --expected 100ms --samples 3 --interval 2s --timestamp-path "$.datetime" -v
```

## Command Options

| Option | Description | Example |
|--------|-------------|---------|
| `<url>` | Endpoint URL to validate (required) | `https://api.example.com/data` |
| `--expected` | Max acceptable data age (required) | `30s`, `5m`, `1h`, `500ms` |
| `--samples` | Number of samples to collect (default: 10) | `--samples 5` |
| `--interval` | Time between samples (default: 5s) | `--interval 2s` |
| `--timestamp-path` | JSON path to timestamp field | `--timestamp-path "$.data.updated_at"` |
| `--header` | Add HTTP header (repeatable) | `--header "Authorization=Bearer xxx"` |
| `-o, --output` | Save report as JSON | `-o report.json` |
| `-v, --verbose` | Show individual samples | `-v` |

## Duration Format

The `--expected` and `--interval` options accept durations in these formats:
- `ms` - milliseconds (e.g., `500ms`)
- `s` - seconds (e.g., `30s`)
- `m` - minutes (e.g., `5m`)
- `h` - hours (e.g., `1h`)

## Public APIs for Testing

### 1. WorldTimeAPI (Recommended for testing)

Returns current time with ISO timestamp:

```bash
docflow integrate sla "https://worldtimeapi.org/api/timezone/UTC" \
  --expected 5s --samples 5 --timestamp-path "$.datetime" -v
```

Response includes:
```json
{
  "datetime": "2025-01-04T19:30:45.123456+00:00",
  "utc_datetime": "2025-01-04T19:30:45.123456+00:00"
}
```

### 2. JSONPlaceholder (Static data - will fail freshness checks)

Returns static post data (no real timestamps):

```bash
docflow integrate sla "https://jsonplaceholder.typicode.com/posts/1" \
  --expected 1s --samples 3 -v
```

This will show "UNKNOWN" verdict since there's no extractable timestamp.

### 3. GitHub API (Rate limited)

Returns repository data with timestamps:

```bash
docflow integrate sla "https://api.github.com/repos/dotnet/runtime" \
  --expected 1h --samples 3 --timestamp-path "$.updated_at" -v
```

**Note:** GitHub rate limits unauthenticated requests.

## Testing with Authentication

For APIs requiring authentication:

```bash
docflow integrate sla "https://api.example.com/data" \
  --expected 30s \
  --header "Authorization=Bearer YOUR_TOKEN" \
  --header "X-API-Key=YOUR_KEY" \
  -v
```

## Understanding the Output

### Verdict Meanings

| Verdict | Description |
|---------|-------------|
| COMPLIANT | All samples within SLA (99%+ compliance) |
| MARGINALLY COMPLIANT | Minor violations (95-99% compliance) |
| MINOR VIOLATION | Some samples exceed SLA (<95% compliance) |
| SEVERE VIOLATION | Data is 10x+ older than SLA (like the 1200 Aero incident) |
| UNKNOWN | Could not extract timestamps |

### Sample Output

```
SLA Validation: https://api.example.com/v1/flights/status
Expected Max Age: 00:00:30

Collecting 10 samples... [==========] 100%

+---------------Results---------------+
| !!! SEVERE VIOLATION                |
|                                     |
| Expected Max Age:    00:00:30       |
| Actual Average Age:  02:34:17       |
| Actual Max Age:      04:12:33       |
| Actual Min Age:      01:45:22       |
|                                     |
| Compliance: 0% (0/10 within SLA)    |
+-------------------------------------+

Note: SEVERE: Average data age is significantly higher than SLA
Note: This may indicate stale data caching or upstream data delays.

+--------------Samples----------------+
| #  | Sampled At | Data Age | Status |
|----|------------|----------|--------|
| 1  | 18:30:00   | 02:15:33 | FAIL   |
| 2  | 18:30:05   | 02:20:41 | FAIL   |
| ...                                 |
+-------------------------------------+

Response Time: avg 245ms, max 512ms, min 89ms
```

## Simulating the 1200 Aero Scenario

The "1200 Aero discovery" refers to finding that an API returning flight data
was actually serving cached data 2-5 hours old, when the SLA promised 30-second
freshness.

To simulate this scenario for testing:

1. Set up a mock endpoint that returns stale timestamps
2. Or test against a known slow-updating API:

```bash
# Test with a very strict SLA that most APIs won't meet
docflow integrate sla "https://worldtimeapi.org/api/timezone/UTC" \
  --expected 100ms --samples 10 --interval 1s \
  --timestamp-path "$.datetime" -v
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Compliant or Marginally Compliant |
| 1 | Minor Violation or Unknown |
| 2 | Severe Violation |

Use exit codes in CI/CD pipelines:

```bash
docflow integrate sla "$API_URL" --expected 30s -q || echo "SLA VIOLATION!"
```

## Saving Reports

Save detailed reports for analysis:

```bash
docflow integrate sla "https://api.example.com/data" \
  --expected 30s \
  --samples 20 \
  -o sla-report-$(date +%Y%m%d).json
```

The JSON report includes:
- All sample data with timestamps
- Response time statistics
- Compliance percentages
- Verdict and notes
