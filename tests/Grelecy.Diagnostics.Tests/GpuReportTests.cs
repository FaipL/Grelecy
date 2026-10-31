// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text.Json;
using Grelecy.Diagnostics;

internal static class GpuReportTests
{
    internal static IReadOnlyList<(string Name, Action Body)> All { get; } =
    [
        ("GPU reports use a separate, culture-independent schema and UTC time", () =>
        {
            var report = Sample() with { TestedAt = new DateTimeOffset(2026, 10, 30, 18, 0, 0, TimeSpan.FromHours(2)) };
            var expected = GpuTestReportFormatter.ToJson(report, new());
            using var document = JsonDocument.Parse(expected);
            var root = document.RootElement;
            Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Equal("automatic_gpu_tests", root.GetProperty("reportType").GetString());
            Equal(GpuTestProtocol.Version, root.GetProperty("testVersion").GetInt32());
            Equal("2026-10-30T16:00:00.0000000+00:00", root.GetProperty("testedAt").GetString());
            Equal(ReportFormatter.ApplicationVersion, root.GetProperty("application").GetProperty("version").GetString());
            Check(!root.TryGetProperty("windows", out _) && !root.TryGetProperty("observation", out _), "schemas must stay separate");
            var previous = CultureInfo.CurrentCulture;
            try
            {
                foreach (var language in LanguageCatalog.All)
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(language.Code);
                    Equal(expected, GpuTestReportFormatter.ToJson(report, new()));
                }
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }),
        ("GPU duration exports preserve sample order and calculate the actual median", () =>
        {
            using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(Sample(), new()));
            var timing = document.RootElement.GetProperty("timing");
            Equal("passed", timing.GetProperty("status").GetString());
            Equal(GpuTestProtocol.Scenario, timing.GetProperty("scenario").GetString());
            var measurement = timing.GetProperty("measurements");
            Equal("ms", measurement.GetProperty("unit").GetString());
            Equal("gpu_timestamp_query", measurement.GetProperty("clock").GetString());
            Equal(5, measurement.GetProperty("sampleCount").GetInt32());
            Equal(7.0, measurement.GetProperty("samples")[0].GetDouble());
            Equal(1.0, measurement.GetProperty("minimum").GetDouble());
            Equal(3.0, measurement.GetProperty("median").GetDouble());
            Equal(11.0, measurement.GetProperty("maximum").GetDouble());
        }),
        ("GPU exports never expose session identifiers or default PCI IDs", () =>
        {
            foreach (var format in Formats(Sample(), new()))
            {
                Contains("GTX 1060", format);
                DoesNotContain("private-session-luid", format);
                DoesNotContain("sessionId", format);
                DoesNotContain("SessionId", format);
                DoesNotContain("87654321", format);
                DoesNotContain("10DE", format);
                DoesNotContain("1C03", format);
            }
            using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(Sample(), new()));
            var graphics = document.RootElement.GetProperty("graphics");
            Check(!graphics.TryGetProperty("hardware", out _) && !graphics.TryGetProperty("index", out _), "private or optional properties must be absent");
        }),
        ("GPU privacy selection removes names, driver versions, memory and CUDA device details", () =>
        {
            foreach (var format in Formats(Sample(), new(false, true)))
            {
                foreach (var value in new[] { "GTX 1060", "31.0.15.4567", "8589934592", "8192", "10DE", "1C03", "12040", "6.1" })
                    DoesNotContain(value, format);
            }
            using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(Sample(), new(false, true)));
            var root = document.RootElement;
            Check(!root.TryGetProperty("graphics", out _), "graphics must be absent");
            Check(!root.GetProperty("cuda").TryGetProperty("driverApiVersion", out _), "driver choice applies to CUDA metadata too");
            Check(!root.GetProperty("cuda").TryGetProperty("computeCapability", out _), "CUDA device metadata must be absent");
            Check(root.GetProperty("timing").TryGetProperty("measurements", out _), "anonymized test results remain shareable");
        }),
        ("Explicit PCI export includes model IDs only and rejects out-of-range IDs", () =>
        {
            using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(Sample(), new(true, true)));
            var hardware = document.RootElement.GetProperty("graphics").GetProperty("hardware");
            Equal("10DE", hardware.GetProperty("vendorId").GetString());
            Equal("1C03", hardware.GetProperty("deviceId").GetString());
            Equal(2, hardware.EnumerateObject().Count());
            foreach (var adapter in new[]
            {
                Sample().Adapter! with { VendorId = uint.MaxValue },
                Sample().Adapter! with { DeviceId = 0 },
                Sample().Adapter! with { IsSoftware = true }
            })
            {
                using var invalid = JsonDocument.Parse(GpuTestReportFormatter.ToJson(Sample() with { Adapter = adapter }, new(true, true)));
                Check(!invalid.RootElement.GetProperty("graphics").TryGetProperty("hardware", out _), "invalid or software PCI IDs must be omitted");
            }
        }),
        ("Invalid or incomplete GPU measurements cannot become scores", () =>
        {
            var sample = Sample();
            var invalid = new List<GpuTestReport>
            {
                sample with { Adapter = null },
                sample with { Direct3D11 = sample.Direct3D11 with { Status = GpuTestStatus.Inconclusive } },
                sample with { Direct3D11 = sample.Direct3D11 with { FeatureLevel = "unverified" } },
                sample with { ComputeShader = sample.ComputeShader with { Status = GpuTestStatus.NotRun } },
                sample with { Timing = sample.Timing with { Scenario = "unverified-workload" } },
                sample with { Timing = sample.Timing with { ElementCount = 4 } },
                sample with { Timing = sample.Timing with { Iterations = 1 } },
                sample with { Timing = sample.Timing with { WarmupCount = 0 } },
                sample with { Timing = sample.Timing with { SamplesMilliseconds = [] } },
                sample with { Timing = sample.Timing with { SamplesMilliseconds = [1, 2, 3, 4] } },
                sample with { Timing = sample.Timing with { SamplesMilliseconds = [1, 2, 3, 4, 5, 6] } }
            };
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, -1 })
                invalid.Add(sample with { Timing = sample.Timing with { SamplesMilliseconds = [1, 2, value, 4, 5] } });
            foreach (var report in invalid)
            {
                using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(report, new()));
                var timing = document.RootElement.GetProperty("timing");
                Equal("inconclusive", timing.GetProperty("status").GetString());
                Equal("measurement_invalid", timing.GetProperty("code").GetString());
                Check(!timing.TryGetProperty("measurements", out _), "invalid values must not be serialized");
                var plain = GpuTestReportFormatter.ToPlainText(report, UiLanguage.English, new());
                DoesNotContain("Median GPU duration", plain);
                Contains("No valid GPU duration", plain);
            }
            foreach (var state in new[] { GpuTestStatus.NotRun, GpuTestStatus.Inconclusive, GpuTestStatus.Cancelled, GpuTestStatus.TimedOut })
            {
                var report = sample with { Timing = sample.Timing with { Status = state } };
                using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(report, new()));
                Check(!document.RootElement.GetProperty("timing").TryGetProperty("measurements", out _), "non-passed states cannot export metrics");
            }
        }),
        ("Software WARP results are labelled and never exported as GPU performance", () =>
        {
            var sample = Sample();
            var report = sample with { Adapter = sample.Adapter! with { IsSoftware = true, Name = "WARP" } };
            using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(report, new(false, false)));
            Equal("software", document.RootElement.GetProperty("execution").GetString());
            var timing = document.RootElement.GetProperty("timing");
            Equal("not_run", timing.GetProperty("status").GetString());
            Equal("software_timing_omitted", timing.GetProperty("code").GetString());
            Check(!timing.TryGetProperty("measurements", out _), "software timing must not appear");
            foreach (var text in new[]
            {
                GpuTestReportFormatter.ToPlainText(report, UiLanguage.English, new(false, false)),
                GpuTestReportFormatter.ToMarkdown(report, UiLanguage.English, new(false, false))
            })
            {
                Contains("Software execution", text);
                DoesNotContain("Median GPU duration", text);
            }
        }),
        ("Native diagnostic strings cannot leak exceptions, paths or unknown capability claims", () =>
        {
            var sample = Sample();
            const string secret = @"C:\Users\private-account\native-error";
            var report = sample with
            {
                Adapter = sample.Adapter! with { DriverVersion = secret },
                Direct3D11 = new((GpuTestStatus)987, secret, secret),
                ComputeShader = new(GpuTestStatus.Inconclusive, "private_account"),
                Cuda = new(GpuTestStatus.Passed, secret, int.MaxValue, -1, 99),
                Timing = sample.Timing with { Code = secret, Scenario = secret }
            };
            foreach (var format in Formats(report, new(true, true)))
            {
                DoesNotContain("private", format);
                DoesNotContain("native-error", format);
            }
            Contains("information_unavailable", GpuTestReportFormatter.ToPlainText(report, UiLanguage.English, new()));
            using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(report, new()));
            Equal("inconclusive", document.RootElement.GetProperty("direct3D11").GetProperty("status").GetString());
            Check(!document.RootElement.GetProperty("direct3D11").TryGetProperty("featureLevel", out _), "unknown feature levels must be absent");
            Check(!document.RootElement.GetProperty("cuda").TryGetProperty("computeCapability", out _), "invalid CUDA capability must be absent");
            Check(!document.RootElement.GetProperty("cuda").TryGetProperty("driverApiVersion", out _), "invalid CUDA version must be absent");
        }),
        ("GPU capability values distinguish CUDA driver API version from toolkit installation", () =>
        {
            using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(Sample(), new()));
            var cuda = document.RootElement.GetProperty("cuda");
            Equal(12040, cuda.GetProperty("driverApiVersion").GetInt32());
            Equal(6, cuda.GetProperty("computeCapability").GetProperty("major").GetInt32());
            Equal(1, cuda.GetProperty("computeCapability").GetProperty("minor").GetInt32());
            Equal("driver_api_probe_no_ai_workload", cuda.GetProperty("scope").GetString());
            Check(!cuda.TryGetProperty("toolkitVersion", out _), "the probe cannot identify an installed toolkit");
            var text = GpuTestReportFormatter.ToPlainText(Sample(), UiLanguage.English, new());
            Contains("Maximum CUDA driver API version: 12.4", text);
            Contains("No game or AI model was executed", text);
        }),
        ("Every GPU result state has a stable machine code, including interruption", () =>
        {
            var states = new[]
            {
                (GpuTestStatus.Passed, "passed"), (GpuTestStatus.NotAvailable, "not_available"),
                (GpuTestStatus.Inconclusive, "inconclusive"), (GpuTestStatus.NotRun, "not_run"),
                (GpuTestStatus.Cancelled, "cancelled"), (GpuTestStatus.TimedOut, "timed_out"),
                ((GpuTestStatus)999, "inconclusive")
            };
            foreach (var (state, code) in states)
            {
                var report = GpuTestProtocol.WithoutResults(state, "not_run");
                using var document = JsonDocument.Parse(GpuTestReportFormatter.ToJson(report, new()));
                Equal(code, document.RootElement.GetProperty("direct3D11").GetProperty("status").GetString());
                Equal(code, document.RootElement.GetProperty("computeShader").GetProperty("status").GetString());
                Equal(code, document.RootElement.GetProperty("cuda").GetProperty("status").GetString());
                Check(!document.RootElement.GetProperty("timing").TryGetProperty("measurements", out _), "empty results never carry measurements");
            }
        }),
        ("GPU exports escape hardware text and retain genuine multilingual content", () =>
        {
            var sample = Sample();
            var report = sample with { Adapter = sample.Adapter! with { Name = "[card](https://example.invalid)\n<script>\u202E 显卡 العربية" } };
            var markdown = GpuTestReportFormatter.ToMarkdown(report, UiLanguage.English, new());
            DoesNotContain("[card](", markdown);
            DoesNotContain("<script>", markdown);
            DoesNotContain("\u202E", markdown);
            Contains("\\[card\\]", markdown);
            Contains("显卡 العربية", markdown);
        }),
        ("All GPU UI and report strings are present and translated in every catalog", () =>
        {
            Check(LocalizedText.ValidateCatalogs().Count == 0, "catalogs must have identical nonempty key sets");
            var english = LocalizedText.GetTranslations(UiLanguage.English);
            foreach (var language in LanguageCatalog.All)
            {
                var translations = LocalizedText.GetTranslations(language.Language);
                foreach (var key in english.Keys.Where(key => key.StartsWith("gpu.", StringComparison.Ordinal)))
                    Check(translations.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), "missing GPU translation");
                if (language.Language != UiLanguage.English)
                {
                    Check(translations["gpu.intro"] != english["gpu.intro"], "intro must be translated");
                    Check(translations["gpu.limitations"] != english["gpu.limitations"], "limitations must be translated");
                }
                var text = GpuTestReportFormatter.ToPlainText(Sample(), language.Language, new());
                Contains(translations["gpu.title"], text);
                Contains(translations["gpu.limitations"], text);
                Contains(translations["gpu.median"], text);
            }
        })
    ];

    private static GpuTestReport Sample() => new(DateTimeOffset.UnixEpoch,
        new(87654321, "NVIDIA GeForce GTX 1060", 0x10DE, 0x1C03, 8589934592, false, "31.0.15.4567", "private-session-luid"),
        new(GpuTestStatus.Passed, "d3d11_created", "11_0"),
        new(GpuTestStatus.Passed, "compute_verified"),
        new(GpuTestStatus.Passed, "cuda_driver_ready", 12040, 6, 1),
        new(GpuTestStatus.Passed, "gpu_timing_measured", GpuTestProtocol.Scenario,
            GpuTestProtocol.ElementCount, GpuTestProtocol.Iterations, GpuTestProtocol.WarmupCount, [7, 2, 3, 11, 1]));

    private static IEnumerable<string> Formats(GpuTestReport report, GpuTestReportOptions options)
    {
        yield return GpuTestReportFormatter.ToJson(report, options);
        foreach (var language in new[] { UiLanguage.English, UiLanguage.French })
        {
            yield return GpuTestReportFormatter.ToPlainText(report, language, options);
            yield return GpuTestReportFormatter.ToMarkdown(report, language, options);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual) => Check(Equals(expected, actual), $"expected {expected}; got {actual}");
    private static void Contains(string expected, string actual) => Check(actual.Contains(expected, StringComparison.Ordinal), $"missing {expected}");
    private static void DoesNotContain(string excluded, string actual) => Check(!actual.Contains(excluded, StringComparison.Ordinal), $"unexpected {excluded}");
}
