using System.Net;
using System.Text.Json;
using DiskOptimizer.Models;
using DiskOptimizer.Services;

int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
PnpDeviceItem Device(string version = "32.0.16.1088") => new() { DeviceId = @"PCI\VEN_10DE&DEV_2705&SUBSYS_E13B1462", DeviceName = "NVIDIA GeForce RTX 4070 Ti SUPER", Category = "Display", OsVersion = "10.0.26100", OsArchitecture = "AMD64", OsProductType = 1, DriverVersion = version };
string Response(string latest = "616.92", string model = "NVIDIA%20GeForce%20RTX%204070%20Ti%20SUPER", string url = "https://www.nvidia.com/en-us/drivers/details/123/", string os = "Windows%2011", string whql = "1") => JsonSerializer.Serialize(new { Success = "1", IDS = new[] { new { downloadInfo = new { Version = latest, DetailsURL = url, Is64Bit = "1", IsWHQL = whql, IsCRD = "0", IsDC = "1", IsBeta = "0", ReleaseDateTime = "Wed Sep 09, 2026", OSList = new[] { new { OSName = os } }, series = new[] { new { products = new[] { new { productName = model } } } } } } } });
Check(DriverCatalogService.NormalizeNvidiaVersion("32.0.16.1088") == "610.88", "Windows version normalization preserves third component");
Check(DriverCatalogService.NormalizeNvidiaVersion("31.0.15.6094") == "560.94", "Previous NVIDIA branch normalization");
Check(DriverCatalogService.NormalizeNvidiaVersion("bad") == null, "Unknown version is not current");
Check(DriverCatalogService.NormalizeNvidiaVersion("32.0.16.10000") == null, "Malformed revision rejected");
Check(DriverCatalogService.TryGetNvidiaDeviceId(Device(), out var pci) && pci == "2705", "Exact PCI hex extracted");
var otherVendor = Device(); otherVendor.DeviceId = @"PCI\VEN_1002&DEV_2705";
Check(!DriverCatalogService.TryGetNvidiaDeviceId(otherVendor, out _), "Same device number of another vendor rejected");
var arm = Device(); arm.OsArchitecture = "ARM64";
Check(!DriverCatalogService.TryGetWindowsCatalogId(arm, out _), "ARM is not falsely matched to x64");
var oldOs = Device(); oldOs.OsVersion = "6.1.7601";
Check(!DriverCatalogService.TryGetWindowsCatalogId(oldOs, out _), "Unsupported Windows rejected");
var win10 = Device(); win10.OsVersion = "10.0.19045";
Check(DriverCatalogService.TryGetWindowsCatalogId(win10, out var osId) && osId == 57, "Windows 10 catalogue mapped");
Check(DriverCatalogService.ParseNvidiaResponse(Device(), Response()).Status == DriverAssessmentStatus.UpdateAvailable, "Newer exact model package detected");
Check(DriverCatalogService.ParseNvidiaResponse(Device("32.0.16.1692"), Response()).Status == DriverAssessmentStatus.UpToDate, "Same version current in catalogue");
Check(DriverCatalogService.ParseNvidiaResponse(Device("32.0.16.2099"), Response()).Status == DriverAssessmentStatus.NewerInstalled, "Newer installed never downgraded");
Check(DriverCatalogService.ParseNvidiaResponse(Device("Brak danych"), Response()).Status == DriverAssessmentStatus.Unknown, "Missing installed version unknown");
Check(DriverCatalogService.ParseNvidiaResponse(Device(), Response(model: "GeForce RTX 4070 Ti")).Status == DriverAssessmentStatus.Unsupported, "Similar GPU is not exact model");
Check(DriverCatalogService.ParseNvidiaResponse(Device(), Response(os: "Windows%2010%2064-bit")).Status == DriverAssessmentStatus.Unsupported, "Wrong OS rejected");
Check(DriverCatalogService.ParseNvidiaResponse(Device(), Response(whql: "0")).Status == DriverAssessmentStatus.Unsupported, "Non-WHQL package rejected");
Check(DriverCatalogService.ParseNvidiaResponse(Device(), Response(url: "https://nvidia.com.evil.example/download")).Status == DriverAssessmentStatus.Unsupported, "Lookalike download origin rejected");
Check(!DriverCatalogService.IsOfficialNvidiaUrl("https://evil@nvidia.com/file"), "URL credentials rejected");
Check(!DriverCatalogService.IsOfficialNvidiaUrl("http://www.nvidia.com/file"), "Non-HTTPS rejected");
Check(DriverCatalogService.ParseNvidiaResponse(Device(), "{\"Success\":\"0\"}").Status == DriverAssessmentStatus.Unsupported, "Empty catalogue never current");
using var offline = new HttpClient(new Handler((_, _) => throw new HttpRequestException("offline")));
var offlineResults = await new DriverCatalogService(offline).AssessAsync(new[] { Device() });
Check(offlineResults.Single().Status == DriverAssessmentStatus.Error, "Network errors explicit");
using var fake = new HttpClient(new Handler((request, _) => { Check(request.RequestUri!.Query.Contains("deviceID=2705") && request.RequestUri.Query.Contains("osID=135"), "HTTP query matches actual hardware and OS"); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Response()) }); }));
Check((await new DriverCatalogService(fake).AssessAsync(new[] { Device() })).Single().HasUpdate, "End-to-end provider response used");
var amdResult = (await new DriverCatalogService(fake).AssessAsync(new[] { otherVendor })).Single();
Check(amdResult.Status == DriverAssessmentStatus.Unsupported && amdResult.SourceUrl.Contains("amd.com"), "AMD explicit limited coverage without irrelevant HTTP request");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
try { await new DriverCatalogService(fake).AssessAsync(new[] { Device() }, ct: cancelled.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { passed++; Console.WriteLine("PASS cancellation propagated"); }
if (args.Contains("--live"))
{
    var result = (await new DriverCatalogService().AssessAsync(new[] { Device() })).Single();
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Check(result.Status is DriverAssessmentStatus.UpdateAvailable or DriverAssessmentStatus.UpToDate or DriverAssessmentStatus.NewerInstalled, "Live official NVIDIA catalogue response verified");
}
Console.WriteLine($"{passed} tests passed.");

sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request, cancellationToken);
}
